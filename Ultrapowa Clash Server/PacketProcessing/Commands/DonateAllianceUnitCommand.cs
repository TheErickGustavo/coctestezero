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
    //Commande 0x04
    class DonateAllianceUnitCommand : Command
    {
        public DonateAllianceUnitCommand(BinaryReader br)
        {
            Unknown1 = br.ReadUInt32WithEndian();
            PlayerId = br.ReadUInt32WithEndian();
            UnitType = br.ReadUInt32WithEndian();
            Unknown2 = br.ReadUInt32WithEndian();
            Unknown3 = br.ReadUInt32WithEndian();
        }

        //00 00 00 04 00 00 00 00 49 79 1C DD 00 3D 09 00 00 00 00 08 00 00 48 7A

        public uint Unknown1 { get; set; } //00 00 00 00
        public uint PlayerId { get; set; } //49 79 1C DD
        public uint UnitType { get; set; } //00 3D 09 00
        public uint Unknown2 { get; set; } //00 00 00 08
        public uint Unknown3 { get; set; } //00 00 48 7A

        public override void Execute(Level level)
        {
            try
            {
                var donor = level.GetPlayerAvatar();
                long requesterId = this.PlayerId;
                if (requesterId <= 0)
                    return;

                var requesterLevel = ResourcesManager.GetPlayer(requesterId, true);
                if (requesterLevel == null || requesterLevel.GetPlayerAvatar() == null)
                {
                    Debugger.WriteLine("[UCS] Doacao ignorada: solicitante nao encontrado (" + requesterId + ")");
                    return;
                }

                var unitData = ObjectManager.DataTables.GetDataById((int)this.UnitType) as CombatItemData;
                if (unitData == null)
                {
                    Debugger.WriteLine("[UCS] Doacao ignorada: unidade desconhecida (" + this.UnitType + ")");
                    return;
                }

                long allianceId = donor.GetAllianceId();
                if (allianceId <= 0)
                    allianceId = requesterLevel.GetPlayerAvatar().GetAllianceId();
                var alliance = (allianceId > 0) ? ObjectManager.GetAlliance(allianceId) : null;
                TroopRequest request = (alliance != null) ? alliance.GetTroopRequest(requesterId) : null;

                //X4b: fluxo unico (respeita lista de tropas pedidas + capacidade do castelo; atualiza contadores)
                string error;
                int delivered = AllianceTroopService.Donate(level, requesterLevel, unitData, 1, request, out error);
                if (delivered <= 0)
                {
                    Debugger.WriteLine("[UCS] Doacao ignorada: " + (error ?? "motivo desconhecido"));
                    return;
                }

                if (alliance != null)
                {
                    string progress = AllianceTroopService.GetCastleProgress(requesterLevel);
                    string msg = donor.GetAvatarName() + " doou " + delivered + "x " + TroopNameParser.GetDisplayName(unitData)
                        + " para " + requesterLevel.GetPlayerAvatar().GetAvatarName() + " (" + progress + ")";
                    ChatBroadcast.ToAlliance(alliance, ChatBroadcast.MakeEntry(donor, msg));
                }
                Debugger.WriteLine("[UCS] Doacao: " + donor.GetAvatarName() + " -> " + requesterId + " " + delivered + "x " + TroopNameParser.GetDisplayName(unitData));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Debugger.WriteLine("[UCS] DonateAllianceUnit falhou: " + ex.Message);
                Console.ResetColor();
            }
        }
    }
}
