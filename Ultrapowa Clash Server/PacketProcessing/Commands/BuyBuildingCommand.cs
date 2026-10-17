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
    //Commande 0x1F4
    class BuyBuildingCommand : Command
    {
        public BuyBuildingCommand(BinaryReader br)
        {
            X = br.ReadInt32WithEndian();
            Y = br.ReadInt32WithEndian();
            BuildingId = br.ReadInt32WithEndian();
            Unknown1 = br.ReadUInt32WithEndian();
        }

        public int X { get; set; } 
        public int Y { get; set; } 
        public int BuildingId { get; set; } 
        public uint Unknown1 { get; set; } //00 00 2D 7F some client tick

        public override void Execute(Level level)
        {
            ClientAvatar ca = level.GetPlayerAvatar();

            BuildingData bd = (BuildingData)ObjectManager.DataTables.GetDataById(BuildingId);
            if (bd == null)
            {
                Console.WriteLine("[UCS] BuyBuilding id=" + BuildingId + " SEM BuildingData na tabela");
                return;
            }

            foreach (var go in level.GameObjectManager.GetGameObjects(0))
            {
                Building ex = go as Building;
                if (ex == null)
                    continue;
                if (ex.X == X && ex.Y == Y)
                {
                    Console.WriteLine("[UCS] BuyBuilding id=" + BuildingId + " RECUSADO: tile (" + X + "," + Y + ") ja ocupado por id=" + ex.GetBuildingData().GetGlobalID());
                    return;
                }
            }

            int cost = bd.GetBuildCost(0);
            ResourceData rd = bd.GetBuildResource(0);
            bool hasRes = rd != null && ca.HasEnoughResources(rd, cost);
            bool freeWorker = level.HasFreeWorkers();
            bool force = true;

            Console.WriteLine("[UCS] BuyBuilding id=" + BuildingId + " pos=(" + X + "," + Y + ") custo=" + cost +
                " " + (rd != null ? rd.GetName() : "?") + " temRecurso=" + hasRes + " workerLivre=" + freeWorker + " force=" + force);

            if (force || (hasRes && (bd.IsWorkerBuilding() || freeWorker)))
            {
                if (rd != null && hasRes)
                    ca.CommodityCountChangeHelper(0, rd, -cost);

                Building b = new Building(bd, level);
                b.StartConstructing(X, Y);
                level.GameObjectManager.AddGameObject(b);

                if (force && b.IsConstructing())
                    b.SpeedUpConstruction();

                Console.WriteLine("[UCS] BuyBuilding OK id=" + BuildingId + " lvl=" + b.GetUpgradeLevel());

                //Tutorial: mapeia predio->passo (missions.csv: step1 cannon, step4 second builder, step5 elixir pump,
                //step6 elixir storage, step7 gold storage, step8 barrack). Nunca regride.
                switch (BuildingId)
                {
                    case 1000008: ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 1u); break;
                    case 1000015: ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 4u); break;
                    case 1000002: ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 5u); break;
                    case 1000003: ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 6u); break;
                    case 1000005: ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 7u); break;
                    case 1000006: ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 8u); break;
                }
                Console.WriteLine("[UCS] Tutorial passos=" + ca.TutorialStepsCount);
            }
            else
            {
                Console.WriteLine("[UCS] BuyBuilding RECUSADO id=" + BuildingId);
            }
        }
    }
}
