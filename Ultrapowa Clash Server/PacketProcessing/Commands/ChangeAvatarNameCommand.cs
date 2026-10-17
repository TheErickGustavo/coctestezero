using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;
using UCS.Network;

namespace UCS.PacketProcessing
{
    //Commande 0x000C (12) - mudanca de nome (tutorial e normal)
    class ChangeAvatarNameCommand : Command
    {
        public ChangeAvatarNameCommand(BinaryReader br)
        {
            int len = br.ReadInt32WithEndian();
            PlayerName = Encoding.UTF8.GetString(br.ReadBytes(len));
            Unknown1 = br.ReadByte();
            Unknown2 = br.ReadUInt32WithEndian();
            Unknown3 = br.ReadUInt32WithEndian();
            Unknown4 = br.ReadUInt32WithEndian();
            Unknown5 = br.ReadUInt32WithEndian();
        }

        public string PlayerName { get; set; }
        public byte Unknown1 { get; set; }
        public uint Unknown2 { get; set; }
        public uint Unknown3 { get; set; }
        public uint Unknown4 { get; set; }
        public uint Unknown5 { get; set; }

        public override void Execute(Level level)
        {
            ClientAvatar ca = level.GetPlayerAvatar();
            ca.SetName(PlayerName);
            ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 11u);
            Console.WriteLine("[UCS] Tutorial passos=" + ca.TutorialStepsCount + " (rename cmd12)");

            var p = new AvatarNameChangeOkMessage(level.GetClient());
            p.SetAvatarName(ca.GetAvatarName());
            PacketManager.ProcessOutgoingPacket(p);
            PacketManager.ProcessOutgoingPacket(new OwnHomeDataMessage(level.GetClient(), level));
        }
    }
}
