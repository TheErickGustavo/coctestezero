using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Core;
using UCS.Logic;
using UCS.Network;

namespace UCS.PacketProcessing
{
    // X4b: helpers de chat do cla — entrada persistida no mural (pra todos) ou resposta privada
    // (so o cliente que pediu ve; nao entra no mural).
    static class ChatBroadcast
    {
        public static ChatStreamEntry MakeEntry(ClientAvatar avatar, string message)
        {
            ChatStreamEntry cm = new ChatStreamEntry();
            cm.SetId((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
            cm.SetAvatar(avatar);
            cm.SetMessage(message);
            return cm;
        }

        // Entrada no mural: persiste na alianca e manda pra todos os membros online.
        public static void ToAlliance(Alliance alliance, ChatStreamEntry cm)
        {
            if (alliance == null || cm == null)
                return;
            alliance.AddChatMessage(cm);
            long allianceId = alliance.GetAllianceId();
            foreach (var onlinePlayer in ResourcesManager.GetOnlinePlayers())
            {
                if (onlinePlayer.GetPlayerAvatar().GetAllianceId() == allianceId)
                {
                    var p = new AllianceStreamEntryMessage(onlinePlayer.GetClient());
                    p.SetStreamEntry(cm);
                    PacketManager.ProcessOutgoingPacket(p);
                }
            }
        }

        // Mensagem privada: so o cliente que pediu ve; nao persiste.
        public static void SendPrivate(Client client, ClientAvatar avatar, string message)
        {
            if (client == null)
                return;
            ChatStreamEntry cm = MakeEntry(avatar, message);
            var p = new AllianceStreamEntryMessage(client);
            p.SetStreamEntry(cm);
            PacketManager.ProcessOutgoingPacket(p);
        }
    }
}
