using System;
using UCS.Core;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    // /account <avatarId>  - define para qual conta o login com accountId<=0 cai
    // (o app manda 0 depois de limpar os dados locais).
    class AccountGameOpCommand : GameOpCommand
    {
        private string[] m_vArgs;

        public AccountGameOpCommand(string[] args)
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
                Debugger.WriteLine("Conta padrao atual: " + ObjectManager.DefaultAccountId);
                return;
            }
            try
            {
                ObjectManager.DefaultAccountId = Convert.ToInt64(m_vArgs[1]);
                Debugger.WriteLine("[UCS] Conta padrao (login com id<=0) = " + ObjectManager.DefaultAccountId);
            }
            catch (Exception ex)
            {
                Debugger.WriteLine("[UCS] /account falhou: " + ex.Message);
            }
        }
    }
}
