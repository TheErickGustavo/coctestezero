using System;
using System.IO;
using UCS.Core;
using UCS.Helpers;
using UCS.Logic;
using UCS.Network;

namespace UCS.PacketProcessing
{
    //Packet 14306 (cliente -> servidor): promover/rebaixar membro do cla.
    //Payload capturado: i64 avatarId + i32 role (3 = Anciao, 4 = Colider, 1 = Membro).
    class PromoteAllianceMemberMessage : Message
    {
        private long m_vAvatarId;
        private int m_vRole;

        public PromoteAllianceMemberMessage(Client client, BinaryReader br) : base(client, br)
        {
        }

        public override void Decode()
        {
            using (var br = new BinaryReader(new MemoryStream(GetData())))
            {
                m_vAvatarId = br.ReadInt64WithEndian();
                m_vRole = br.ReadInt32WithEndian();
            }
        }

        public override void Process(Level level)
        {
            Level targetAccount = ResourcesManager.GetPlayer(m_vAvatarId, true);
            if (targetAccount == null)
                return;

            var requesterAvatar = level.GetPlayerAvatar();
            var targetAvatar = targetAccount.GetPlayerAvatar();

            long requesterAllianceId = requesterAvatar.GetAllianceId();
            long targetAllianceId = targetAvatar.GetAllianceId();

            if (requesterAllianceId <= 0 || requesterAllianceId != targetAllianceId)
                return;

            Alliance alliance = ObjectManager.GetAlliance(requesterAllianceId);
            if (alliance == null)
                return;

            var requesterMember = alliance.GetAllianceMember(requesterAvatar.GetId());
            var targetMember = alliance.GetAllianceMember(m_vAvatarId);
            if (requesterMember == null || targetMember == null)
                return;

            //Cargos do cliente NAO sao ordenados por autoridade: 1=membro, 2=chefe, 3=anciao, 4=colider.
            //Tabela de autoridade (maior = manda mais): membro 1, anciao 2, colider 3, chefe 4.
            Func<int, int> authority = r =>
            {
                if (r == 2) return 4; //chefe
                if (r == 4) return 3; //colider
                if (r == 3) return 2; //anciao
                return 1;             //membro (e qualquer valor invalido)
            };

            int requesterAuthority = authority(requesterMember.GetRole());
            int targetAuthority = authority(targetMember.GetRole());
            int newRoleAuthority = authority(m_vRole);

            //So chefe e colider podem promover/rebaixar.
            if (requesterAuthority < 3)
            {
                Debugger.WriteLine("[UCS] Promote: RECUSADO - " + requesterAvatar.GetAvatarName() +
                    " (role " + requesterMember.GetRole() + ") sem autoridade");
                return;
            }

            //Alvo tem que estar abaixo do solicitante.
            if (targetAuthority >= requesterAuthority)
                return;

            //Nao pode promover para o proprio nivel ou acima (chefe so promove ate colider).
            if (newRoleAuthority >= requesterAuthority)
                return;

            //Nao existe "virar chefe" por esta mensagem.
            if (m_vRole != 1 && m_vRole != 3 && m_vRole != 4)
                return;

            targetMember.SetRole(m_vRole);

            Debugger.WriteLine("[UCS] Promote: " + targetAvatar.GetAvatarName() + " -> role " + m_vRole +
                " por " + requesterAvatar.GetAvatarName());
        }
    }
}