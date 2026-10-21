using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;
using UCS.Logic;
using UCS.GameFiles;
using UCS.Core;
using Ionic.Zlib;

namespace UCS.PacketProcessing
{
    //Packet 24107
    class EnemyHomeDataMessage : Message
    {
        private Level m_vOwnerLevel;
        private Level m_vVisitorLevel;

        public EnemyHomeDataMessage(Client client, Level ownerLevel, Level visitorLevel)
            : base(client)
        {
            SetMessageType(24107);

            m_vOwnerLevel = ownerLevel;
            m_vVisitorLevel = visitorLevel;
        }

        public override void Encode()
        {
            List<Byte> data = new List<Byte>();

            //data.AddRange(BitConverter.GetBytes(Player.GetPlayerAvatar().GetSecondsFromLastUpdate()).Reverse());
            data.AddRange(new byte[]{
                0x00, 0x00, 0x00, 0xF0, 
                0xFF, 0xFF, 0xFF, 0xFF, 
                0x54, 0xCE, 0x5C, 0x4A
            });

            ClientHome ch = new ClientHome(m_vOwnerLevel.GetPlayerAvatar().GetId());
            ch.SetShieldDurationSeconds(m_vOwnerLevel.GetPlayerAvatar().RemainingShieldTime);
            ch.SetHomeJSON(m_vOwnerLevel.SaveToJSON());

            data.AddRange(ch.Encode());
            data.AddRange(m_vOwnerLevel.GetPlayerAvatar().Encode());

            //R-C5: log do que o cliente recebe (heroi + tropas do cla) - diagnostico
            //de por que o heroi/castelo do cla nao aparecem na tela de ataque.
            try
            {
                ClientAvatar va = m_vVisitorLevel.GetPlayerAvatar();
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("[UCS] R-C5 24107 atacante " + va.GetId() + ": ");
                for (int hi = 0; hi < 2; hi++)
                {
                    HeroData hd = ObjectManager.DataTables.GetDataById(28000000 + hi) as HeroData;
                    if (hd == null)
                        continue;
                    sb.Append(hd.GetName() + ":hp=" + va.GetHeroHealth(hd) + ",st=" +
                        va.GetHeroState(hd) + ",lvl=" + va.GetUnitUpgradeLevel(hd) + " ");
                }
                sb.Append("| unidades=");
                foreach (var u in va.GetUnits())
                    sb.Append(u.Data.GetName() + ":" + u.Value + " ");
                sb.Append("| claUnits=");
                foreach (var au in va.AllianceUnits)
                    sb.Append(au.Data.GetName() + ":" + au.Value + "(id=" + au.Data.GetGlobalID() + ") ");
                sb.Append("| casteloLvl=" + va.GetAllianceCastleLevel() +
                    " cap=" + va.GetAllianceCastleTotalCapacity() + "/" + va.GetAllianceCastleUsedCapacity());
                Console.WriteLine(sb.ToString());
            }
            catch { }
            data.AddRange(m_vVisitorLevel.GetPlayerAvatar().Encode());

            data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x03, 0x00 });

            SetData(data.ToArray());
        }
    }
}
