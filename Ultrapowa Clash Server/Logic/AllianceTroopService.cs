using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Core;
using UCS.GameFiles;

namespace UCS.Logic
{
    // X4b: nucleo compartilhado da doacao de tropas (usado pelo comando 0x04 e pelo /doar do chat).
    static class AllianceTroopService
    {
        public const int ArmyCampBuildingId = 1000000; // acampamento (fonte da tropa)
        public const int CastleBuildingId = 1000014;   // castelo de cla (destino)

        // Acha um predio pelo id de dados (ex: 1000014 = castelo) na vila.
        public static Building FindBuilding(Level level, int dataGlobalId)
        {
            if (level == null)
                return null;
            for (int ci = 0; ci < 12; ci++)
            {
                List<GameObject> objs = null;
                try { objs = level.GameObjectManager.GetGameObjects(ci); }
                catch { objs = null; }
                if (objs == null)
                    continue;
                foreach (GameObject go in objs)
                {
                    Building b = go as Building;
                    if (b == null)
                        continue;
                    if (b.GetBuildingData() != null && b.GetBuildingData().GetGlobalID() == dataGlobalId)
                        return b;
                }
            }
            return null;
        }

        public static UnitStorageComponent GetCastleStorage(Level level, out Building castle)
        {
            castle = FindBuilding(level, CastleBuildingId);
            if (castle == null)
                return null;
            return castle.GetUnitStorageComponent(true);
        }

        public static int CountInArmyCamps(Level donor, CombatItemData cd)
        {
            int count = 0;
            for (int ci = 0; ci < 12; ci++)
            {
                List<GameObject> objs = null;
                try { objs = donor.GameObjectManager.GetGameObjects(ci); }
                catch { objs = null; }
                if (objs == null)
                    continue;
                foreach (GameObject go in objs)
                {
                    Building b = go as Building;
                    if (b == null)
                        continue;
                    if (b.GetBuildingData() == null || b.GetBuildingData().GetGlobalID() != ArmyCampBuildingId)
                        continue;
                    UnitStorageComponent usc = b.GetUnitStorageComponent();
                    if (usc == null)
                        continue;
                    count += usc.GetUnitCountByData(cd);
                }
            }
            return count;
        }

        public static int RemoveFromArmyCamps(Level donor, CombatItemData cd, int count)
        {
            int remaining = count;
            for (int ci = 0; ci < 12 && remaining > 0; ci++)
            {
                List<GameObject> objs = null;
                try { objs = donor.GameObjectManager.GetGameObjects(ci); }
                catch { objs = null; }
                if (objs == null)
                    continue;
                foreach (GameObject go in objs)
                {
                    if (remaining <= 0)
                        break;
                    Building b = go as Building;
                    if (b == null)
                        continue;
                    if (b.GetBuildingData() == null || b.GetBuildingData().GetGlobalID() != ArmyCampBuildingId)
                        continue;
                    UnitStorageComponent usc = b.GetUnitStorageComponent();
                    if (usc == null)
                        continue;
                    int have = usc.GetUnitCountByData(cd);
                    if (have <= 0)
                        continue;
                    int take = Math.Min(have, remaining);
                    usc.RemoveUnits(cd, take);
                    remaining -= take;
                }
            }
            return count - remaining;
        }

        // Entrega no castelo do receptor, respeitando a capacidade local do castelo (housing).
        public static int DeliverToCastle(Level requester, CombatItemData cd, int count)
        {
            Building castle;
            UnitStorageComponent storage = GetCastleStorage(requester, out castle);
            if (storage == null)
                return 0;
            int delivered = 0;
            for (int i = 0; i < count; i++)
            {
                if (!storage.AddDonatedUnit(cd))
                    break;
                delivered++;
            }
            return delivered;
        }

        public static string GetCastleProgress(Level player)
        {
            Building castle;
            UnitStorageComponent storage = GetCastleStorage(player, out castle);
            if (storage == null || castle == null)
                return "0/0";
            return storage.GetUsedCapacity() + "/" + storage.GetMaxCapacity();
        }

        // Fluxo completo: valida o pedido, tira do acampamento do doador, entrega no castelo do
        // solicitante, atualiza contadores e o progresso do pedido. Retorna quantas unidades foram
        // entregues; se 0, 'error' diz o motivo.
        public static int Donate(Level donorLevel, Level requesterLevel, CombatItemData cd, int requestedCount, TroopRequest request, out string error)
        {
            error = null;
            if (donorLevel == null || requesterLevel == null || cd == null)
            {
                error = "dados invalidos";
                return 0;
            }
            ClientAvatar donor = donorLevel.GetPlayerAvatar();
            ClientAvatar requester = requesterLevel.GetPlayerAvatar();
            if (donor == null || requester == null)
            {
                error = "dados invalidos";
                return 0;
            }

            // 1) o solicitante pediu esse tipo de tropa?
            if (request != null && request.GetWantedUnits().Count > 0 && !request.GetWantedUnits().Contains(cd.GetGlobalID()))
            {
                error = request.GetRequesterName() + " pediu apenas: " + TroopNameParser.DescribeWanted(request.GetWantedUnits());
                return 0;
            }

            // 2) espaco no castelo do solicitante
            Building castle;
            UnitStorageComponent storage = GetCastleStorage(requesterLevel, out castle);
            if (storage == null)
            {
                error = "o castelo do cla de " + requester.GetAvatarName() + " nao esta disponivel";
                return 0;
            }
            int capacity = storage.GetMaxCapacity();
            int used = storage.GetUsedCapacity();
            int housing = cd.GetHousingSpace();
            int fit = (housing > 0 && capacity > 0) ? (capacity - used) / housing : 0;
            if (fit <= 0)
            {
                error = "o castelo de " + requester.GetAvatarName() + " esta cheio (" + used + "/" + capacity + ")";
                return 0;
            }

            // 3) estoque do doador
            int available = CountInArmyCamps(donorLevel, cd);
            if (available <= 0)
            {
                error = "voce nao tem " + TroopNameParser.GetDisplayName(cd) + " no acampamento";
                return 0;
            }

            int planned = Math.Min(requestedCount <= 0 ? 1 : requestedCount, Math.Min(available, fit));
            int removed = RemoveFromArmyCamps(donorLevel, cd, planned);
            int delivered = DeliverToCastle(requesterLevel, cd, removed);
            if (delivered <= 0)
            {
                error = "nao deu pra entregar no castelo";
                return 0;
            }

            // 4) contadores + progresso do pedido (contadores = ESPACO de habitacao, como no CoC real)
            int donatedSpace = delivered * housing;
            donor.SetDonatedTroops(donor.GetDonatedTroops() + donatedSpace);
            requester.SetReceivedTroops(requester.GetReceivedTroops() + donatedSpace);
            requester.SetAllianceCastleUsedCapacity(storage.GetUsedCapacity()); //X4b: sincroniza o avatar com o castelo (bubble da visita/limite)
            if (request != null)
            {
                request.SetUsedSpace(used + delivered * housing);
                if (request.IsFull())
                {
                    long allianceId = requester.GetAllianceId();
                    Alliance alliance = (allianceId > 0) ? ObjectManager.GetAlliance(allianceId) : null;
                    if (alliance != null)
                        alliance.RemoveTroopRequest(request.GetRequesterId());
                }
            }
            return delivered;
        }
    }
}
