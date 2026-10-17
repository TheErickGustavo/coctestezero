using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Helpers;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    //Packet 24111 - formato real 7.65 (ver=2, 35B p/ nome de 6 chars)
    class AvatarNameChangeOkMessage : Message
    {
        private string m_vAvatarName;
        private bool m_vTutorialFormat;

        public AvatarNameChangeOkMessage(Client client) : base(client)
        {
            SetMessageType(24111);
            SetMessageVersion(2);
            m_vAvatarName = "";
            m_vTutorialFormat = false;
        }

        public AvatarNameChangeOkMessage(Client client, bool tutorialFormat) : this(client)
        {
            m_vTutorialFormat = tutorialFormat;
        }

        public string GetAvatarName()
        {
            return m_vAvatarName;
        }

        public void SetAvatarName(string name)
        {
            m_vAvatarName = name;
        }

        public override void Encode()
        {
            List<Byte> pack = new List<Byte>();

            if (m_vTutorialFormat)
            {
                pack.AddInt32(3);
                pack.AddString(m_vAvatarName);
                pack.AddInt32(1);
                pack.AddInt32(-1);
            }
            else
            {
                pack.AddInt32(1);
                pack.AddInt32(0);
                pack.AddInt32(12);
                pack.AddString(m_vAvatarName);
                pack.Add(0);
                pack.Add(0xC6);
                pack.Add(0x5D);
                pack.Add(0x46);
                pack.Add(0x02);
                pack.AddInt32(1);
                pack.AddInt32(0);
            }

            SetData(pack.ToArray());
        }
    }
}
