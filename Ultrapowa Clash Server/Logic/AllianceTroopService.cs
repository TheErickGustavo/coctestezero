using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Core;
using UCS.GameFiles;
using UCS.PacketProcessing;

namespace UCS.Logic
{
    // X4b: nucleo compartilhado da doacao de tropas (usado pelo comando 0x04 e pelo /doar do chat).
    static class AllianceTroopService
    {
        public const int ArmyCampBuildingId = 1000006; // Barrack/quartel (1000000 e a CASA!)
        public const int DarkElixirBarrackId = 1000026;
        public const int CastleBuildingId = 1000014;   // castelo de cla (destino)

        //R-T7: herois (Dragao/Golem) nao sao doaveis no CoC 2015.
        private static readonly int[] HeroUnitIds = { 4000049, 4000073, 4000078 };

        public static bool IsHeroUnit(CombatItemData cd)
        {
            if (cd == null)
                return false;
            int id = cd.GetGlobalID();
            for (int i = 0; i < HeroUnitIds.Length; i++)
            {
                if (HeroUnitIds[i] == id)
                    return true;
            }
            return false;
        }

        //R-T7: devolve os storages onde as tropas do jogador ficam (quartéis).
        //Antes o codigo usava ArmyCampBuildingId = 1000000, que e a CASA -> a contagem
        //de "quanto tenho para doar" dava sempre 0 ("voce nao tem X no acampamento").
        private static List<UnitStorageComponent> GetArmyCampStorages(Level level)
        {
            List<UnitStorageComponent> result = new List<UnitStorageComponent>();
            if (level == null)
                return result;
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
                    if (b.GetBuildingData() == null)
                        continue;
                    int gid = b.GetBuildingData().GetGlobalID();
                    if (gid == CastleBuildingId)
                        continue;   //castelo do cla (destino das doacoes)
                    if (gid == 1000022 || gid == 1000025)
                        continue;   //altar do rei/rainha (herois)
                    UnitStorageComponent usc = b.GetUnitStorageComponent();
                    if (usc == null || usc.IsSpellForge)
                        continue;   //feitiaria
                    result.Add(usc);
                }
            }
            return result;
        }

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
            if (IsHeroUnit(cd))
                return 0;
            foreach (UnitStorageComponent usc in GetArmyCampStorages(donor))
                count += usc.GetUnitCountByData(cd);
            return count;
        }

        public static int RemoveFromArmyCamps(Level donor, CombatItemData cd, int count)
        {
            int remaining = count;
            foreach (UnitStorageComponent usc in GetArmyCampStorages(donor))
            {
                if (remaining <= 0)
                    break;
                int have = usc.GetUnitCountByData(cd);
                if (have <= 0)
                    continue;
                int take = Math.Min(have, remaining);
                //R-T7: remove de qualquer nivel (o storage e recarregado com nivel -1, mas
                //tropas treinadas podem ter nivel real) e desconta do avatar.
                usc.RemoveUnitsAnyLevel(cd, take);
                remaining -= take;
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

        //X4b: copia as unidades do castelo (1000014) para a lista alliance_units do avatar.
        //O cliente (7.65) le a bolha do castelo NA VISITA (0/35) desses dados binarios do avatar,
        //nao do JSON da vila. Sem isso, a visita mostra sempre 0/cap.
        public static void SyncAllianceUnits(Level player)
        {
            try
            {
                if (player == null) return;
                ClientAvatar avatar = player.GetPlayerAvatar();
                if (avatar == null) return;
                Building castle;
                UnitStorageComponent storage = GetCastleStorage(player, out castle);
                if (storage == null || castle == null) return;

                avatar.AllianceUnits.Clear();
                for (int i = 0; i < storage.GetUnitSlotCount(); i++)
                {
                    CombatItemData cd = storage.GetUnitType(i);
                    int count = storage.GetUnitCount(i);
                    if (cd != null && count > 0)
                    {
                        //R-T7: id do TIPO da tropa (o cliente 7.65 so reconhece o id do
                        //nivel 1 - o GetGlobalID() do servidor devolve ids deslocados).
                        CharacterData baseType = UnitStorageComponent.GetBaseType(cd);
                        avatar.AllianceUnits.Add(new DataSlot(baseType != null ? (CombatItemData)baseType : cd, count));
                    }
                }
                avatar.SetAllianceCastleLevel(castle.GetUpgradeLevel() + 1); //JSON lvl e 0-based
                avatar.SetAllianceCastleTotalCapacity(storage.GetMaxCapacity());
                avatar.SetAllianceCastleUsedCapacity(storage.GetUsedCapacity());
            }
            catch (Exception ex)
            {
                Debugger.WriteLine("[UCS] SyncAllianceUnits falhou: " + ex.Message);
            }
        }

        // X4c: quantas unidades desse tipo o castelo do solicitante ja tem (acumula slots iguais)
        public static int CountDonatedTypeInCastle(Level requesterLevel, CombatItemData cd)
        {
            Building castle;
            UnitStorageComponent storage = GetCastleStorage(requesterLevel, out castle);
            if (storage == null)
                return 0;
            return storage.GetUnitCountByData(cd);
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
            //    X4c: regra do CoC 2015 - qualquer tropa pode ser doada no espaco VAZIO do castelo.
            //    A restricao "pediu apenas X/Y" so vale quando a tropa NAO cabe no espaco vago.
            //R-T7: herois (Dragao/Golem) nao podem ser doados no CoC 2015.
            if (IsHeroUnit(cd))
            {
                error = TroopNameParser.GetDisplayName(cd) + " e um heroi e nao pode ser doado";
                return 0;
            }
            if (request != null && request.GetWantedUnits().Count > 0 && !request.GetWantedUnits().Contains(cd.GetGlobalID()))
            {
                Building castleChk;
                UnitStorageComponent storageChk = GetCastleStorage(requesterLevel, out castleChk);
                int freeAll = (storageChk != null) ? storageChk.GetMaxCapacity() - storageChk.GetUsedCapacity() : 0;
                if (cd.GetHousingSpace() > freeAll)
                {
                    error = "espaco vago (" + freeAll + ") nao cabe " + TroopNameParser.GetDisplayName(cd) +
                            "; " + request.GetRequesterName() + " pediu apenas: " + TroopNameParser.DescribeWanted(request.GetWantedUnits());
                    return 0;
                }
                //cabe no espaco vago: doacao livre permitida; segue o fluxo normal
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
                int free = capacity - used;
                if (free > 0)
                    error = "espaco vago (" + free + ") nao cabe " + TroopNameParser.GetDisplayName(cd) + " (" + housing + "); castelo " + used + "/" + capacity;
                else
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

            // X4c: se o pedido define quantidade exata p/ essa tropa, clampa por ela
            // (ex: pediu "5 gigantes", ja foi doado 3, entao resta no maximo 2)
            int typeLimit = int.MaxValue;
            if (request != null)
            {
                KeyValuePair<int, int> entry = request.GetWantedCounts().Find(kv => kv.Key == cd.GetGlobalID());
                if (entry.Key == cd.GetGlobalID() && entry.Value > 0)
                {
                    int alreadyGiven = CountDonatedTypeInCastle(requesterLevel, cd);
                    typeLimit = Math.Max(entry.Value - alreadyGiven, 0);
                    if (typeLimit == 0)
                    {
                        error = TroopNameParser.GetDisplayName(cd) + ": o pedido de " + request.GetRequesterName() +
                                " ja esta completo (" + entry.Value + "x)";
                        return 0;
                    }
                }
            }

            int planned = Math.Min(requestedCount <= 0 ? 1 : requestedCount, Math.Min(Math.Min(available, fit), typeLimit));
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
            SyncAllianceUnits(requesterLevel); //X4b: bolha da visita/cartao do castelo leem o avatar
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
