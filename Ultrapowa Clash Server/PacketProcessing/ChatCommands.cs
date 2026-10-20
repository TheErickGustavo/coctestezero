using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Core;
using UCS.GameFiles;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    // X4b: comandos de chat do cla (linhas que comecam com "/").
    // TryHandle retorna true quando a mensagem foi consumida como comando (nao vai pro mural).
    static class ChatCommands
    {
        public static bool TryHandle(Level level, Client client, string message)
        {
            if (level == null || string.IsNullOrEmpty(message))
                return false;
            string text = message.TrimStart();
            if (text.Length == 0 || text[0] != '/')
                return false;
            ClientAvatar avatar = level.GetPlayerAvatar();
            if (avatar == null)
                return false;
            long allianceId = avatar.GetAllianceId();
            if (allianceId <= 0)
                return false;
            Alliance alliance = ObjectManager.GetAlliance(allianceId);
            if (alliance == null)
                return false;

            string[] parts = text.Substring(1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;
            string cmd = TroopNameParser.Normalize(parts[0]);

            switch (cmd)
            {
                case "doar":
                case "doacao":
                case "donate":
                    HandleDoar(level, client, alliance, parts);
                    return true;
                case "ajuda":
                case "help":
                    ChatBroadcast.SendPrivate(client, avatar, HelpText());
                    return true;
                default:
                    //R-T7: os comandos de admin (/give, /account, /visit, ...) ficam no
                    //GameOpCommandFactory (usados pelo chat GLOBAL). No chat do CLA
                    //eles nao existiam -> "Comando desconhecido". Agora o chat do cla
                    //delega para a mesma fabrica.
                    {
                        object op = GameOpCommandFactory.Parse(text);
                        if (op != null)
                        {
                            Command opCmd = op as Command;
                            if (opCmd != null)
                            {
                                opCmd.Execute(level);
                                return true;
                            }
                        }
                    }
                    ChatBroadcast.SendPrivate(client, avatar, "Comando desconhecido: /" + parts[0] + ". Use /ajuda");
                    return true;
            }
        }

        private static string HelpText()
        {
            return "Comandos do cla: /doar <tropa> [qtd] [jogador] — doa pro pedido mais recente, ou direto pro jogador citado; /ajuda";
        }

        private static void HandleDoar(Level level, Client client, Alliance alliance, string[] parts)
        {
            ClientAvatar avatar = level.GetPlayerAvatar();
            List<string> args = new List<string>();
            for (int i = 1; i < parts.Length; i++)
                args.Add(parts[i]);

            if (args.Count == 0)
            {
                ChatBroadcast.SendPrivate(client, avatar, "Use: /doar <tropa> [quantidade] [jogador]. Ex: /doar barbaro 3 vizinho");
                return;
            }

            // 1) acha a tropa: tenta n-gramas (1..3 palavras) em qualquer posicao da mensagem
            CombatItemData cd = null;
            for (int start = 0; start < args.Count && cd == null; start++)
            {
                for (int len = Math.Min(3, args.Count - start); len >= 1; len--)
                {
                    string candidate = string.Join(" ", args.GetRange(start, len));
                    CombatItemData found = TroopNameParser.ResolveUnit(candidate);
                    if (found != null)
                    {
                        cd = found;
                        args.RemoveRange(start, len);
                        break;
                    }
                }
            }
            if (cd == null)
            {
                ChatBroadcast.SendPrivate(client, avatar, "Nao entendi a tropa. Ex: /doar dragao 2 vizinho");
                return;
            }

            // 2) quantidade opcional (qualquer token numerico)
            int quantity = 1;
            for (int i = 0; i < args.Count; i++)
            {
                int value;
                if (int.TryParse(args[i], out value) && value > 0)
                {
                    quantity = value;
                    args.RemoveAt(i);
                    break;
                }
            }

            // 3) jogador alvo opcional (o que sobrar do texto)
            string targetName = string.Join(" ", args).Trim();

            Level targetLevel = null;
            TroopRequest request = null;
            if (targetName.Length > 0)
            {
                targetLevel = FindMemberLevel(alliance, targetName);
                if (targetLevel == null)
                {
                    ChatBroadcast.SendPrivate(client, avatar, "Jogador nao encontrado no cla: " + targetName);
                    return;
                }
                request = alliance.GetTroopRequest(targetLevel.GetPlayerAvatar().GetId());
            }
            else
            {
                request = alliance.GetLatestTroopRequest(avatar.GetId());
                if (request == null)
                {
                    ChatBroadcast.SendPrivate(client, avatar, "Nao ha pedidos pendentes no cla. Pra entregar direto, use: /doar <tropa> <jogador>");
                    return;
                }
                targetLevel = ResourcesManager.GetPlayer(request.GetRequesterId(), true);
                if (targetLevel == null || targetLevel.GetPlayerAvatar() == null)
                {
                    ChatBroadcast.SendPrivate(client, avatar, "O jogador do pedido nao foi encontrado");
                    return;
                }
            }

            if (targetLevel.GetPlayerAvatar().GetId() == avatar.GetId())
            {
                ChatBroadcast.SendPrivate(client, avatar, "Voce nao pode doar pra voce mesmo");
                return;
            }

            string error;
            int delivered = AllianceTroopService.Donate(level, targetLevel, cd, quantity, request, out error);
            if (delivered <= 0)
            {
                ChatBroadcast.SendPrivate(client, avatar, error ?? "nao deu pra doar");
                return;
            }

            string progress = AllianceTroopService.GetCastleProgress(targetLevel);
            string msg = avatar.GetAvatarName() + " doou " + delivered + "x " + TroopNameParser.GetDisplayName(cd)
                + " para " + targetLevel.GetPlayerAvatar().GetAvatarName() + " (" + progress + ")";
            ChatBroadcast.ToAlliance(alliance, ChatBroadcast.MakeEntry(avatar, msg));
            Debugger.WriteLine("[UCS] /doar: " + avatar.GetAvatarName() + " -> " + targetLevel.GetPlayerAvatar().GetAvatarName()
                + " " + delivered + "x " + TroopNameParser.GetDisplayName(cd) + " (" + progress + ")");
        }

        private static Level FindMemberLevel(Alliance alliance, string name)
        {
            string wanted = TroopNameParser.Normalize(name);
            if (wanted.Length == 0)
                return null;
            Level exact = null;
            Level approx = null;
            foreach (var member in alliance.GetAllianceMembers())
            {
                Level lv = ResourcesManager.GetPlayer(member.GetAvatarId(), true);
                if (lv == null || lv.GetPlayerAvatar() == null)
                    continue;
                string memberName = TroopNameParser.Normalize(lv.GetPlayerAvatar().GetAvatarName());
                if (memberName == wanted)
                {
                    exact = lv;
                    break;
                }
                if (approx == null && memberName.Contains(wanted))
                    approx = lv;
            }
            return exact ?? approx;
        }
    }
}
