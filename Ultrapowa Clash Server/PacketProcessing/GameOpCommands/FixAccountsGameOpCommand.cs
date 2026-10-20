using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;
using UCS.Network;

namespace UCS.PacketProcessing
{
    // /fixaccounts
    // Repara o estado de TODAS as contas de uma vez (necessario quando ha 20-50 contas):
    //  - contador de tropas do avatar = soma real dos quartéis
    //  - res_prod dos coletores: avail=0 e t = tempo até encher
    //  - score de cada clã = soma dos troféus dos membros
    //  - castelo do clã sincronizado
    class FixAccountsGameOpCommand : GameOpCommand
    {
        private string[] m_vArgs;

        public FixAccountsGameOpCommand(string[] args)
        {
            m_vArgs = args;
            SetRequiredAccountPrivileges(1);
        }

        public override void Execute(Level level)
        {
            if (level.GetAccountPrivileges() < GetRequiredAccountPrivileges())
            {
                SendCommandFailedMessage(level.GetClient());
                return;
            }

            int fixedTroops = 0, fixedCollectors = 0, fixedClans = 0, fixedCastle = 0;

            List<Level> levels = ResourcesManager.GetInMemoryLevels();
            foreach (Level l in levels)
            {
                if (l == null || l.GetPlayerAvatar() == null)
                    continue;

                // 1) contador de tropas do avatar = storage dos quartéis
                UnitStorageComponent.SyncAvatarUnitCounts(l);
                fixedTroops++;

                // 2) coletores: avail = 0 e t = tempo ate encher
                for (int ci = 0; ci < 12; ci++)
                {
                    List<GameObject> objs = null;
                    try { objs = l.GameObjectManager.GetGameObjects(ci); }
                    catch { objs = null; }
                    if (objs == null)
                        continue;
                    foreach (GameObject go in objs)
                    {
                        Building b = go as Building;
                        if (b == null)
                            continue;
                        BuildingData bd = b.GetBuildingData();
                        if (bd == null || String.IsNullOrEmpty(bd.ProducesResource))
                            continue;
                        ResourceStorageComponent rsc = b.GetResourceStorageComponent(true);
                        if (rsc == null)
                            continue;
                        if (rsc.ResetCollectorState())
                            fixedCollectors++;
                    }
                }

                // 3) castelo do clã sincronizado no avatar
                Alliance alliance = ObjectManager.GetAlliance(l.GetPlayerAvatar().GetAllianceId());
                if (alliance != null)
                {
                    AllianceTroopService.SyncAllianceUnits(l);
                    fixedCastle++;
                }
            }

            // 4) score dos clãs = soma dos troféus dos membros
            foreach (Alliance alliance in ObjectManager.GetInMemoryAlliances())
            {
                if (alliance == null)
                    continue;
                alliance.RecalculateScore();
                fixedClans++;
            }

            // 5) persiste tudo
            ObjectManager.SaveAllNow();

            Debugger.WriteLine("[UCS] FixAccounts: " + levels.Count + " players (" + fixedTroops +
                " contadores de tropas, " + fixedCollectors + " coletores, " + fixedCastle +
                " castelos, " + fixedClans + " clans) reparados e salvos.");
        }
    }
}
