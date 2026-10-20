using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UCS.Core;
using UCS.GameFiles;
using UCS.Helpers;

namespace UCS.Logic
{
    class DataSlot
    {
        public Data Data;
        public int Value;

        public DataSlot(Data d, int value)
        {
            Data = d;
            Value = value;
        }

        public void Decode(BinaryReader br)
        {
            Data = br.ReadDataReference();
            Value = br.ReadInt32WithEndian();
        }

        public byte[] Encode()
        {
            List<Byte> data = new List<Byte>();
            data.AddInt32(DataSlotUtil.GetClientGlobalId(Data));
            data.AddInt32(Value);
            return data.ToArray();
        }

        public JObject Save(JObject jsonObject)
        {
            //R-T7: o id gravado tem de ser o que o CLIENTE 7.65 entende (o id real do
            //tipo: 4000049 = Dragon). O GetGlobalID() do servidor usa um "count" parcial
            //e devolve ids deslocados (4000006), que o cliente ignora.
            jsonObject.Add("global_id", DataSlotUtil.GetClientGlobalId(Data));
            jsonObject.Add("value", Value);
            return jsonObject;
        }

        public void Load(JObject jsonObject)
        {
            Data = ObjectManager.DataTables.GetDataById(jsonObject["global_id"].ToObject<int>());
            Value = jsonObject["value"].ToObject<int>();
        }
    }
}
