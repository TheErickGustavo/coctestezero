using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;

namespace UCS.PacketProcessing
{
    //Commande 0x1FC 508
    class TrainUnitCommand : Command
    {
        public TrainUnitCommand(BinaryReader br)
        {
            BuildingId = br.ReadInt32WithEndian(); //buildingId - 0x1DCD6500;
            Unknown1 = br.ReadUInt32WithEndian();
            UnitType = br.ReadInt32WithEndian();
            Count = br.ReadInt32WithEndian();
            Unknown3 = br.ReadUInt32WithEndian();
        }

        public int BuildingId { get; set; }
        public uint Unknown1 { get; set; } //00 00 00 00
        public int UnitType { get; set; } //00 3D 09 03
        public int Count { get; set; } //00 00 00 01
        public uint Unknown3 { get; set; } //FF FF FF FF

        public override void Execute(Level level)
        {
            //Tutorial step9 = train warriors (barbaro 4000000)
            if (UnitType == 4000000)
            {
                ClientAvatar caT = level.GetPlayerAvatar();
                caT.TutorialStepsCount = Math.Max(caT.TutorialStepsCount, 9u);
                Console.WriteLine("[UCS] Tutorial passos=" + caT.TutorialStepsCount + " (train warriors)");
            }

            GameObject go = level.GameObjectManager.GetGameObjectByID(BuildingId);
            if(Count > 0)
            {
                Building b = (Building)go;
                UnitProductionComponent c = b.GetUnitProductionComponent();
                CombatItemData cid = (CombatItemData)ObjectManager.DataTables.GetDataById(UnitType);
                //R-C5: log do nivel pedido pelo cliente (para comparar com o tempo de
                //treino do servidor - se divergem, o servidor entrega a tropa atrasado).
                int upLevel = level.GetPlayerAvatar().GetUnitUpgradeLevel(cid);
                int trainTime = cid.GetTrainingTime(upLevel);
                Console.WriteLine("[UCS] R-C5 treino: unit=" + cid.GetName() +
                    " count=" + Count + " unknown1=" + Unknown1 +
                    " nivelUpgrade=" + upLevel + " trainingTime(servidor)=" + trainTime + "s");
                do
                {
                    if (!c.CanAddUnitToQueue(cid))
                        break;
                    ResourceData trainingResource = cid.GetTrainingResource();
                    ClientAvatar ca = level.GetHomeOwnerAvatar();
                    int trainingCost = cid.GetTrainingCost(ca.GetUnitUpgradeLevel(cid));
                    if (!ca.HasEnoughResources(trainingResource, trainingCost))
                        break;
                    ca.SetResourceCount(trainingResource, ca.GetResourceCount(trainingResource) - trainingCost);
                    c.AddUnitToProductionQueue(cid);
                    Count--;
                }
                while (Count > 0);
            }
        }

    }
}
