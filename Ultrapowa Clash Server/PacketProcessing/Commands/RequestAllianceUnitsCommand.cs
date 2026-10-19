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

                string rawMessage = (FlagHasRequestMessage == 0x01 && !string.IsNullOrEmpty(Message)) ? Message : "";

                //X4b: extrai as tropas citadas na mensagem (ex: "dragao e barbaro" -> [dragao, barbaro])
                List<int> wanted = TroopNameParser.ExtractUnitIds(rawMessage);

                //X4c: quantidades exatas quando citadas ("5 gigantes e 2 magos" -> [(gigante,5),(mago,2)])
                List<KeyValuePair<int, int>> wantedCounts = TroopNameParser.ExtractWantedCounts(rawMessage);

                //capacidade do castelo (housing): predio do jogador; fallback pro avatar
                int capacity = 0, used = 0;
                Building castle;
                UnitStorageComponent storage = AllianceTroopService.GetCastleStorage(level, out castle);
                if (storage != null)
                {
                    capacity = storage.GetMaxCapacity();
                    used = storage.GetUsedCapacity();
                }
                if (capacity <= 0)
                {
                    capacity = avatar.GetAllianceCastleTotalCapacity();
                    used = avatar.GetAllianceCastleUsedCapacity();
                }
                if (capacity <= 0)
                {
                    capacity = 10;
                    used = 0;
                }

                //registra/substitui o pedido pendente do jogador
                TroopRequest request = new TroopRequest();
                request.SetRequesterId(avatar.GetId());
                request.SetRequesterName(avatar.GetAvatarName());
                request.SetMessage(rawMessage);
                request.SetWantedUnits(wanted);
                request.SetWantedCounts(wantedCounts);
                request.SetCapacity(capacity);
                request.SetUsedSpace(used);
                request.SetTimestamp((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
                alliance.AddOrUpdateTroopRequest(request);

                //X4c: anuncio do mural mostra as quantidades quando houver ("pediu 5x Gigante, 2x Mago")
                string countAnnounce = TroopNameParser.DescribeWantedCounts(wantedCounts);
                string wantedDesc = (wanted.Count > 0)
                    ? ((countAnnounce.Length > 0) ? countAnnounce : TroopNameParser.DescribeWanted(wanted))
                    : "";
                string msg = avatar.GetAvatarName() + " pediu tropas (" + used + "/" + capacity + ")" +
                    (wantedDesc.Length > 0 ? ": " + wantedDesc : (rawMessage.Length > 0 ? ": " + rawMessage : ""));
                ChatBroadcast.ToAlliance(alliance, ChatBroadcast.MakeEntry(avatar, msg));

                //X4c: log do servidor mostra qtds quando houver
                string countText = TroopNameParser.DescribeWantedCounts(wantedCounts);
                string wantedText = (wanted.Count > 0) ? TroopNameParser.DescribeWanted(wanted) : "qualquer tropa";
                Debugger.WriteLine("[UCS] Pedido de tropas: " + avatar.GetAvatarName() + " (cla " + allianceId + ") quer: " + wantedText +
                    ((countText.Length > 0) ? " [" + countText + "]" : "") + " (" + used + "/" + capacity + ")");
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
