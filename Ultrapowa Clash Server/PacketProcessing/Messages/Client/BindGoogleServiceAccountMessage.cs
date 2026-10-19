using System;
using System.Collections.Generic;
using System.IO;
using UCS.Core;
using UCS.Helpers;
using UCS.Logic;
using UCS.Network;

namespace UCS.PacketProcessing
{
    //Packet 14262 (cliente -> servidor). Formato 7.65 capturado byte-a-byte:
    //  bool force + string googleServiceId + string accessToken
    //O cliente chama isso no fluxo nativo "Login no Google+"/"Vincular dispositivo".
    class BindGoogleServiceAccountMessage : Message
    {
        private bool m_vForce;
        private string m_vGoogleServiceId;
        private string m_vAccessToken;

        public BindGoogleServiceAccountMessage(Client client, BinaryReader br) : base(client, br)
        {
        }

        public override void Decode()
        {
            using (var br = new BinaryReader(new MemoryStream(GetData())))
            {
                m_vForce = br.ReadByte() != 0;
                m_vGoogleServiceId = br.ReadScString();
                m_vAccessToken = br.ReadScString();
            }
        }

        public override void Process(Level level)
        {
            long currentId = level.GetPlayerAvatar().GetId();
            long boundId = GoogleBindService.GetBoundPlayer(m_vGoogleServiceId);
            long targetId = (boundId != 0 && boundId != currentId) ? boundId : GoogleBindService.GetOtherPlayerId(currentId);

            Debugger.WriteLine("[UCS] BindGoogle: id=" + m_vGoogleServiceId + " force=" + m_vForce +
                " atual=" + currentId + " bound=" + boundId + " target=" + targetId);

            //Confirmacao do popup nativo: o jogador aceitou carregar a outra vila.
            if (m_vForce && targetId != 0 && targetId != currentId)
            {
                GoogleBindService.Bind(m_vGoogleServiceId, targetId);
                GoogleServiceAccountBoundMessage ok = new GoogleServiceAccountBoundMessage(this.Client);
                ok.SetResultCode(0);
                PacketManager.ProcessOutgoingPacket(ok);
                return;
            }

            //Conta Google ja vinculada a outra vila -> popup nativo (nome + id).
            if (targetId != 0 && targetId != currentId)
            {
                Level other = ResourcesManager.GetPlayer(targetId);
                if (other != null)
                {
                    GoogleServiceAccountAlreadyBoundMessage ab = new GoogleServiceAccountAlreadyBoundMessage(this.Client);
                    ab.SetGoogleServiceId(m_vGoogleServiceId);
                    ab.SetAccountId(other.GetPlayerAvatar().GetId());
                    ab.SetPassToken(GoogleBindService.GetPassToken(targetId));
                    ab.SetAvatar(other.GetPlayerAvatar());
                    PacketManager.ProcessOutgoingPacket(ab);
                    return;
                }
            }

            //Primeira vez: grava o vinculo com a vila atual e confirma.
            GoogleBindService.Bind(m_vGoogleServiceId, currentId);
            GoogleServiceAccountBoundMessage bound = new GoogleServiceAccountBoundMessage(this.Client);
            bound.SetResultCode(0);
            PacketManager.ProcessOutgoingPacket(bound);
        }
    }
}
