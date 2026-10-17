using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using UCS.Helpers;
using UCS.Core;
using UCS.Network;
using UCS.Logic;
using UCS.GameFiles;
using Newtonsoft.Json.Linq;

namespace UCS.PacketProcessing
{
    //Packet 14101
    class GoHomeMessage : Message
    {
        public GoHomeMessage(Client client, BinaryReader br) : base(client, br)
        {
        }

        public override void Decode()
        { 
        }

        public override void Process(Level level)
        {
            level.Tick();

            try
            {
                if (level.LastNpcAttackLevelId == 0x01036640)
                {
                    bool hasCannon = false;
                    foreach (var go in level.GameObjectManager.GetGameObjects(0))
                    {
                        Building b = (Building)go;
                        if (b.GetBuildingData().GetGlobalID() == 1000008)
                        {
                            hasCannon = true;
                            break;
                        }
                    }
                    if (!hasCannon)
                    {
                        BuildingData bd = (BuildingData)ObjectManager.DataTables.GetDataById(1000008);
                        Building b = new Building(bd, level);
                        level.GameObjectManager.AddGameObject(b);
                        JObject j = new JObject();
                        j.Add("data", 1000008);
                        j.Add("lvl", 0);
                        j.Add("x", 17);
                        j.Add("y", 2);
                        b.Load(j);
                        Console.WriteLine("[UCS] Tutorial: canhao 1000008 adicionado (17,2) pos-batalha-1");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] ensure canhao falhou: " + ex.Message);
            }

            //Tutorial: TutorialStepsCount = numero do ultimo passo concluido (golden real: pos-b1=3, pos-b2=10)
            try
            {
                var avatar = level.GetPlayerAvatar();
                if (level.LastNpcAttackLevelId == 0x01036640 && avatar.TutorialStepsCount < 3)
                {
                    avatar.TutorialStepsCount = 3;
                    Console.WriteLine("[UCS] Tutorial: passos=3 (pos-batalha-1)");
                }
                if (level.LastNpcAttackLevelId == 0x01036641 && avatar.TutorialStepsCount < 10)
                {
                    avatar.TutorialStepsCount = 10;
                    Console.WriteLine("[UCS] Tutorial: passos=10 (pos-batalha-2)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] tutorial passos falhou: " + ex.Message);
            }

            Alliance alliance = ObjectManager.GetAlliance(level.GetPlayerAvatar().GetAllianceId());
            //player.GetPlayerAvatar().Clean();
            PacketManager.ProcessOutgoingPacket(new OwnHomeDataMessage(this.Client, level));
            if (alliance != null)
                PacketManager.ProcessOutgoingPacket(new AllianceStreamMessage(this.Client, alliance));
        }
    }
}
