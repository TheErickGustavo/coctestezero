using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using UCS.Helpers;
using UCS.Core;
using UCS.Network;
using UCS.Logic;
using UCS.GameFiles;
using Newtonsoft.Json.Linq;

namespace UCS.PacketProcessing
{
    //Packet 14101
    class GoHomeMessage : Message
    {
        public GoHomeMessage(Client client, BinaryReader br) : base(client, br)
        {
        }

        public override void Decode()
        { 
        }

        public override void Process(Level level)
        {
            level.Tick();

            // R-C4: liquida no maximo uma vez o ataque PvP que esta no contexto da sessao.
            // O payload 603 observado (ex.: 277, 691) e tratado provisoriamente como
            // percentual de destruicao em decimos: 500 == 50.0%. Validar esta escala
            // contra captura do protocolo antes de considerar a regra definitiva.
            ProcessPendingPvpBattle(level);

            try
            {
                if (level.LastNpcAttackLevelId == 0x01036640)
                {
                    bool hasCannon = false;
                    foreach (var go in level.GameObjectManager.GetGameObjects(0))
                    {
                        Building b = (Building)go;
                        if (b.GetBuildingData().GetGlobalID() == 1000008)
                        {
                            hasCannon = true;
                            break;
                        }
                    }
                    if (!hasCannon)
                    {
                        BuildingData bd = (BuildingData)ObjectManager.DataTables.GetDataById(1000008);
                        Building b = new Building(bd, level);
                        level.GameObjectManager.AddGameObject(b);
                        JObject j = new JObject();
                        j.Add("data", 1000008);
                        j.Add("lvl", 0);
                        j.Add("x", 17);
                        j.Add("y", 2);
                        b.Load(j);
                        Console.WriteLine("[UCS] Tutorial: canhao 1000008 adicionado (17,2) pos-batalha-1");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] ensure canhao falhou: " + ex.Message);
            }

            //Tutorial: TutorialStepsCount = numero do ultimo passo concluido (golden real: pos-b1=3, pos-b2=10)
            try
            {
                var avatar = level.GetPlayerAvatar();
                if (level.LastNpcAttackLevelId == 0x01036640 && avatar.TutorialStepsCount < 3)
                {
                    avatar.TutorialStepsCount = 3;
                    Console.WriteLine("[UCS] Tutorial: passos=3 (pos-batalha-1)");
                }
                if (level.LastNpcAttackLevelId == 0x01036641 && avatar.TutorialStepsCount < 10)
                {
                    avatar.TutorialStepsCount = 10;
                    Console.WriteLine("[UCS] Tutorial: passos=10 (pos-batalha-2)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] tutorial passos falhou: " + ex.Message);
            }

            Alliance alliance = ObjectManager.GetAlliance(level.GetPlayerAvatar().GetAllianceId());
            //player.GetPlayerAvatar().Clean();
            //R-C5: sincroniza o contador de tropas do avatar com o storage antes de
            //mandar o 24101 (o acampamento mostra o storage, o multiplayer o avatar).
            UnitStorageComponent.SyncAvatarUnitCounts(level);
            //R-T7: mantem as tropas do castelo do cla sincronizadas no avatar.
            AllianceTroopService.SyncAllianceUnits(level);
            PacketManager.ProcessOutgoingPacket(new OwnHomeDataMessage(this.Client, level));
            //X3: historico do mural - reativado de proposito (o real nao manda 24311; ver LoginMessage.cs)
            if (alliance != null)
                PacketManager.ProcessOutgoingPacket(new AllianceStreamMessage(this.Client, alliance));
        }

        private static void AddResource(ClientAvatar ca, string name, int amount)
        {
            if (amount == 0)
                return;
            ResourceData rd = ObjectManager.DataTables.GetResourceByName(name);
            if (rd == null)
                return;
            ca.SetResourceCount(rd, ca.GetResourceCount(rd) + amount);
        }

        private void ProcessPendingPvpBattle(Level attacker)
        {
            if (attacker == null || !attacker.HasPendingPvpBattle ||
                !attacker.TryMarkBattleResultProcessed())
                return;

            long defenderId = attacker.BattleDefenderId;
            bool hasResult = attacker.HasEndOfBattleResult;
            int resultValue = attacker.EndOfBattleValue;
            Level defender = null;

            try
            {
                if (defenderId <= 0 || defenderId == attacker.GetPlayerAvatar().GetId())
                {
                    Console.WriteLine("[UCS] R-C4: contexto PvP invalido; resultado ignorado.");
                    return;
                }

                //R-C4b: sem o comando 603 o cliente NAO entrou em combate (apenas procurou
                //uma vila e voltou). Antes isso aplicava -20/+20 de trofeus a cada
                //"procurar vila -> sair", corrompendo a economia. Agora so liquida
                //quando houve mesmo o 603.
                if (!hasResult)
                {
                    Console.WriteLine("[UCS] R-C4: sem 603 (sem combate) attacker=" +
                        attacker.GetPlayerAvatar().GetId() + " defender=" + defenderId +
                        " -> resultado descartado, trofeus intactos");
                    return;
                }

                try { defender = ResourcesManager.GetPlayer(defenderId, true); }
                catch (Exception loadEx)
                {
                    Console.WriteLine("[UCS] R-C4: nao foi possivel carregar defensor " +
                        defenderId + ": " + loadEx.Message);
                    return;
                }

                if (defender == null || defender.GetPlayerAvatar() == null)
                {
                    Console.WriteLine("[UCS] R-C4: defensor " + defenderId + " nao encontrado.");
                    return;
                }

                //R-C4/P-7: o 603 e o TICK FINAL do relogio do jogo (1/60s) - nao e dano nem
                //percentual. O servidor tem os ticks de colocacao de cada tropa
                // (0x258 PlaceAttacker.Unknown1) e a vila do defensor, entao simula a
                //batalha para saber as estrelas (centro destruido = 1 estrela), a % de
                //destruicao e o loot roubado.
                ClientAvatar attackerAvatar = attacker.GetPlayerAvatar();
                ClientAvatar defenderAvatar = defender.GetPlayerAvatar();

                int endTick = resultValue;
                List<KeyValuePair<CombatItemData, int>> units = attacker.GetBattleUnits();
                BattleSimulation sim = BattleSimulation.Run(attacker, defender, units, endTick);

                double destructionPct = sim.DestructionPct;
                bool attackerWon = sim.Stars >= 1;   //1 estrela = centro da vila destruido

                //CoC 2015: base 20/20 + bonus de destruicao (0.2 por %) e ajuste pela
                //razao de trofeus. Calibrado contra o cliente 7.65 (3 casos exatos):
                //   ratio = trofeus do defensor / trofeus do atacante
                //   ratio^13 -> quem tem menos trofeus perde menos e ganha mais
                int bonus = (int)Math.Round(0.2 * destructionPct);
                int attackerScore = attackerAvatar.GetScore();
                int defenderScore = defenderAvatar.GetScore();
                double ratio = attackerScore > 0 ? (double)defenderScore / attackerScore : 1.0;
                if (ratio <= 0.0)
                    ratio = 1.0;
                double ratioFactor = Math.Pow(ratio, 13.0);
                int delta;
                if (attackerWon)
                    delta = (int)Math.Round((20 + bonus) * ratioFactor);
                else
                    delta = -(int)Math.Round((20 - bonus) / ratioFactor);

                attackerAvatar.SetScore(Math.Max(0, attackerScore + delta));
                defenderAvatar.SetScore(Math.Max(0, defenderScore - delta));

                //R-C4: credita o LOOT roubado na batalha (o cliente 7.65 mostra o loot na
                //tela mas nao manda nada ao servidor - o servidor precisa calcular).
                if (sim.LootGold > 0 || sim.LootElixir > 0 || sim.LootDarkElixir > 0)
                {
                    AddResource(attackerAvatar, "Gold", sim.LootGold);
                    AddResource(attackerAvatar, "Elixir", sim.LootElixir);
                    AddResource(attackerAvatar, "DarkElixir", sim.LootDarkElixir);
                }

                Console.WriteLine("[UCS] R-C4: PvP finalizado attacker=" +
                    attackerAvatar.GetId() + " defender=" + defenderId +
                    " tropas=" + units.Count +
                    " tickFinal=" + endTick +
                    " dano=" + (long)sim.TotalDamage +
                    " predios=" + sim.BuildingsDestroyed + "/" + sim.BuildingsTotal +
                    " destrucao=" + destructionPct.ToString("0.00") + "%" +
                    " estrelas=" + sim.Stars + (sim.TownHallDestroyed ? " (CENTRO DESTRUIDO)" : "") +
                    " loot=" + sim.LootGold + "ouro/" + sim.LootElixir + "elixir" +
                    " outcome=" + (attackerWon ? "win" : "loss") +
                    " delta=" + delta +
                    " scores=" + attackerAvatar.GetScore() + "/" + defenderAvatar.GetScore());

                ObjectManager.SaveBattleChanges(attacker, defender);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C4: falha ao liquidar PvP: " + ex);
            }
            finally
            {
                attacker.ClearPvpBattleContext();
            }
        }
    }
}
