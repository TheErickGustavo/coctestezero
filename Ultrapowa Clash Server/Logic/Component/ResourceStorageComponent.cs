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
    class ResourceStorageComponent : Component
    {
        private List<int> m_vCurrentResources;
        private List<int> m_vMaxResources;
        private List<int> m_vStolenResources;
        //R-C6: timer de producao (o componente era um stub: a coleta de elixir/ouro
        //nunca era creditada e sumia ao fechar o jogo).
        private Timer m_vTimer;
        private int m_vProducedIndex = -1;
        private int m_vProducedCapacity;
        private int m_vPerHour;
        private int m_vTotalSeconds;

        public ResourceStorageComponent(GameObject go) : base (go)
        {
            m_vCurrentResources = new List<int>();
            m_vMaxResources = new List<int>();
            m_vStolenResources = new List<int>();

            var table = ObjectManager.DataTables.GetTable(2);
            int resourceCount = table.GetItemCount();
            for(int i=0;i<resourceCount;i++)
            {
                m_vCurrentResources.Add(0);
                m_vMaxResources.Add(0);
                m_vStolenResources.Add(0);
            }
            SetupProduction();
        }

        //R-C6: descobre qual recurso o predio produz, a capacidade e a taxa/hora.
        private void SetupProduction()
        {
            try
            {
                Building b = GetParent() as Building;
                if (b == null)
                    return;
                BuildingData bd = b.GetBuildingData();
                if (bd == null || String.IsNullOrEmpty(bd.ProducesResource))
                    return;
                ResourceData rd = ObjectManager.DataTables.GetResourceByName(bd.ProducesResource);
                if (rd == null)
                    return;
                var table = ObjectManager.DataTables.GetTable(2);
                int idx = -1;
                for (int i = 0; i < table.GetItemCount(); i++)
                {
                    if (table.GetItemAt(i) == rd)
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx < 0)
                    return;
                int lvl = b.GetUpgradeLevel();
                if (lvl < 0)
                    lvl = 0;
                m_vProducedIndex = idx;
                m_vProducedCapacity = GetStoredCapacity(bd, lvl);
                if (bd.ResourcePerHour != null && lvl < bd.ResourcePerHour.Count)
                    m_vPerHour = bd.ResourcePerHour[lvl];
                //R-C6: a capacidade real do recurso produzido (ResourceMax) entra na
                //soma das caps da vila (o "deposito maximo" que o cliente mostra).
                if (m_vProducedIndex < m_vMaxResources.Count)
                    m_vMaxResources[m_vProducedIndex] = m_vProducedCapacity;
                StartProductionTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C6: setup de producao falhou: " + ex.Message);
            }
        }

        private int GetStoredCapacity(BuildingData bd, int lvl)
        {
            try
            {
                //R-C6: no CSV do 7.65 as colunas MaxStored* vem vazias; a capacidade real
                //do coletor e a coluna ResourceMax por nivel (500, 1000, 1500...).
                if (bd.ResourceMax != null && lvl < bd.ResourceMax.Count && bd.ResourceMax[lvl] > 0)
                    return bd.ResourceMax[lvl];
                if (bd.MaxStoredElixir != null && lvl < bd.MaxStoredElixir.Count && bd.MaxStoredElixir[lvl] > 0)
                    return bd.MaxStoredElixir[lvl];
                if (bd.MaxStoredGold != null && lvl < bd.MaxStoredGold.Count && bd.MaxStoredGold[lvl] > 0)
                    return bd.MaxStoredGold[lvl];
                if (bd.MaxStoredDarkElixir != null && lvl < bd.MaxStoredDarkElixir.Count && bd.MaxStoredDarkElixir[lvl] > 0)
                    return bd.MaxStoredDarkElixir[lvl];
            }
            catch { }
            return 0;
        }

        //R-C6: tempo para encher = capacidade / (producao por hora).
        private void StartProductionTimer()
        {
            if (m_vProducedIndex < 0 || m_vProducedCapacity <= 0)
                return;
            int seconds = 3600;
            if (m_vPerHour > 0)
                seconds = (int)((long)m_vProducedCapacity * 3600 / m_vPerHour);
            if (seconds <= 0)
                seconds = 3600;
            m_vTotalSeconds = seconds;
            m_vTimer = new Timer();
            m_vTimer.StartTimer(seconds, GetParent().GetLevel().GetTime());
        }

        //R-C6: o coletor acumula CONTINUAMENTE (como no CoC): o disponivel e
        //producao/hora * tempo decorrido, ate a capacidade. Quando o timer zera,
        //o coletor esta cheio.
        public int GetCollectable()
        {
            if (m_vProducedIndex < 0 || m_vProducedIndex >= m_vCurrentResources.Count)
                return 0;
            if (m_vTimer == null)
                return m_vProducedCapacity;
            try
            {
                int remaining = m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime());
                int elapsed = m_vTotalSeconds - remaining;
                if (elapsed <= 0)
                    return 0;
                long amount = (long)m_vPerHour * elapsed / 3600;
                if (amount > m_vProducedCapacity)
                    amount = m_vProducedCapacity;
                return (int)amount;
            }
            catch
            {
                return 0;
            }
        }

        public int GetProducedIndex()
        {
            return m_vProducedIndex;
        }

        public int GetProducedCapacity()
        {
            return m_vProducedCapacity;
        }

        public int Collect()        {
            int available = GetCollectable();
            if (available <= 0)
                return 0;
            m_vCurrentResources[m_vProducedIndex] = 0;
            StartProductionTimer();
            return available;
        }

        public void SetCount(int resourceIndex, int count)
        {
            if (resourceIndex >= 0 && resourceIndex < m_vCurrentResources.Count)
                m_vCurrentResources[resourceIndex] = count;
        }

        public int GetCount(int resourceIndex)
        {
            return m_vCurrentResources[resourceIndex];
        }

        public int GetMax(int resourceIndex)
        {
            return m_vMaxResources[resourceIndex];
        }

        public void SetMaxArray(List<int> resourceCaps)
        {
            m_vMaxResources = resourceCaps;
            GetParent().GetLevel().GetComponentManager().RefreshResourcesCaps();
        }

        //R-C6: quando o timer expira o coletor fica cheio.
        public override void Tick()
        {
            if (m_vTimer == null || m_vProducedIndex < 0)
                return;
            try
            {
                if (m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime()) <= 0)
                {
                    if (m_vProducedIndex < m_vCurrentResources.Count)
                        m_vCurrentResources[m_vProducedIndex] = m_vProducedCapacity;
                    m_vTimer = null;
                }
            }
            catch { }
        }

        public override void Load(JObject jsonObject)
        {
            //R-C6: recalcula indice/capacidade/timer agora que o nivel do predio ja foi
            //carregado do JSON (no construtor o nivel ainda era -1 -> capacidade 0).
            SetupProduction();
            JObject resProd = (JObject)jsonObject["res_prod"];
            if (resProd == null)
                return;
            try
            {
                int t = resProd["t"] != null ? resProd["t"].ToObject<int>() : 0;
                int avail = resProd["avail"] != null ? resProd["avail"].ToObject<int>() : 0;
                if (m_vProducedIndex >= 0)
                    m_vCurrentResources[m_vProducedIndex] = avail;
                if (t > 0)
                {
                    m_vTimer = new Timer();
                    m_vTimer.StartTimer(t, GetParent().GetLevel().GetTime());
                }
            }
            catch { }
        }

        public override JObject Save(JObject jsonObject)
        {
            JObject resProd = new JObject();
            resProd.Add("t", m_vTimer != null
                ? m_vTimer.GetRemainingSeconds(GetParent().GetLevel().GetTime()) : 0);
            resProd.Add("avail", GetCollectable());
            jsonObject.Add("res_prod", resProd);
            return jsonObject;
        }

        public override int Type
        {
            get { return 6; }
        }
    }
}
