using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;
using UCS.Logic;
using UCS.Core;
using UCS.Helpers;

namespace UCS.PacketProcessing
{
    //Packet 24101
    class OwnHomeDataMessage : Message
    {

        private byte[] m_vSerializedVillage { get; set; }

        public OwnHomeDataMessage(Client client, Level level) : base (client)
        {
            SetMessageType(24101);
            this.Player = level;
        }

        public override void Encode()
        {
            byte[] generated = null;
            try
            {
                Player.GetPlayerAvatar().WithAchievements = false;

                List<Byte> data = new List<Byte>();

                ClientHome ch = new ClientHome(Player.GetPlayerAvatar().GetId());
                ch.SetShieldDurationSeconds(Player.GetPlayerAvatar().RemainingShieldTime);
                ch.SetHomeJSON(Player.SaveToJSON());

                //data.AddRange(BitConverter.GetBytes(Player.GetPlayerAvatar().GetSecondsFromLastUpdate()).Reverse());
                data.AddInt32(0);//replace previous after patch
                data.AddInt32(-1);
                data.AddInt32((int)Player.GetTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds); //0x54, 0x47, 0xFD, 0x10 //patch 21/10
                data.AddRange(ch.Encode());
                data.AddRange(Player.GetPlayerAvatar().Encode());

                //7.1
                data.AddInt32(0);
                data.AddInt32(0);

                generated = data.ToArray();
                File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "generated-24101.bin"), generated);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] OwnHomeData: falha na geracao: " + ex);
            }

            string goldenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "golden-24101.bin");
            if (File.Exists(goldenPath))
            {
                SetData(File.ReadAllBytes(goldenPath));
                Console.WriteLine("[UCS] OwnHomeData: GOLDEN INJECTION (" + new FileInfo(goldenPath).Length + " bytes); gerado=" + (generated == null ? "erro" : generated.Length.ToString() + " bytes"));
                return;
            }

            if (generated != null)
            {
                SetData(generated);
            }
        }

        public Level Player { get; set; }
    }
}
