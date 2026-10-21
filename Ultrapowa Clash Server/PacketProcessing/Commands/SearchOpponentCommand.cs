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
using UCS.Network;

namespace UCS.PacketProcessing
{
    //Commande 700
    class SearchOpponentCommand : Command
    {
        public SearchOpponentCommand(BinaryReader br)
        {
            br.ReadInt32WithEndian();
            br.ReadInt32WithEndian();
            br.ReadInt32WithEndian();
        }

        //00 00 00 00 00 00 00 00 00 00 00 97

        public override void Execute(Level level)
        {
            if (level == null || level.GetPlayerAvatar() == null)
                return;

            // Exclui explicitamente o próprio atacante; se não houver outro jogador,
            // não envia vila alguma em vez de iniciar uma batalha contra si mesmo.
            var attackerId = level.GetPlayerAvatar().GetId();
            var l = ObjectManager.GetRandomPlayerExcluding(attackerId);
            if (l != null && l.GetPlayerAvatar() != null &&
                l.GetPlayerAvatar().GetId() != attackerId)
            {
                level.BeginPvpBattle(l);
                l.Tick();
                //R-C5: o servidor so entrega a tropa do quartel quando o level e tickeado.
                //Sem isso o cliente (que anima a producao em tempo real) ja mostrava a
                //tropa no acampamento e no multiplayer, mas o servidor mandava menos.
                level.Tick();
                //R-C5: garante que o avatar enviado no 24107 tem o mesmo contador de
                //tropas que o storage (o cliente mostra o avatar no multiplayer e o
                //storage no acampamento; se divergem aparecem numeros diferentes).
                UnitStorageComponent.SyncAvatarUnitCounts(level);
                //R-T7: sincroniza as tropas do castelo do cla (avatar.AllianceUnits) para
                //que aparecam no multiplayer/visita.
                AllianceTroopService.SyncAllianceUnits(level);
                LogTroopState(level);
                var p = new EnemyHomeDataMessage(level.GetClient(), l, level);
                PacketManager.ProcessOutgoingPacket(p);
            }
            else
            {
                level.ClearPvpBattleContext();
                Console.WriteLine("[UCS] R-C4: nenhum oponente PvP elegivel para avatar " + attackerId);
            }
        }

        //R-C5: log de diagnostico das tropas (avatar vs storage vs fila de producao).
        private static void LogTroopState(Level level)
        {
            try
            {
                ClientAvatar ca = level.GetPlayerAvatar();
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("[UCS] R-C5 tropas avatar=");
                foreach (DataSlot ds in ca.GetUnits())
                    sb.Append(ds.Data.GetName() + ":" + ds.Value + " ");
                sb.Append(" | storage=");
                List<Component> comps = level.GetComponentManager().GetComponents(0);
                Dictionary<string, int> store = new Dictionary<string, int>();
                for (int i = 0; i < comps.Count; i++)
                {
                    UnitStorageComponent st = comps[i] as UnitStorageComponent;
                    if (st == null || st.IsSpellForge)
                        continue;
                    for (int s = 0; s < st.GetUnitSlotCount(); s++)
                    {
                        CombatItemData cd = st.GetUnitType(s);
                        if (cd == null)
                            continue;
                        string n = cd.GetName();
                        if (store.ContainsKey(n))
                            store[n] += st.GetUnitCount(s);
                        else
                            store[n] = st.GetUnitCount(s);
                    }
                }
                foreach (KeyValuePair<string, int> kv in store)
                    sb.Append(kv.Key + ":" + kv.Value + " ");
                sb.Append(" | fila=");
                for (int i = 0; i < comps.Count; i++)
                {
                    UnitProductionComponent up = comps[i] as UnitProductionComponent;
                    if (up == null)
                        continue;
                    for (int s = 0; s < up.GetSlotCount(); s++)
                    {
                        CombatItemData cd = up.GetUnit(s) as CombatItemData;
                        if (cd == null)
                            continue;
                        sb.Append(cd.GetName() + ":" + up.GetTrainingCount(s) +
                            "(restam=" + up.GetTotalRemainingSeconds() + "s) ");
                    }
                }
                Console.WriteLine(sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C5: erro no log de tropas: " + ex.Message);
            }
        }
    }
}
