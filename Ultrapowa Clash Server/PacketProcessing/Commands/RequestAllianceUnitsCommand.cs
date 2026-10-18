using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;
using UCS.Network;

namespace UCS.PacketProcessing
{
    //Commande 0x1FF
    class RequestAllianceUnitsCommand : Command
    {
        public RequestAllianceUnitsCommand(BinaryReader br)
        {
            Unknown1 = br.ReadUInt32WithEndian();
            FlagHasRequestMessage = br.ReadByte();
            if (FlagHasRequestMessage == 0x01)
            {
                Message = br.ReadScString();
            }
        }

        //00 00 01 FF 00 00 10 5D 01 00 00 00 21 4A 27 61 69 20 62 65 73 6F 69 6E 20 64 65 20 74 72 6F 75 70 65 73 20 64 65 20 72 65 6E 66 6F 72 74

        public uint Unknown1 { get; set; }
        public byte FlagHasRequestMessage { get; set; } 
        public int MessageLength { get; set; } 
        public string Message { get; set; } 

        public override void Execute(Level level)
        {
            try
            {
                var avatar = level.GetPlayerAvatar();
                long allianceId = avatar.GetAllianceId();
                if (allianceId <= 0)
                    return;
                var alliance = ObjectManager.GetAlliance(allianceId);
                if (alliance == null)
                    return;

                string msg = (FlagHasRequestMessage == 0x01 && !string.IsNullOrEmpty(Message))
                    ? (avatar.GetAvatarName() + " pediu tropas: " + Message)
                    : (avatar.GetAvatarName() + " pediu tropas");

                ChatStreamEntry cm = new ChatStreamEntry();
                cm.SetId((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
                cm.SetAvatar(avatar);
                cm.SetMessage(msg);

                alliance.AddChatMessage(cm);

                foreach (var onlinePlayer in ResourcesManager.GetOnlinePlayers())
                {
                    if (onlinePlayer.GetPlayerAvatar().GetAllianceId() == allianceId)
                    {
                        var p = new AllianceStreamEntryMessage(onlinePlayer.GetClient());
                        p.SetStreamEntry(cm);
                        PacketManager.ProcessOutgoingPacket(p);
                    }
                }
                Debugger.WriteLine("[UCS] Pedido de tropas: " + avatar.GetAvatarName() + " (cla " + allianceId + ")");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Debugger.WriteLine("[UCS] RequestAllianceUnits falhou: " + ex.Message);
                Console.ResetColor();
            }
        }
}
}
