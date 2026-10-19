using System;
using System.Collections.Generic;
using UCS.Helpers;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    //Packet 20262 (servidor -> cliente): a conta Google ja esta vinculada a outra vila.
    //O cliente mostra o popup nativo "carregar a outra vila?" com nome + id.
    //Candidato 7.65; se o cliente ignorar, testar 24262.
    class GoogleServiceAccountAlreadyBoundMessage : Message
    {
        private string m_vGoogleServiceId;
        private long m_vAccountId;
        private string m_vPassToken;
        private ClientAvatar m_vAvatar;

        public GoogleServiceAccountAlreadyBoundMessage(Client client) : base(client)
        {
            SetMessageType(24262);
            SetMessageVersion(0);
            m_vGoogleServiceId = "";
            m_vPassToken = "";
        }

        public void SetGoogleServiceId(string value)
        {
            m_vGoogleServiceId = value;
        }

        public void SetAccountId(long value)
        {
            m_vAccountId = value;
        }

        public void SetPassToken(string value)
        {
            m_vPassToken = value;
        }

        public void SetAvatar(ClientAvatar avatar)
        {
            m_vAvatar = avatar;
        }

        public override void Encode()
        {
            List<Byte> pack = new List<Byte>();
            pack.AddString(m_vGoogleServiceId);
            if (m_vAccountId != 0)
            {
                pack.Add(1);
                pack.AddInt64(m_vAccountId);
            }
            else
            {
                pack.Add(0);
            }
            pack.AddString(m_vPassToken);
            if (m_vAvatar != null)
                pack.AddRange(m_vAvatar.Encode());
            SetData(pack.ToArray());
        }
    }
}
