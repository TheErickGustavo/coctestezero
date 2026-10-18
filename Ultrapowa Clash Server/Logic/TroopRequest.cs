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
        private int m_vCapacity;            // espaco total do castelo (housing)
        private int m_vUsedSpace;           // espaco ja preenchido (doado)
        private int m_vTimestamp;           // unix seconds (ordena os pedidos)

        public TroopRequest()
        {
            m_vWantedUnits = new List<int>();
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
            m_vUsedSpace = (o["used_space"] != null) ? o["used_space"].ToObject<int>() : 0;
            m_vTimestamp = (o["timestamp"] != null) ? o["timestamp"].ToObject<int>() : 0;
        }
    }
}
