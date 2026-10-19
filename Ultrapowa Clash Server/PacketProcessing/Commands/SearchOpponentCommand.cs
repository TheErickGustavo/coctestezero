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
                var p = new EnemyHomeDataMessage(level.GetClient(), l, level);
                PacketManager.ProcessOutgoingPacket(p);
            }
            else
            {
                level.ClearPvpBattleContext();
                Console.WriteLine("[UCS] R-C4: nenhum oponente PvP elegivel para avatar " + attackerId);
            }
        }
    }
}
