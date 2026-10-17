using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Core;
using UCS.Helpers;

namespace UCS.PacketProcessing
{
    //Packet 24133
    class NpcDataMessage : Message
    {
        public NpcDataMessage(Client client, Level level, AttackNpcMessage cnam) : base (client)
        {
            SetMessageType(24133);

            this.Player = level;

            JsonBase = ObjectManager.NpcLevels[(int)cnam.LevelId - 0x01036640];
          
            LevelId = cnam.LevelId;
        }

        public override void Encode()
        {
            string gp = "gamefiles/pve/golden-level-0x" + LevelId.ToString("X8") + ".bin";
            if (System.IO.File.Exists(gp))
            {
                byte[] gb = System.IO.File.ReadAllBytes(gp);
                Console.WriteLine("[UCS] NpcData REPLAY " + gp + " " + gb.Length + "B");
                SetData(gb);
                return;
            }

            List<Byte> data = new List<Byte>();

            byte[] raw = System.Text.Encoding.ASCII.GetBytes(JsonBase);
            Console.WriteLine("[UCS] NpcData sem golden (" + gp + ") -> formato v1 jsonLen=" + raw.Length);

            data.AddInt32(0);
            data.AddInt32(raw.Length);
            data.AddRange(raw);

            data.AddRange(Player.GetPlayerAvatar().Encode());

            data.AddInt32(0);
            data.AddInt32(LevelId);

            SetData(data.ToArray());
        }

        public String JsonBase { get; set; }
        public int LevelId { get; set; }
        public Level Player { get; set; }
    }
}
