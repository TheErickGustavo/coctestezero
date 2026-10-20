using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;
//using System.Data.SQLite;
//using System.Data.SQLite.Linq;
using System.Data.Linq;
using System.Data.Entity;
using MySql.Data;
using System.IO;
using System.Configuration;
using Newtonsoft.Json;
using UCS.PacketProcessing;
using UCS.Logic;
using UCS.GameFiles;
using UCS.Network;

namespace UCS.Core

{
    class ObjectManager
    {
        public System.Threading.Timer TimerReference;
        public System.Threading.Timer TickTimerReference;
        public bool m_vTimerCanceled;
        private static readonly object m_vDatabaseLock = new object();
        private static long m_vAvatarSeed;
        private static long m_vAllianceSeed;
        private static DatabaseManager m_vDatabase;
        private static string m_vHomeDefault;
        private static Random m_vRandomSeed;
        private static Dictionary<long, Alliance> m_vAlliances;
        //private static ConcurrentDictionary<long, Level> m_vInMemoryPlayers { get; set; }

        public ObjectManager()
        {
            m_vTimerCanceled = false;
            m_vDatabase = new DatabaseManager();
            NpcLevels = new Dictionary<int, string>();
            DataTables = new DataTables();
            m_vAlliances = new Dictionary<long, Alliance>();

            if (Convert.ToBoolean(ConfigurationManager.AppSettings["useCustomPatch"]))
            {
                LoadFingerPrint();
            }

            using (StreamReader sr = new StreamReader(@"gamefiles/default/home.json"))
            {
                m_vHomeDefault = sr.ReadToEnd();
            }

            m_vAvatarSeed = m_vDatabase.GetMaxPlayerId() + 1;
            m_vAllianceSeed = m_vDatabase.GetMaxAllianceId() + 1;
            LoadGameFiles();
            LoadNpcLevels();

            System.Threading.TimerCallback TimerDelegate = new System.Threading.TimerCallback(Save);
            System.Threading.Timer TimerItem = new System.Threading.Timer(TimerDelegate, null, 60000, 60000);
            TimerReference = TimerItem;

            //R-C5: tick periodico dos levels online (1s). Sem isso a producao de tropas
            //so avançava quando o jogador fazia alguma acao (e chegava atrasada).
            System.Threading.TimerCallback TickDelegate = new System.Threading.TimerCallback(TickOnlineLevels);
            System.Threading.Timer TickTimer = new System.Threading.Timer(TickDelegate, null, 1000, 1000);
            TickTimerReference = TickTimer;

            Console.WriteLine("Database Sync started");
            m_vRandomSeed = new Random();
        }

        //R-C5: avanca o tempo dos jogadores online (producao de tropas, construcoes).
        private static void TickOnlineLevels(object state)
        {
            try
            {
                foreach (Level level in ResourcesManager.GetOnlinePlayers())
                {
                    if (level == null)
                        continue;
                    try { level.Tick(); }
                    catch { }
                }
            }
            catch { }
        }

        public static Alliance CreateAlliance(long seed)
        {
            Alliance alliance;
            lock (m_vDatabaseLock)
            {
                if (seed == 0)
                    seed = m_vAllianceSeed;
                alliance = new Alliance(seed);
                m_vAllianceSeed++;
            }
            m_vDatabase.CreateAlliance(alliance);
            m_vAlliances.Add(alliance.GetAllianceId(), alliance);
            return alliance;
        }

        public static Level CreateAvatar(long seed)
        {
            Level pl;
            lock (m_vDatabaseLock)
            {
                if (seed == 0)
                    seed = m_vAvatarSeed;
                pl = new Level(seed);
                m_vAvatarSeed++;
            }
            pl.LoadFromJSON(m_vHomeDefault);
            m_vDatabase.CreateAccount(pl);
            return pl;
        }

        public static Alliance GetAlliance(long allianceId)
        {
            Alliance alliance = null;
            if (m_vAlliances.ContainsKey(allianceId))
            {
                alliance = m_vAlliances[allianceId];
            }
            else
            {
                alliance = m_vDatabase.GetAlliance(allianceId);
                if (alliance != null)
                {
                    m_vAlliances.Add(alliance.GetAllianceId(), alliance);
                }
            }
            return alliance;
        }

        public static List<Alliance> GetInMemoryAlliances()
        {
            List<Alliance> alliances = new List<Alliance>();
            alliances.AddRange(m_vAlliances.Values);
            return alliances;
        }

        public static Level GetRandomPlayer()
        {
            List<Level> players = ResourcesManager.GetInMemoryLevels();
            if (players.Count == 0)
                return null;
            int index;
            lock (m_vDatabaseLock)
                index = m_vRandomSeed.Next(0, players.Count);
            return players[index];
        }

        // R-C4: seleciona apenas jogadores diferentes do atacante.
        public static Level GetRandomPlayerExcluding(long excludedAvatarId)
        {
            List<Level> candidates = ResourcesManager.GetInMemoryLevels()
                .Where(p => p != null && p.GetPlayerAvatar() != null &&
                            p.GetPlayerAvatar().GetId() != excludedAvatarId)
                .ToList();

            //R-C4b: se o unico adversario estiver offline, ele nao esta em memoria e o
            //"procurar vila" ficava em tela de nuvem infinita (nenhum 24107 era enviado).
            //Fallback: carrega um jogador do banco para servir de oponente.
            if (candidates.Count == 0)
            {
                try
                {
                    long maxId = m_vDatabase.GetMaxPlayerId();
                    for (long id = 1; id <= maxId; id++)
                    {
                        if (id == excludedAvatarId)
                            continue;
                        Level level = ResourcesManager.GetPlayer(id, true);
                        if (level != null && level.GetPlayerAvatar() != null)
                        {
                            candidates.Add(level);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[UCS] R-C4: falha ao buscar oponente no banco: " + ex.Message);
                }
            }

            if (candidates.Count == 0)
                return null;

            int index;
            lock (m_vDatabaseLock)
                index = m_vRandomSeed.Next(0, candidates.Count);
            return candidates[index];
        }

        // Persiste imediatamente os participantes e os clans afetados pelo resultado PvP.
        public static void SaveBattleChanges(Level attacker, Level defender)
        {
            lock (m_vDatabaseLock)
            {
                List<Level> players = new List<Level>();
                if (attacker != null)
                    players.Add(attacker);
                if (defender != null && (attacker == null ||
                    defender.GetPlayerAvatar().GetId() != attacker.GetPlayerAvatar().GetId()))
                    players.Add(defender);
                if (players.Count > 0)
                    m_vDatabase.Save(players);

                List<Alliance> alliances = new List<Alliance>();
                foreach (Level player in players)
                {
                    if (player == null || player.GetPlayerAvatar() == null)
                        continue;
                    long allianceId = player.GetPlayerAvatar().GetAllianceId();
                    if (allianceId == 0)
                        continue;
                    Alliance alliance = GetAlliance(allianceId);
                    if (alliance != null && !alliances.Any(a => a.GetAllianceId() == allianceId))
                    {
                        alliance.RecalculateScore();
                        alliances.Add(alliance);
                    }
                }
                if (alliances.Count > 0)
                    m_vDatabase.Save(alliances);
            }

            //R-C4b: reenvia o 24301 (AllianceData) para os membros online dos clans
            //afetados, para o "Meu Cla" na tela mostrar o score novo sem relogar.
            foreach (Level player in new List<Level> { attacker, defender })
            {
                if (player == null || player.GetPlayerAvatar() == null)
                    continue;
                long allianceId = player.GetPlayerAvatar().GetAllianceId();
                if (allianceId <= 0)
                    continue;
                Alliance alliance = GetAlliance(allianceId);
                if (alliance != null)
                    SendAllianceDataToMembers(alliance);
            }
        }

        //R-C4b: reenvia o 24301 do cla para todos os membros online dele.
        public static void SendAllianceDataToMembers(Alliance alliance)
        {
            if (alliance == null)
                return;
            try
            {
                foreach (Level onlinePlayer in ResourcesManager.GetOnlinePlayers())
                {
                    if (onlinePlayer.GetPlayerAvatar() == null)
                        continue;
                    if (onlinePlayer.GetPlayerAvatar().GetAllianceId() != alliance.GetAllianceId())
                        continue;
                    if (onlinePlayer.GetClient() == null)
                        continue;
                    PacketManager.ProcessOutgoingPacket(
                        new AllianceDataMessage(onlinePlayer.GetClient(), alliance));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C4: falha ao reenviar 24301 do cla: " + ex.Message);
            }
        }

        //R-C4b: recalcula o score do cla (soma dos trofeus dos membros), persiste e
        //atualiza a tela dos membros online. Usado quando um membro entra/sai/e expulso.
        public static void RefreshAllianceScore(long allianceId)
        {
            if (allianceId <= 0)
                return;
            Alliance alliance = GetAlliance(allianceId);
            if (alliance == null)
                return;
            try
            {
                alliance.RecalculateScore();
                lock (m_vDatabaseLock)
                {
                    m_vDatabase.Save(new List<Alliance> { alliance });
                }
                SendAllianceDataToMembers(alliance);
                Console.WriteLine("[UCS] R-C4: score do cla " + allianceId +
                    " recalculado para " + alliance.GetScore());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C4: falha ao recalcular score do cla: " + ex.Message);
            }
        }

        //R-C4b: HP total atacavel de uma vila (muros nao contam para a % de destruicao
        //no CoC 2015). Usado para converter os "damage points" do comando 603 em
        //percentual aproximado de destruicao.
        public static int GetAttachableHpTotal(Level level)
        {
            if (level == null)
                return 0;
            int total = 0;
            try
            {
                foreach (var go in level.GameObjectManager.GetGameObjects(0))
                {
                    Building b = go as Building;
                    if (b == null)
                        continue;
                    BuildingData bd = b.GetBuildingData();
                    if (bd == null)
                        continue;
                    if (!string.IsNullOrEmpty(bd.TID) &&
                        bd.TID.IndexOf("WALL", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue; //muros nao entram na % de destruicao
                    if (bd.Hitpoints == null || bd.Hitpoints.Count == 0)
                        continue;
                    int lvl = b.GetUpgradeLevel();
                    int hp = (lvl >= 0 && lvl < bd.Hitpoints.Count)
                        ? bd.Hitpoints[lvl]
                        : bd.Hitpoints[0];
                    if (hp > 0)
                        total += hp;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C4: erro ao calcular HP atacavel: " + ex.Message);
            }
            return total;
        }

        //R-C6: salva um player imediatamente (usado pelas acoes que mudam recursos,
        //para que nada se perca ao fechar o jogo).
        public static void SavePlayerNow(Level level)
        {
            if (level == null)
                return;
            try
            {
                lock (m_vDatabaseLock)
                {
                    m_vDatabase.Save(new List<Level> { level });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C6: falha ao salvar player: " + ex.Message);
            }
        }

        //R-C6: salva players + clans imediatamente (usado pelo /fixaccounts e por acoes
        //que mudam recursos, para nada se perder ao fechar o jogo).
        public static void SaveAllNow()
        {
            try
            {
                lock (m_vDatabaseLock)
                {
                    m_vDatabase.Save(ResourcesManager.GetInMemoryLevels());
                    m_vDatabase.Save(m_vAlliances.Values.ToList());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C6: falha ao salvar tudo: " + ex.Message);
            }
        }

        public static void LoadFingerPrint()
        {
            FingerPrint = new FingerPrint(@"gamefiles/fingerprint.json");    
        }

        public static void LoadNpcLevels()
        {
            Console.Write("Loading Npc levels... ");
            for(int i=0;i<50;i++)
            {
                using(StreamReader sr = new StreamReader(@"gamefiles/pve/level" + ( i + 1) + ".json"))
                {
                    NpcLevels.Add(i, sr.ReadToEnd());
                }
            }
            Console.WriteLine("done");
            
        }

        public static void LoadGameFiles()
        {
            List<Tuple<string, string, int>> gameFiles = new List<Tuple<string, string, int>>();
            gameFiles.Add(new Tuple<string, string, int>("Achievements", @"gamefiles/logic/achievements.csv", 22));
            gameFiles.Add(new Tuple<string, string, int>("Buildings", @"gamefiles/logic/buildings.csv", 0));
            gameFiles.Add(new Tuple<string, string, int>("Characters", @"gamefiles/logic/characters.csv", 3));
            gameFiles.Add(new Tuple<string, string, int>("Decos", @"gamefiles/logic/decos.csv", 17));
            gameFiles.Add(new Tuple<string, string, int>("Experience Levels", @"gamefiles/logic/experience_levels.csv", 10));
            gameFiles.Add(new Tuple<string, string, int>("Globals", @"gamefiles/logic/globals.csv", 13));
            gameFiles.Add(new Tuple<string, string, int>("Heroes", @"gamefiles/logic/heroes.csv", 27));
            gameFiles.Add(new Tuple<string, string, int>("Leagues", @"gamefiles/logic/leagues.csv", 12));
            gameFiles.Add(new Tuple<string, string, int>("NPCs", @"gamefiles/logic/npcs.csv", 16));
            gameFiles.Add(new Tuple<string, string, int>("Obstacles", @"gamefiles/logic/obstacles.csv", 7));
            gameFiles.Add(new Tuple<string, string, int>("Shields", @"gamefiles/logic/shields.csv", 19));
            gameFiles.Add(new Tuple<string, string, int>("Spells", @"gamefiles/logic/spells.csv", 25));
            gameFiles.Add(new Tuple<string, string, int>("Townhall Levels", @"gamefiles/logic/townhall_levels.csv", 14));
            gameFiles.Add(new Tuple<string, string, int>("Traps", @"gamefiles/logic/traps.csv", 11));
            gameFiles.Add(new Tuple<string, string, int>("Resources", @"gamefiles/logic/resources.csv", 2));

            Console.WriteLine("Loading server data...");
            foreach(var data in gameFiles)
            {
                Console.Write("\t" + data.Item1);
                DataTables.InitDataTable(new CSVTable(data.Item2),data.Item3);
                Console.WriteLine(" done");
            }
        }

        private void Save(object state)
        {
            m_vDatabase.Save(ResourcesManager.GetInMemoryLevels());
            m_vDatabase.Save(m_vAlliances.Values.ToList());
            if (m_vTimerCanceled)
            {
                TimerReference.Dispose();
                if (TickTimerReference != null)
                    TickTimerReference.Dispose();
            }
        }

        //Todo Cleanup
        //Remove disc clients
        //Remove InMemoryPlayers after a certain time

        //public static ConcurrentDictionary<Client, Level> OnlinePlayers { get; set; }
        //public static ConcurrentDictionary<Level, Client> OnlineClients { get; set; }
        //public static ConcurrentDictionary<Socket, Client> Clients { get; set; }
        public static DataTables DataTables { get; set; }
        public static FingerPrint FingerPrint { get; set; }
        public static Dictionary<int,string> NpcLevels {get;set;}
        
    }
}
