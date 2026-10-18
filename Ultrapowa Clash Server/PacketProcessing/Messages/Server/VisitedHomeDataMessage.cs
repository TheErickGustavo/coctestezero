using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UCS.Logic;
using UCS.Helpers;

namespace UCS.PacketProcessing
{
    //Packet 24113
    class VisitedHomeDataMessage : Message
    {
        private Level m_vOwnerLevel;
        private Level m_vVisitorLevel;

        public VisitedHomeDataMessage(Client client, Level ownerLevel, Level visitorLevel) : base (client)
        {
            SetMessageType(24113);
            m_vOwnerLevel = ownerLevel;
            m_vVisitorLevel = visitorLevel;
        }

        public override void Encode()
        {
            List<Byte> data = new List<Byte>();

            //data.AddRange(BitConverter.GetBytes(Player.GetPlayerAvatar().GetSecondsFromLastUpdate()).Reverse());
            data.AddInt32(0);//replace previous after patch

            ClientHome ch = new ClientHome(m_vOwnerLevel.GetPlayerAvatar().GetId());
            ch.SetShieldDurationSeconds(m_vOwnerLevel.GetPlayerAvatar().RemainingShieldTime);
            // X2 v2: o JSON da VISITA no servidor real difere do JSON da casa (ver PrepareVisitHomeJSON)
            string visitJson = PrepareVisitHomeJSON(m_vOwnerLevel.SaveToJSON());
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "generated-24113.json"), visitJson);
            ch.SetHomeJSON(visitJson);

            data.AddRange(ch.Encode());
            data.AddRange(m_vOwnerLevel.GetPlayerAvatar().Encode());

            // X2 v2: cauda real (9 capturas conn27) = [avatar do alvo][u8 1][avatar do visitante]
            // (o byte 01 separa os dois avatares: depois vem 00 00 00 00 | i64 id | i64 home | u8 cla | ...)
            data.Add(1);
            data.AddRange(m_vVisitorLevel.GetPlayerAvatar().Encode());

            File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "generated-24113.bin"), data.ToArray());
            SetData(data.ToArray());
        }

        // X2 v2: JSON da VISITA no formato do servidor real (diferencas medidas nas capturas conn27):
        //  - sem as chaves de liga/respawn (last_league_rank, last_league_shuffle, respawnVars)
        //  - coletores (1000002/1000004/1000024) com "res_time" (o real manda; a falta quebra o modo visita)
        //  - castelo do cla (1000014) com os campos de estado do cla
        private static string PrepareVisitHomeJSON(string json)
        {
            JObject root = JObject.Parse(json);
            root.Remove("last_league_rank");
            root.Remove("last_league_shuffle");
            root.Remove("respawnVars");

            JArray buildings = root["buildings"] as JArray;
            if (buildings != null)
            {
                foreach (JObject b in buildings.OfType<JObject>())
                {
                    long dataId = b.Value<long?>("data") ?? 0;
                    switch (dataId)
                    {
                        case 1000002:
                        case 1000004:
                        case 1000024:
                            if (b["res_time"] == null)
                                b["res_time"] = -5999730; // valor observado nas capturas reais
                            break;
                        case 1000014:
                            if (b["locked"] == null) b["locked"] = false;
                            if (b["unit_req_time"] == null) b["unit_req_time"] = 0;
                            if (b["clan_mail_time"] == null) b["clan_mail_time"] = 0;
                            if (b["last_unit_req_time"] == null) b["last_unit_req_time"] = 0;
                            if (b["share_replay_time"] == null) b["share_replay_time"] = 1600;
                            if (b["elder_kick_time"] == null) b["elder_kick_time"] = 1600;
                            break;
                    }
                }
            }

            return root.ToString(Formatting.None);
        }
    }
}
