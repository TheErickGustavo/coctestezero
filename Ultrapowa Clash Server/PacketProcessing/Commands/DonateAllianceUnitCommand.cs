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
                if (requesterLevel == null)
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

                // Remove a unidade do acampamento do doador (se existir).
                bool removed = false;
                for (int ci = 0; ci < 12 && !removed; ci++)
                {
                    List<GameObject> objs = null;
                    try { objs = level.GameObjectManager.GetGameObjects(ci); }
                    catch { objs = null; }
                    if (objs == null) continue;
                    foreach (var go in objs)
                    {
                        var c = go as Building;
                        if (c == null) continue;
                        if (c.GetBuildingData() == null || c.GetBuildingData().GetGlobalID() != 1000000)
                            continue;
                        var usc = c.GetUnitStorageComponent();
                        if (usc == null) continue;
                        if (usc.GetUnitTypeIndex(unitData) < 0)
                            continue;
                        usc.RemoveUnits(unitData, 1);
                        removed = true;
                        break;
                    }
                }
                if (!removed)
                {
                    Debugger.WriteLine("[UCS] Doacao ignorada: doador sem a unidade " + this.UnitType);
                    return;
                }

                // Adiciona a unidade ao castelo do solicitante.
                ConstructionItem castle = null;
                for (int ci = 0; ci < 12 && castle == null; ci++)
                {
                    List<GameObject> objs = null;
                    try { objs = requesterLevel.GameObjectManager.GetGameObjects(ci); }
                    catch { objs = null; }
                    if (objs == null) continue;
                    foreach (var go in objs)
                    {
                        var c = go as Building;
                        if (c == null) continue;
                        if (c.GetBuildingData() == null || c.GetBuildingData().GetGlobalID() != 1000014)
                            continue;
                        castle = c;
                        break;
                    }
                }
                if (castle == null)
                {
                    Debugger.WriteLine("[UCS] Doacao ignorada: castelo nao encontrado (" + requesterId + ")");
                    return;
                }
                var castleStorage = castle.GetUnitStorageComponent(true);
                if (castleStorage == null || !castleStorage.AddDonatedUnit(unitData))
                {
                    Debugger.WriteLine("[UCS] Doacao ignorada: castelo cheio (" + requesterId + ")");
                    return;
                }

                // Aviso no mural do cla.
                long allianceId = donor.GetAllianceId();
                if (allianceId <= 0)
                    allianceId = requesterLevel.GetPlayerAvatar().GetAllianceId();
                var alliance = (allianceId > 0) ? ObjectManager.GetAlliance(allianceId) : null;
                if (alliance != null)
                {
                    string msg = donor.GetAvatarName() + " doou tropas para " + requesterLevel.GetPlayerAvatar().GetAvatarName();
                    ChatStreamEntry cm = new ChatStreamEntry();
                    cm.SetId((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
                    cm.SetAvatar(donor);
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
                }
                Debugger.WriteLine("[UCS] Doacao: " + donor.GetAvatarName() + " -> " + requesterId + " unidade " + this.UnitType);
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
