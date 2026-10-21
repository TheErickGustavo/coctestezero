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
                //R-C6: o Unknown1 do 0x1FA e o ACUMULADO do coletor em TICKS (1/60s):
                //7249 ticks * (3000/h / 216000) = 100,7 elixir, que era exatamente o
                //que o cliente coletou. O cliente recalcula o proprio acumulado e ignora
                //o "avail" do servidor, entao o Unknown1 e a autoridade do cliente.
                int clientAmount = -1;
                if (Unknown1 > 0)
                {
                    int perHour = storage.GetPerHour();
                    if (perHour > 0)
                    {
                        long amount = (long)Unknown1 * perHour / 216000;
                        int cap = storage.GetProducedCapacity();
                        if (amount > cap) amount = cap;
                        clientAmount = (int)amount;
                    }
                }
                //R-C6: diagnostico completo do coletor (lvl, capacidade, taxa/hora, timer,
                //acumulado, unknown1 e o valor que o cliente mandou).
                Debugger.WriteLine("[UCS] Coleta: predio " + BuildingId + " (data " +
                    b.GetBuildingData().GetGlobalID() + " " + b.GetBuildingData().TID +
                    " lvl" + b.GetUpgradeLevel() + ") | " + storage.Describe() +
                    " | unknown1=" + Unknown1 + " (=>" + clientAmount + ")" +
                    " | servidor=" + available +
                    " | tickServidor=" + (int)level.GetTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds +
                    " | clockServidor=" + DateTime.Now.ToString("HH:mm:ss"));
                if (available <= 0 && clientAmount <= 0)
                {
                    Debugger.WriteLine("[UCS] Coleta: nada disponivel no predio " + BuildingId);
                    return;
                }
                //R-C6/R-C8: credita o valor REAL do servidor (GetCollectable), que e o
                //mesmo que o cliente exibe (ele calcula pelo "res_time" da vila).
                //O Unknown1 do 0x1FA e o TICK do relogio do jogo, nao o tempo acumulado:
                //usar ele fazia o servidor creditar 27 onde o cliente tinha coletado
                //~11.000 (o recurso sumia ao recarregar a vila).
                if (available <= 0 && clientAmount > 0)
                    available = clientAmount;
                var table = ObjectManager.DataTables.GetTable(2);
                ResourceData rd = (ResourceData)table.GetItemAt(idx);
                ClientAvatar ca = level.GetPlayerAvatar();
                ca.SetResourceCount(rd, ca.GetResourceCount(rd) + available);
                storage.Collect();
                Debugger.WriteLine("[UCS] Coleta: " + available + "x " + rd.GetName() +
                    " (total " + ca.GetResourceCount(rd) + ")" +
                    " [servidor=" + available + " clienteUnknown1=" + clientAmount + "]");
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
