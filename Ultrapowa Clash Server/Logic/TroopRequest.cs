using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UCS.Logic
{
    // X4b: pedido de tropas pendente de um membro (do mural do cla).
    // Fica em memoria na Alliance e persiste no JSON dela (restart do servidor nao perde).
    class TroopRequest
    {
        private long m_vRequesterId;
        private string m_vRequesterName;
        private string m_vMessage;
        private List<int> m_vWantedUnits;   // ids globais das tropas pedidas (vazio = qualquer tropa)
        private List<KeyValuePair<int, int>> m_vWantedCounts; // X4c: id -> quantidade exata pedida
        private int m_vCapacity;            // espaco total do castelo (housing)
        private int m_vUsedSpace;           // espaco ja preenchido (doado)
        private int m_vTimestamp;           // unix seconds (ordena os pedidos)

        public TroopRequest()
        {
            m_vWantedUnits = new List<int>();
            m_vWantedCounts = new List<KeyValuePair<int, int>>();
        }

        public long GetRequesterId()
        {
            return m_vRequesterId;
        }

        public void SetRequesterId(long id)
        {
            m_vRequesterId = id;
        }

        public string GetRequesterName()
        {
            return m_vRequesterName;
        }

        public void SetRequesterName(string name)
        {
            m_vRequesterName = name;
        }

        public string GetMessage()
        {
            return m_vMessage;
        }

        public void SetMessage(string message)
        {
            m_vMessage = message;
        }

        public List<int> GetWantedUnits()
        {
            return m_vWantedUnits;
        }

        public void SetWantedUnits(List<int> wanted)
        {
            m_vWantedUnits = (wanted != null) ? wanted : new List<int>();
        }

        // X4c: quantidades exatas pedidas (lista espelha wanted; 0 = sem quantidade definida)
        public List<KeyValuePair<int, int>> GetWantedCounts()
        {
            int n = (m_vWantedUnits != null) ? m_vWantedUnits.Count : 0;
            if (m_vWantedCounts == null || m_vWantedCounts.Count != n)
            {
                var fixedList = new List<KeyValuePair<int, int>>();
                for (int i = 0; i < n; i++)
                    fixedList.Add(new KeyValuePair<int, int>(m_vWantedUnits[i], 0));
                if (m_vWantedCounts != null)
                {
                    foreach (KeyValuePair<int, int> kv in m_vWantedCounts)
                    {
                        int idx = fixedList.FindIndex(x => x.Key == kv.Key);
                        if (idx >= 0)
                            fixedList[idx] = kv;
                    }
                }
                m_vWantedCounts = fixedList;
            }
            return m_vWantedCounts;
        }

        public void SetWantedCounts(List<KeyValuePair<int, int>> counts)
        {
            m_vWantedCounts = (counts != null) ? new List<KeyValuePair<int, int>>(counts) : new List<KeyValuePair<int, int>>();
        }

        public int GetCapacity()
        {
            return m_vCapacity;
        }

        public void SetCapacity(int capacity)
        {
            m_vCapacity = capacity;
        }

        public int GetUsedSpace()
        {
            return m_vUsedSpace;
        }

        public void SetUsedSpace(int used)
        {
            m_vUsedSpace = used;
        }

        public int GetTimestamp()
        {
            return m_vTimestamp;
        }

        public void SetTimestamp(int timestamp)
        {
            m_vTimestamp = timestamp;
        }

        public bool IsFull()
        {
            return m_vCapacity > 0 && m_vUsedSpace >= m_vCapacity;
        }

        public JObject Save(JObject o)
        {
            o.Add("requester_id", m_vRequesterId);
            o.Add("requester_name", m_vRequesterName ?? "");
            o.Add("message", m_vMessage ?? "");
            JArray wanted = new JArray();
            foreach (int id in m_vWantedUnits)
                wanted.Add(id);
            o.Add("wanted", wanted);
            JArray counts = new JArray();
            if (m_vWantedCounts != null)
            {
                foreach (KeyValuePair<int, int> kv in m_vWantedCounts)
                {
                    JArray pair = new JArray();
                    pair.Add(kv.Key);
                    pair.Add(kv.Value);
                    counts.Add(pair);
                }
            }
            o.Add("wanted_counts", counts);
            o.Add("capacity", m_vCapacity);
            o.Add("used_space", m_vUsedSpace);
            o.Add("timestamp", m_vTimestamp);
            return o;
        }

        public void Load(JObject o)
        {
            m_vRequesterId = o["requester_id"].ToObject<long>();
            m_vRequesterName = (o["requester_name"] != null) ? o["requester_name"].ToObject<string>() : "";
            m_vMessage = (o["message"] != null) ? o["message"].ToObject<string>() : "";
            m_vWantedUnits = new List<int>();
            if (o["wanted"] != null)
            {
                foreach (var w in (JArray)o["wanted"])
                    m_vWantedUnits.Add(w.ToObject<int>());
            }
            m_vCapacity = (o["capacity"] != null) ? o["capacity"].ToObject<int>() : 0;
            m_vWantedCounts = new List<KeyValuePair<int, int>>();
            if (o["wanted_counts"] != null)
            {
                foreach (var pair in (JArray)o["wanted_counts"])
                {
                    if (pair is JArray && ((JArray)pair).Count >= 2)
                        m_vWantedCounts.Add(new KeyValuePair<int, int>(pair[0].ToObject<int>(), pair[1].ToObject<int>()));
                }
            }
            m_vUsedSpace = (o["used_space"] != null) ? o["used_space"].ToObject<int>() : 0;
            m_vTimestamp = (o["timestamp"] != null) ? o["timestamp"].ToObject<int>() : 0;
        }
    }
}
