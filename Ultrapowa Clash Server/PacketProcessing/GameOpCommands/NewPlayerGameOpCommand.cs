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
    // /newplayer <avatarId> [nome]
    // Cria uma conta nova com a vila padrao (gamefiles/default/home.json), igual ao
    // tutorial do CoC. Serve para testar uma vila "do zero" e para criar as
    // varias contas do servidor.
    class NewPlayerGameOpCommand : GameOpCommand
    {
        private string[] m_vArgs;

        public NewPlayerGameOpCommand(string[] args)
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
            if (m_vArgs.Length < 2)
            {
                Debugger.WriteLine("Uso: /newplayer <avatarId> [nome]");
                return;
            }
            try
            {
                long id = Convert.ToInt64(m_vArgs[1]);
                Level existing = ResourcesManager.GetPlayer(id, true);
                if (existing != null && existing.GetPlayerAvatar() != null)
                {
                    Debugger.WriteLine("[UCS] NewPlayer: conta " + id + " ja existe (" +
                        existing.GetPlayerAvatar().GetAvatarName() + ")");
                    return;
                }
                Level created = ObjectManager.CreateAvatar(id);
                if (created != null && created.GetPlayerAvatar() != null && m_vArgs.Length > 2)
                    created.GetPlayerAvatar().SetName(m_vArgs[2]);
                Debugger.WriteLine("[UCS] NewPlayer: conta " + id + " criada" +
                    (m_vArgs.Length > 2 ? " como " + m_vArgs[2] : ""));
            }
            catch (Exception ex)
            {
                Debugger.WriteLine("[UCS] NewPlayer falhou: " + ex.Message);
            }
        }
    }
}
