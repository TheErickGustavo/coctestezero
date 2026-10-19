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
    //Commande 600
    class PlaceAttackerCommand : Command
    {
        public PlaceAttackerCommand(BinaryReader br)
        {
            X = br.ReadInt32WithEndian();
            Y = br.ReadInt32WithEndian();
            Unit = (CharacterData)br.ReadDataReference();
            Unknown1 = br.ReadUInt32WithEndian();
        }

        //00 00 00 03 
        //00 00 02 58 00 00 42 B9 00 00 57 01 00 3D 09 00 00 00 01 55 
        //00 00 02 58 00 00 45 E8 00 00 56 1C 00 3D 09 00 00 00 01 5C 
        //00 00 02 58 00 00 47 01 00 00 54 EB 00 3D 09 00 00 00 01 63

        public int X { get; set; }
        public int Y { get; set; }
        public CharacterData Unit { get; set; }
        public uint Unknown1 { get; set; }

        public override void Execute(Level level)
        {
            //R-C5: log do nivel da tropa colocada (o cliente informa o nivel exato).
            Console.WriteLine("[UCS] R-C5 place: unit=" + (Unit != null ? Unit.GetName() : "?") +
                " unknown1(nivel?)=" + Unknown1 + " x=" + X + " y=" + Y);
            List<Component> components = level.GetComponentManager().GetComponents(0);
            for (int i = 0; i < components.Count; i++)
            {
                UnitStorageComponent c = (UnitStorageComponent)components[i];
                //R-C5: remove de qualquer nivel (o storage e recarregado com nivel -1,
                //mas tropas treinadas podem ter nivel real) e desconta do avatar.
                if (c.IsSpellForge)
                    continue;
                if (c.GetUnitTypeIndex(Unit) != -1)
                {
                    var storageCount = c.GetUnitCountByData(Unit);
                    if (storageCount >= 1)
                    {
                        c.RemoveUnitsAnyLevel(Unit, 1);
                        break;
                    }
                }
            }
        }
    }
}
