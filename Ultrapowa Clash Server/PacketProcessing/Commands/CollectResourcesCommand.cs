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
    //Commande 0x1FA
    class CollectResourcesCommand : Command
    {
        public CollectResourcesCommand(BinaryReader br)
        {
            BuildingId = br.ReadUInt32WithEndian(); //buildingId - 0x1DCD6500;
            Unknown1 = br.ReadUInt32WithEndian();
        }

        public uint BuildingId { get; set; }
        public uint Unknown1 { get; set; }

        public override void Execute(Level level)
        {
            //R-C6: o comando de coleta estava VAZIO -> o cliente mostrava o recurso
            //coletado (simulacao local) mas o servidor nunca creditava, e ao fechar o
            //jogo o recurso sumia.
            try
            {
                if (level == null)
                    return;
                GameObject go = level.GameObjectManager.GetGameObjectByID((int)BuildingId);
                Building b = go as Building;
                if (b == null)
                {
                    Debugger.WriteLine("[UCS] Coleta: predio " + BuildingId + " nao encontrado");
                    return;
                }
                ResourceStorageComponent storage = b.GetResourceStorageComponent(true);
                if (storage == null)
                {
                    Debugger.WriteLine("[UCS] Coleta: predio " + BuildingId + " (data " +
                        b.GetBuildingData().GetGlobalID() + " " + b.GetBuildingData().TID +
                        " lvl" + b.GetUpgradeLevel() + ") sem storage de recurso");
                    return;
                }
                int idx = storage.GetProducedIndex();
                if (idx < 0)
                    return;
                int available = storage.GetCollectable();
                Debugger.WriteLine("[UCS] Coleta: predio " + BuildingId + " (data " +
                    b.GetBuildingData().GetGlobalID() + " " + b.GetBuildingData().TID +
                    " lvl" + b.GetUpgradeLevel() + ") disponivel=" + available +
                    " cap=" + storage.GetProducedCapacity() + " unknown1=" + Unknown1);
                if (available <= 0)
                {
                    Debugger.WriteLine("[UCS] Coleta: nada disponivel no predio " + BuildingId);
                    return;
                }
                var table = ObjectManager.DataTables.GetTable(2);
                ResourceData rd = (ResourceData)table.GetItemAt(idx);
                ClientAvatar ca = level.GetPlayerAvatar();
                ca.SetResourceCount(rd, ca.GetResourceCount(rd) + available);
                storage.Collect();
                Debugger.WriteLine("[UCS] Coleta: " + available + "x " + rd.GetName() +
                    " (total " + ca.GetResourceCount(rd) + ")");
                //R-C6: persiste na hora (o recurso nao pode sumir ao fechar o jogo)
                ObjectManager.SavePlayerNow(level);
            }
            catch (Exception ex)
            {
                Debugger.WriteLine("[UCS] Coleta falhou: " + ex.Message);
            }
        }
    }
}
