using System;
using System.Collections.Generic;
using UCS.Helpers;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    //Packet 20261 (servidor -> cliente): resultado do bind Google.
    //Candidato 7.65; se o cliente ignorar, testar 24261.
    class GoogleServiceAccountBoundMessage : Message
    {
        private int m_vResultCode;

        public GoogleServiceAccountBoundMessage(Client client) : base(client)
        {
            SetMessageType(24261);
            SetMessageVersion(0);
        }

        public void SetResultCode(int code)
        {
            m_vResultCode = code;
        }

        public override void Encode()
        {
            List<Byte> pack = new List<Byte>();
            pack.AddInt32(m_vResultCode);
            SetData(pack.ToArray());
        }
    }
}
