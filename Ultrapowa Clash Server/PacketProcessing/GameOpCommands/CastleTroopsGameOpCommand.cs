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
    // /castelo <avatarId> <tropa> [qtd]
    // Coloca tropas no CASTELO DO CLA de um jogador (o "alliance castle", data 1000014).
    // Necessario porque o cliente 7.65 bloqueia a doacao para o proprio jogador
    // ("voce nao pode doar pra voce mesmo") - assim nao da para encher o proprio
    // castelo pela donation normal. As tropas doadas no castelo do cla sao as que
    // aparecem na tela de ataque.
    class CastleTroopsGameOpCommand : GameOpCommand
    {
        private string[] m_vArgs;

        public CastleTroopsGameOpCommand(string[] args)
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
                SendCommandSuccessMessage(level.GetClient(),
                    "Uso: /castelo <avatarId> <tropa> [qtd]  (ex.: /castelo 1 dragao 5)");
                return;
            }
            try
            {
                long id = Convert.ToInt64(m_vArgs[1]);
                Level target = ResourcesManager.GetPlayer(id, true);
                if (target == null || target.GetPlayerAvatar() == null)
                {
                    SendCommandSuccessMessage(level.GetClient(), "Jogador " + id + " nao encontrado");
                    return;
                }
                CombatItemData cd = TroopNameParser.ResolveUnit(m_vArgs[2]);
                if (cd == null)
                {
                    SendCommandSuccessMessage(level.GetClient(), "Tropa nao reconhecida: " + m_vArgs[2]);
                    return;
                }
                int count = 1;
                if (m_vArgs.Length > 3)
                    int.TryParse(m_vArgs[3], out count);
                if (count <= 0)
                    count = 1;

                int delivered = AllianceTroopService.DeliverToCastle(target, cd, count);
                //R-T7: sincroniza o avatar (alliance_units) para o cliente mostrar as
                //tropas do cla na tela de ataque e salva na hora.
                AllianceTroopService.SyncAllianceUnits(target);
                ObjectManager.SavePlayerNow(target);

                string progress = AllianceTroopService.GetCastleProgress(target);
                SendCommandSuccessMessage(level.GetClient(), "Castelo de " +
                    target.GetPlayerAvatar().GetAvatarName() + ": +" + delivered + "x " +
                    TroopNameParser.GetDisplayName(cd) + " (" + progress + ")");
                Debugger.WriteLine("[UCS] /castelo: " + target.GetPlayerAvatar().GetAvatarName() +
                    " +" + delivered + "x " + cd.GetName() + " (" + progress + ")");
            }
            catch (Exception ex)
            {
                SendCommandSuccessMessage(level.GetClient(), "/castelo falhou: " + ex.Message);
                Debugger.WriteLine("[UCS] /castelo falhou: " + ex.Message);
            }
        }
    }
}
