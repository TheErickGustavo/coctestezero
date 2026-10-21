using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;
using UCS.Network;

namespace UCS.PacketProcessing
{
    // /give <avatarId> <gemas> [ouro] [elixir] [elixirEscuro]
    // Da recursos/gemas a um jogador (util para teste e para recuperar conta
    // quando o resgate de conquista nao persiste no servidor).
    class GiveGameOpCommand : GameOpCommand
    {
        private string[] m_vArgs;

        public GiveGameOpCommand(string[] args)
        {
            m_vArgs = args;
            SetRequiredAccountPrivileges(1);
        }

        public override void Execute(Level level)
        {
            if (level.GetAccountPrivileges() < GetRequiredAccountPrivileges())
            {
                SendCommandFailedMessage(level.GetClient());
                return;
            }
            if (m_vArgs.Length < 3)
            {
                Debugger.WriteLine("Uso: /give <avatarId> <gemas> [ouro] [elixir] [elixirEscuro]");
                return;
            }
            try
            {
                long id = Convert.ToInt64(m_vArgs[1]);
                Level target = ResourcesManager.GetPlayer(id, true);
                if (target == null || target.GetPlayerAvatar() == null)
                {
                    Debugger.WriteLine("Give failed: id " + id + " not found");
                    return;
                }
                ClientAvatar ca = target.GetPlayerAvatar();

                int gems = Convert.ToInt32(m_vArgs[2]);
                //As gemas existem em DOIS lugares no avatar: o escalar m_vCurrentGems
                //(usado no Encode e no HasEnoughDiamonds) e o slot "Diamonds" dos
                //recursos (mostrado na tela). Tem que atualizar os DOIS.
                ca.SetDiamonds(ca.GetDiamonds() + gems);
                AddResource(ca, "Diamonds", gems);

                if (m_vArgs.Length > 3) AddResource(ca, "Gold", Convert.ToInt32(m_vArgs[3]));
                if (m_vArgs.Length > 4) AddResource(ca, "Elixir", Convert.ToInt32(m_vArgs[4]));
                if (m_vArgs.Length > 5) AddResource(ca, "DarkElixir", Convert.ToInt32(m_vArgs[5]));

                SendCommandSuccessMessage(level.GetClient(), "Dadas " + gems + " gemas para " + ca.GetAvatarName() +
                    " (total " + ca.GetDiamonds() + ")");
                Debugger.WriteLine("Give: " + ca.GetAvatarName() + " (id " + id + ") +" + gems + " gemas, " +
                    "ouro=" + ca.GetResourceCount(ObjectManager.DataTables.GetResourceByName("Gold")) +
                    ", elixir=" + ca.GetResourceCount(ObjectManager.DataTables.GetResourceByName("Elixir")) +
                    ", elixirEscuro=" + ca.GetResourceCount(ObjectManager.DataTables.GetResourceByName("DarkElixir")));

                //R-C6: persiste na hora
                ObjectManager.SavePlayerNow(target);
            }
            catch (Exception ex)
            {
                Debugger.WriteLine("Give failed with error: " + ex);
            }
        }

        private static void AddResource(ClientAvatar ca, string name, int amount)
        {
            if (amount == 0)
                return;
            ResourceData rd = ObjectManager.DataTables.GetResourceByName(name);
            if (rd == null)
                return;
            ca.SetResourceCount(rd, ca.GetResourceCount(rd) + amount);
        }
    }
}
