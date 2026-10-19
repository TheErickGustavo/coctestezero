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
            PacketManager.ProcessOutgoingPacket(new OwnHomeDataMessage(this.Client, level));
            //X3: historico do mural - reativado de proposito (o real nao manda 24311; ver LoginMessage.cs)
            if (alliance != null)
                PacketManager.ProcessOutgoingPacket(new AllianceStreamMessage(this.Client, alliance));
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

                // Ausencia do comando 603 equivale a desistência. O limiar 500 pressupoe
                // payload em decimos de percentual; stars nao estao presentes no payload
                // de 4 bytes capturado, portanto nao afirmamos detectar estrelas aqui.
                bool attackerLost = !hasResult || resultValue < 500;
                ClientAvatar attackerAvatar = attacker.GetPlayerAvatar();
                ClientAvatar defenderAvatar = defender.GetPlayerAvatar();

                if (attackerLost)
                {
                    attackerAvatar.SetScore(Math.Max(0, attackerAvatar.GetScore() - 20));
                    defenderAvatar.SetScore(defenderAvatar.GetScore() + 20);
                }
                else
                {
                    attackerAvatar.SetScore(attackerAvatar.GetScore() + 20);
                    defenderAvatar.SetScore(Math.Max(0, defenderAvatar.GetScore() - 20));
                }

                Console.WriteLine("[UCS] R-C4: PvP finalizado attacker=" +
                    attackerAvatar.GetId() + " defender=" + defenderId +
                    " endValue=" + (hasResult ? resultValue.ToString() : "missing") +
                    " outcome=" + (attackerLost ? "loss" : "win") +
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
