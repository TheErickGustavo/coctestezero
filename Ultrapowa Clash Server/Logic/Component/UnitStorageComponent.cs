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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UCS.Logic
{
    class UnitStorageComponent : Component
    {
        private List<UnitSlot> m_vUnits;//a1 + 12;
        private int m_vMaxCapacity;//a1 + 16
        public bool IsSpellForge;//a1 + 20


        public UnitStorageComponent(GameObject go, int capacity) : base(go)
        {
            m_vUnits = new List<UnitSlot>();
            m_vMaxCapacity = capacity;
            SetStorageType(go);   
        }

        public void AddUnit(CombatItemData cd)

        {
            AddUnitImpl(cd, -1);
        }

        public void AddUnitImpl(CombatItemData cd, int level)
        {
            if(CanAddUnit(cd))
            {
                int unitIndex = GetUnitTypeIndex(cd, level);
                if(unitIndex == -1)
                {
                    UnitSlot us = new UnitSlot(cd, level, 1);
                    m_vUnits.Add(us);
                }
                else
                {
                    m_vUnits[unitIndex].Count++;
                }
                ClientAvatar ca = GetParent().GetLevel().GetPlayerAvatar();
                int unitCount = ca.GetUnitCount(cd);
                ca.SetUnitCount(cd, unitCount + 1);
            }
        }

        // Doacao de cla: adiciona a unidade ao castelo sem mexer no contador do avatar
        // do receptor (a tropa doada nao conta como exercito proprio do receptor).
        // Capacidade conferida LOCALMENTE (so este armazem): CanAddUnit() usava o total
        // da vila (acampamentos etc.), o que barrava a doacao errado.
        public bool AddDonatedUnit(CombatItemData cd)
        {
            if (cd == null)
                return false;
            if (GetUsedCapacity() + cd.GetHousingSpace() > GetMaxCapacity())
                return false;
            int unitIndex = GetUnitTypeIndex(cd, -1);
            if (unitIndex == -1)
                m_vUnits.Add(new UnitSlot(cd, -1, 1));
            else
                m_vUnits[unitIndex].Count++;
            return true;
        }

        public bool CanAddUnit(CombatItemData cd)
        {
            bool result = false;
            if(cd != null)
            {
                if(IsSpellForge)
                {
                    result = (GetMaxCapacity() >= GetUsedCapacity() + cd.GetHousingSpace());
                }
                else
                {
                    var cm = GetParent().GetLevel().GetComponentManager();
                    int maxCapacity = cm.GetTotalMaxHousing();//GetMaxCapacity();
                    int usedCapacity = cm.GetTotalUsedHousing();//GetUsedCapacity();
                    int housingSpace = cd.GetHousingSpace();
                    if (GetUsedCapacity() < GetMaxCapacity())
                        result = (maxCapacity >= usedCapacity + housingSpace);
                } 
            }   
            return result;
        }

        //R-T7: o id que o CLIENTE entende de uma tropa e o do TIPO (nivel 1). O
        //GetGlobalID() do servidor usa um "count" parcial e devolve ids deslocados
        //(ex.: 4000008 em vez de 4000049 para o Dragon) - o cliente nao reconhecia.
        public static CharacterData GetBaseType(CombatItemData cd)
        {
            if (cd == null)
                return null;
            try
            {
                return ObjectManager.DataTables.GetCharacterByName(cd.GetName());
            }
            catch { return null; }
        }

        public int GetMaxCapacity()
        {
            return m_vMaxCapacity;
        }

        //X4b: quantidade de tipos de unidade guardados (p/ sincronizar o avatar)
        public int GetUnitSlotCount()
        {
            return m_vUnits.Count;
        }

        public int GetUnitCount(int index)
        {
            return m_vUnits[index].Count;
        }

        public int GetUnitCountByData(CombatItemData cd)
        {
            int count = 0;
            for (int i = 0; i < m_vUnits.Count; i++)
            {
                if (m_vUnits[i].UnitData == cd)
                    count += m_vUnits[i].Count;
            }
            return count;
        }

        public int GetUnitLevel(int index)
        {
            return m_vUnits[index].Level;
        }

        public CombatItemData GetUnitType(int index)
        {
            return m_vUnits[index].UnitData;
        }

        public int GetUnitTypeIndex(CombatItemData cd)
        {
            int index = -1;
            for(int i=0;i<m_vUnits.Count;i++)
            {
                if (m_vUnits[i].UnitData == cd)
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        public int GetUnitTypeIndex(CombatItemData cd, int level)
        {
            int index = -1;
            for (int i = 0; i < m_vUnits.Count; i++)
            {
                if (m_vUnits[i].UnitData == cd)
                {
                    if(m_vUnits[i].Level == level)
                    {
                        index = i;
                        break;
                    } 
                }
            }
            return index;
        }

        public int GetUsedCapacity()
        {
            int count = 0;
            if (m_vUnits.Count >= 1)
            {
                for (int i = 0; i < m_vUnits.Count; i++)
                {
                    int cnt = m_vUnits[i].Count;
                    int housingSpace = m_vUnits[i].UnitData.GetHousingSpace();
                    count += cnt * housingSpace;
                }
            }
            return count;
        }

        public void RemoveUnits(CombatItemData cd, int count)
        {
            RemoveUnitsImpl(cd,-1,count);
        }

        public void RemoveUnitsImpl(CombatItemData cd, int level, int count)
        {
            int unitIndex = GetUnitTypeIndex(cd, level);
            //R-C5: desconta do avatar exatamente o que foi removido do storage (antes
            //descontava o valor pedido, mesmo quando o storage tinha menos -> contador
            //do avatar ficava menor que o real e o multiplayer mostrava menos tropas).
            int removed = 0;
            if (unitIndex != -1)
            {
                UnitSlot us = m_vUnits[unitIndex];
                removed = us.Count <= count ? us.Count : count;
                if (us.Count <= count)
                {
                    m_vUnits.Remove(us);
                }
                else
                {
                    us.Count -= count;
                }
            }
            if (removed > 0)
            {
                ClientAvatar ca = GetParent().GetLevel().GetPlayerAvatar();
                int unitCount = ca.GetUnitCount(cd);
                ca.SetUnitCount(cd, Math.Max(0, unitCount - removed));
            }
        }

        //R-C5: remove unidades de qualquer nivel (usado ao colocar tropas na batalha).
        public bool RemoveUnitsAnyLevel(CombatItemData cd, int count)
        {
            for (int s = 0; s < m_vUnits.Count; s++)
            {
                if (m_vUnits[s].UnitData == cd && m_vUnits[s].Count > 0)
                {
                    int removed = m_vUnits[s].Count <= count ? m_vUnits[s].Count : count;
                    m_vUnits[s].Count -= removed;
                    if (m_vUnits[s].Count <= 0)
                        m_vUnits.RemoveAt(s);
                    ClientAvatar ca = GetParent().GetLevel().GetPlayerAvatar();
                    ca.SetUnitCount(cd, Math.Max(0, ca.GetUnitCount(cd) - removed));
                    return true;
                }
            }
            return false;
        }

        public override void Load(JObject jsonObject)
        {
            var storageTypeToken = jsonObject["storage_type"];
            IsSpellForge = (storageTypeToken != null) && (storageTypeToken.ToObject<int>() == 1);
            JArray unitArray = (JArray)jsonObject["units"];
            if (unitArray != null)
            {
                if(unitArray.Count > 0)
                {
                    foreach (JArray unitSlotArray in unitArray)
                    {
                        int id = unitSlotArray[0].ToObject<int>();
                        int cnt = unitSlotArray[1].ToObject<int>();
                        m_vUnits.Add(new UnitSlot((CombatItemData)ObjectManager.DataTables.GetDataById(id), -1, cnt));
                    }
                }  
            }
        }

        public override JObject Save(JObject jsonObject)
        {
            //{"data":1000000,"lvl":7,"x":10,"y":35,"units":[[4000005,11],[4000000,1],[4000001,1]],"storage_type":0,"l1x":10,"l1y":35}

            JArray unitJsonArray = new JArray();
            if(m_vUnits.Count > 0)
            { 
                foreach (var unit in m_vUnits)
                {
                    JArray unitSlotJsonArray = new JArray();
                    //R-T7: grava o id do TIPO da tropa (ex.: 4000049 = Dragon), como o
                    //jogo real faz. O GetGlobalID() do servidor usa um "count" parcial
                    //e devolvia ids deslocados (ex.: 4000008 = Archer nivel 2) - o
                    //cliente 7.65 nao reconhecia a tropa e o castelo do cla (que usa
                    //alliance_units) nunca aparecia na tela de ataque.
                    int unitId = unit.UnitData.GetGlobalID();
                    CharacterData baseCd = null;
                    try
                    {
                        baseCd = ObjectManager.DataTables.GetCharacterByName(unit.UnitData.GetName());
                    }
                    catch { }
                    if (baseCd != null)
                        unitId = baseCd.GetGlobalID();
                    unitSlotJsonArray.Add(unitId);
                    unitSlotJsonArray.Add(unit.Count);
                    unitJsonArray.Add(unitSlotJsonArray);
                }
            }
            jsonObject.Add("units", unitJsonArray);
            
            if (IsSpellForge)
                jsonObject.Add("storage_type", 1);
            else
                jsonObject.Add("storage_type", 0);

            return jsonObject;
        }

        public void SetMaxCapacity(int capacity)
        {
            m_vMaxCapacity = capacity;
        }

        //R-C5: o contador de tropas do avatar (por tipo) e os slots do storage (por
        //tipo+nivel) podem divergir: o RemoveUnitsImpl so decrementa o avatar quando
        //acha o slot com o nivel exato, e o Load() recarrega tudo com nivel -1. O
        //cliente mostra o avatar no multiplayer (24107) e o storage no acampamento
        //(24101), dai numeros diferentes (90 no acampamento x 86 no multiplayer).
        //Funcao: reconstroi os contadores do avatar a partir do storage (fonte da
        //verdade) e remove os slots zerados.
        public static void SyncAvatarUnitCounts(Level level)
        {
            if (level == null)
                return;
            ClientAvatar ca = level.GetPlayerAvatar();
            if (ca == null)
                return;
            try
            {
                Dictionary<int, int> totals = new Dictionary<int, int>();
                List<Component> components = level.GetComponentManager().GetComponents(0);
                for (int i = 0; i < components.Count; i++)
                {
                    UnitStorageComponent storage = components[i] as UnitStorageComponent;
                    if (storage == null)
                        continue;
                    //R-C5: o CASTELO DO CLA (data 1000014) guarda as tropas DOADAS: elas
                    //nao fazem parte do exercito do jogador (nao ocupam o acampamento
                    //nem aparecem na barra de ataque como tropa normal - no CoC elas
                    //saem do icone do escudo do cla). Antes o codigo excluia 1000143
                    //(que nao existe nesta versao), entao a tropa doada aparecia como
                    //se estivesse no acampamento.
                    Building sb = storage.GetParent() as Building;
                    if (sb != null && sb.GetBuildingData() != null &&
                        sb.GetBuildingData().GetGlobalID() == AllianceTroopService.CastleBuildingId)
                        continue;
                    if (storage.IsSpellForge)
                        continue;
                    for (int s = 0; s < storage.GetUnitSlotCount(); s++)
                    {
                        CombatItemData cd = storage.GetUnitType(s);
                        if (cd == null)
                            continue;
                        int count = storage.GetUnitCount(s);
                        if (count <= 0)
                            continue;
                        //R-T7: id do TIPO da tropa (nivel 1) - o cliente 7.65 so
                        //reconhece esse id (o GetGlobalID() do servidor devolve ids
                        //deslocados por causa do "count" parcial do DataTable).
                        CharacterData baseType = GetBaseType(cd);
                        int id = baseType != null ? baseType.GetGlobalID() : cd.GetGlobalID();
                        if (totals.ContainsKey(id))
                            totals[id] += count;
                        else
                            totals[id] = count;
                    }
                }

                //aplica os totais do storage no avatar (tipos que existem no storage).
                //Tipos so do avatar (ex.: magos do tutorial) sao mantidos.
                //R-C5: manipula os DataSlots direto - o SetUnitCount depende de comparacao
                //de instancia (ds.Data == d) e falhava quando o Data vinhe de outra via.
                List<DataSlot> avatarUnits = ca.GetUnits();
                foreach (DataSlot ds in avatarUnits)
                {
                    if (ds.Data == null)
                        continue;
                    int gid = ds.Data.GetGlobalID();
                    if (totals.ContainsKey(gid))
                    {
                        if (ds.Value != totals[gid])
                            Console.WriteLine("[UCS] R-C5 sync: " + ds.Data.GetName() +
                                " storage=" + totals[gid] + " avatar " + ds.Value + " -> " + totals[gid]);
                        ds.Value = totals[gid];
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C5: erro ao sincronizar contador de tropas: " + ex.Message);
            }
        }

        public void SetStorageType(GameObject go)
        {
            Building b = (Building)GetParent();
            BuildingData bd = b.GetBuildingData();
            IsSpellForge = bd.IsSpellForge();
        }

        public override int Type
        {
            get { return 0; }
        }
    }
}
