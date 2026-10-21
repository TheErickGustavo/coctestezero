using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Configuration;
using UCS.PacketProcessing;
using UCS.Core;
using UCS.GameFiles;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UCS.Logic
{
    class UnitProductionComponent : Component
    {
        private List<DataSlot> m_vUnits;//a1 + 12
        private Timer m_vTimer;//a1 + 16
        private bool m_vIsWaitingForSpace;//a1 + 20
        private bool m_vIsSpellForge;//a1 + 24

        public UnitProductionComponent(GameObject go) : base(go)
        {
            m_vUnits = new List<DataSlot>();
            SetUnitType(go);
            m_vTimer = null;
            m_vIsWaitingForSpace = false;
        }

        public void AddUnitToProductionQueue(CombatItemData cd)
        {
            if(CanAddUnitToQueue(cd))
            {
                for(int i=0;i<GetSlotCount();i++)
                {
                    if ((CombatItemData)m_vUnits[i].Data == cd)
                    {
                        m_vUnits[i].Value++;
                        return;
                    }
                }
                DataSlot ds = new DataSlot(cd, 1);
                m_vUnits.Add(ds);
                if(m_vTimer == null)
                {
                    ClientAvatar ca = GetParent().GetLevel().GetHomeOwnerAvatar();
                    m_vTimer = new Timer();
                    int trainingTime = cd.GetTrainingTime(ca.GetUnitUpgradeLevel(cd));
                    m_vTimer.StartTimer(trainingTime, GetParent().GetLevel().GetTime());
                }
            }
        }

        public bool CanAddUnitToQueue(CombatItemData cd)
        {
            //Console.WriteLine(GetMaxTrainCount());
            //Console.WriteLine(GetTotalCount());
            //Console.WriteLine(cd.GetHousingSpace());
            //R-C5: NAO checar o espaco de habitacao aqui. O cliente 7.65 nao checa
            //(ele so limita pela fila do quartel) e, se o servidor recusasse a compra,
            //o cliente e o servidor divergiam: o jogo mostrava a tropa comprada mas o
            //servidor nunca a tinha (ex.: comprar um Dragao de 20 espacos com a vila
            //em 265/275). O espaco e tratado na entrega (ProductionCompleted).
            return (GetMaxTrainCount() >= GetTotalCount() + cd.GetHousingSpace());
        }

        public CombatItemData GetCurrentlyTrainedUnit()
        {
            CombatItemData cd = null;
            if (m_vUnits.Count >= 1)
                cd = (CombatItemData)m_vUnits[0].Data;
            return cd;
        }

        public int GetMaxTrainCount()
        {
            Building b = (Building)GetParent();
            BuildingData bd = b.GetBuildingData();
            return bd.GetUnitProduction(b.GetUpgradeLevel());
        }

        public int GetSlotCount()
        {
            return m_vUnits.Count;
        }

        public int GetTotalCount()
        {
            int count = 0;
            if(GetSlotCount() >= 1)
            {
                for(int i=0;i<GetSlotCount();i++)
                {
                    int cnt = m_vUnits[i].Value;
                    int housingSpace = ((CombatItemData)m_vUnits[i].Data).GetHousingSpace();
                    count += cnt * housingSpace;
                }
            }
            if (m_vIsSpellForge)
            {
                count += GetParent().GetLevel().GetComponentManager().GetTotalUsedHousing(true);
            }
            return count;
        }

        public int GetTotalRemainingSeconds()
        {
            int result = 0;
            bool firstUnit = true;
            if(m_vUnits.Count > 0)
            {
                foreach(var ds in m_vUnits)
                {
                    CombatItemData cd = (CombatItemData)ds.Data;
                    if(cd != null)
                    {
                        int count = ds.Value;
                        if(count >= 1)
                        {
                            if(firstUnit)
                            {
                                if (m_vTimer != null)
                                    result += m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime());
                                count--;
                                firstUnit = false;
                            }
                            ClientAvatar ca = GetParent().GetLevel().GetHomeOwnerAvatar();
                            result += count * cd.GetTrainingTime(ca.GetUnitUpgradeLevel(cd));
                        }
                    }
                }
            }
            return result;
        }

        public int GetTrainingCount(int index)
        {
            return m_vUnits[index].Value;
        }

        public CombatItemData GetUnit(int index)
        {
            return (CombatItemData)m_vUnits[index].Data;
        }

        public bool HasHousingSpaceForSpeedUp()
        {
            int totalRoom = 0;
            if(m_vUnits.Count >= 1)
            {
                foreach(var ds in m_vUnits)
                {
                    CombatItemData cd = (CombatItemData)ds.Data;
                    totalRoom += cd.GetHousingSpace() * ds.Value;
                }
            }
            ComponentManager cm = GetParent().GetLevel().GetComponentManager();
            int usedHousing = cm.GetTotalUsedHousing(m_vIsSpellForge);
            int maxHousing = cm.GetTotalMaxHousing(m_vIsSpellForge);
            return (totalRoom <= maxHousing - usedHousing);
        }

        public bool IsSpellForge()
        {
            return m_vIsSpellForge;
        }

        public bool IsWaitingForSpace()
        {
            bool result = false;
            if(m_vUnits.Count > 0)
            {
                if(m_vTimer != null)
                {
                    if (m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime()) == 0)
                    {
                        result = m_vIsWaitingForSpace;
                    }
                }
            }
            return result;
        }

        public bool ProductionCompleted()
        {
            bool result = false;
            if (m_vUnits.Count == 0)
                return result;
            CombatItemData cd = (CombatItemData)m_vUnits[0].Data;
            ComponentManager cm = GetParent().GetLevel().GetComponentManager();

            //R-C5: busca DIRETA pelo storage de tropas mais proximo que ACEITE a unidade.
            //A versao anterior usava ComponentManager.GetClosestComponent + ComponentFilter
            //com lista de "ignorados"; o filtro acabava rejeitando TODOS os predios
            //(a lista de ignorados ja vinha populada), entao nenhuma tropa era entregue
            //e elas ficavam na fila do quartel para sempre.
            List<Component> storages = cm.GetComponents(0);
            UnitStorageComponent best = null;
            double bestDistance = double.MaxValue;
            Vector from = new Vector(GetParent().X, GetParent().Y);
            for (int i = 0; i < storages.Count; i++)
            {
                UnitStorageComponent st = storages[i] as UnitStorageComponent;
                if (st == null || st.IsSpellForge)
                    continue;
                GameObject go = st.GetParent();
                if (go == null)
                    continue;
                //R-T7: o CASTELO DO CLA tambem e um storage de tropas, mas as tropas
                //TREINADAS nunca podem parar la (so as doadas). Como a busca e por
                //PROXIMIDADE, o castelo (que fica perto do quartel) recebia as tropas
                //do jogador - elas sumiam do acampamento e apareciam no castelo (e o
                //castelo enchia, '-15 de espaco' no acampamento).
                Building stb = go as Building;
                if (stb != null && stb.GetBuildingData() != null &&
                    stb.GetBuildingData().GetGlobalID() == AllianceTroopService.CastleBuildingId)
                    continue;
                if (!st.CanAddUnit(cd))
                    continue;
                double distance = (from - go.GetPosition()).LengthSquared;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = st;
                }
            }

            if (best != null)
            {
                best.AddUnit(cd);
                StartProducingNextUnit();
                result = true;
            }
            else
            {
                m_vIsWaitingForSpace = true;
                Building pb = GetParent() as Building;
                Console.WriteLine("[UCS] R-C5 prod: nenhum storage com espaco para " +
                    (cd != null ? cd.GetName() : "?") + " perto de " +
                    (pb != null && pb.GetBuildingData() != null ? pb.GetBuildingData().GetGlobalID() + " " + pb.GetBuildingData().TID + " lvl" + pb.GetUpgradeLevel() : "?") +
                    " fila=" + GetTotalCount() + " storagesNaVila=" + storages.Count +
                    " vilaMax=" + cm.GetTotalMaxHousing() + " vilaUsed=" + cm.GetTotalUsedHousing());
            }
            return result;
        }

        public void RemoveUnit(CombatItemData cd)
        {
            int index = -1;
            if(GetSlotCount() >= 1)
            {
                for(int i=0;i<GetSlotCount();i++)
                {
                    if (m_vUnits[i].Data == cd)
                        index = i;
                }
            }
            if(index != -1)
            {
                if (m_vUnits[index].Value >= 1)
                {
                    m_vUnits[index].Value--;
                    if (m_vUnits[index].Value == 0)
                    {
                        m_vUnits.RemoveAt(index);
                    }
                    //R-C5: reinicia o timer SEMPRE que ainda houver fila. Antes o timer so
                    //reiniciava quando o slot ZERAVA, entao a 2a tropa da mesma fila ficava
                    //presa para sempre (a 1a saia normal, a 2a ficava em "looping" /
                    //"tropa fantasma" - exatamente o sintoma reportado).
                    if(GetSlotCount() >= 1)
                    {
                        DataSlot ds = m_vUnits[0];
                        CombatItemData newcd = (CombatItemData)m_vUnits[0].Data;
                        ClientAvatar ca = GetParent().GetLevel().GetHomeOwnerAvatar();
                        m_vTimer = new Timer();
                        int trainingTime = newcd.GetTrainingTime(ca.GetUnitUpgradeLevel(newcd));
                        m_vTimer.StartTimer(trainingTime, GetParent().GetLevel().GetTime());
                    }
                    else
                    {
                        //R-C5: fila vazia = nada em producao. Sem isso o timer
                        //continuava correndo e o cliente mostrava uma tropa
                        //fantasma ("o quartel continua fazendo" apos cancelar).
                        m_vTimer = null;
                    }
                }
            }
        }

        public override void Load(JObject jsonObject)
        {
            JObject unitProdObject = (JObject)jsonObject["unit_prod"];
            if (unitProdObject == null)
                return;
            m_vIsSpellForge = (unitProdObject["unit_type"].ToObject<int>() == 1);

            //R-C5: carrega a FILA antes do timer: so cria o timer se houver fila.
            //Com "t" > 0 e a fila vazia o cliente mostrava uma tropa em producao
            //fantasma (o quartel "continua fazendo" tropas que foram canceladas).
            JArray unitJsonArray = (JArray)unitProdObject["slots"];
            if (unitJsonArray != null)
            {
                foreach (JObject unitJsonObject in unitJsonArray)
                {
                    int id = unitJsonObject["id"].ToObject<int>();
                    int cnt = unitJsonObject["cnt"].ToObject<int>();
                    m_vUnits.Add(new DataSlot(ObjectManager.DataTables.GetDataById(id),cnt));
                }
            }

            var timeToken = unitProdObject["t"];
            if (timeToken != null && m_vUnits.Count >= 1)
            {
                m_vTimer = new Timer();
                int remainingTime = timeToken.ToObject<int>();
                m_vTimer.StartTimer(remainingTime, GetParent().GetLevel().GetTime());
                //R-C5: log do timer restaurado (diagnostico do "reset" ao trocar de vila)
                Building pbLoad = GetParent() as Building;
                Console.WriteLine("[UCS] R-C5 prod-load: predio " +
                    (pbLoad != null && pbLoad.GetBuildingData() != null ? pbLoad.GetBuildingData().GetGlobalID() : 0) + " fila=" + GetTotalCount() +
                    " tRestante=" + remainingTime + " clockLevel=" +
                    GetParent().GetLevel().GetTime().ToString("HH:mm:ss"));
            }
        }

        public override JObject Save(JObject jsonObject)
        {
            //{"data":1000006,"lvl":3,"x":12,"y":34,"unit_prod":{"unit_type":0,"t":0,"slots":[{"id":4000000,"cnt":19}]}}
            JObject unitProdObject = new JObject();
            if(m_vIsSpellForge)
                unitProdObject.Add("unit_type",1);
            else
                unitProdObject.Add("unit_type",0);

            //R-C5: o cliente 7.65 SEMPRE espera "t" e "slots" no unit_prod (sem eles o
            //parser entra em loop e o app morre). Alem disso, "t" com a fila VAZIA faz o
            //cliente mostrar uma tropa em producao fantasma (o quartel "continua
            //fazendo" tropas depois de cancelado). Com a fila vazia, t = 0.
            int remaining = 0;
            if (m_vTimer != null && GetSlotCount() >= 1)
                remaining = m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime());
            if (remaining < 0)
                remaining = 0;
            //R-C5: log do timer de producao (diagnostico do "reset" ao trocar de vila)
            if (GetSlotCount() >= 1)
            {
                Building pbSave = GetParent() as Building;
                Console.WriteLine("[UCS] R-C5 prod-save: predio " +
                    (pbSave != null && pbSave.GetBuildingData() != null ? pbSave.GetBuildingData().GetGlobalID() : 0) + " fila=" + GetTotalCount() +
                    " tRestante=" + remaining + " clockLevel=" +
                    GetParent().GetLevel().GetTime().ToString("HH:mm:ss"));
            }
            unitProdObject.Add("t", remaining);

            JArray unitJsonArray = new JArray();
            foreach(var unit in m_vUnits)
            {
                JObject unitJsonObject = new JObject();
                unitJsonObject.Add("id", unit.Data.GetGlobalID());
                unitJsonObject.Add("cnt", unit.Value);
                unitJsonArray.Add(unitJsonObject);
            }
            unitProdObject.Add("slots",unitJsonArray);

            jsonObject.Add("unit_prod", unitProdObject);
            return jsonObject;
        }

        public void SetUnitType(GameObject go)
        {
            Building b = (Building)GetParent();
            BuildingData bd = b.GetBuildingData();
            m_vIsSpellForge = bd.IsSpellForge();
        }

        public void SpeedUp()
        {
            while(m_vUnits.Count >= 1 && ProductionCompleted())
            { }
        }

        public void StartProducingNextUnit()
        {
            m_vTimer = null;
            if (GetSlotCount() >= 1)
            {
                RemoveUnit((CombatItemData)m_vUnits[0].Data);
            }
        }

        public override void Tick()
        {
            if(m_vTimer != null)
            {
                if (m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime()) <= 0)
                {
                    ProductionCompleted();
                }
            }  
        }

        public override int Type
        {
            get { return 3; }
        }
    }
}
