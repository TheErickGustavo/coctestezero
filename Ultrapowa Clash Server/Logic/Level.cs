using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UCS.Core;
using UCS.PacketProcessing;
using UCS.GameFiles;

namespace UCS.Logic
{
    class Level
    {

        public GameObjectManager GameObjectManager;//a1 + 44
        public WorkerManager WorkerManager;
        public int LastNpcAttackLevelId { get; set; }

        // R-C4: contexto transitório do ataque PvP. Não é persistido no JSON da vila.
        private readonly object m_vBattleContextLock = new object();
        private bool m_vHasPendingPvpBattle;
        private bool m_vBattleResultProcessed;
        private bool m_vHasEndOfBattleResult;
        private long m_vBattleDefenderId;
        private int m_vBattleDefenderScore;
        private long m_vBattleDefenderAllianceId;
        private int m_vEndOfBattleValue;
        private readonly List<KeyValuePair<CombatItemData, int>> m_vBattleUnits = new List<KeyValuePair<CombatItemData, int>>();

        public bool HasPendingPvpBattle { get { lock (m_vBattleContextLock) return m_vHasPendingPvpBattle; } }
        public bool HasEndOfBattleResult { get { lock (m_vBattleContextLock) return m_vHasEndOfBattleResult; } }
        public int EndOfBattleValue { get { lock (m_vBattleContextLock) return m_vEndOfBattleValue; } }
        public long BattleDefenderId { get { lock (m_vBattleContextLock) return m_vBattleDefenderId; } }
        public int BattleDefenderScore { get { lock (m_vBattleContextLock) return m_vBattleDefenderScore; } }
        public long BattleDefenderAllianceId { get { lock (m_vBattleContextLock) return m_vBattleDefenderAllianceId; } }

        public void BeginPvpBattle(Level defender)
        {
            if (defender == null || defender.GetPlayerAvatar() == null)
                return;

            lock (m_vBattleContextLock)
            {
                m_vHasPendingPvpBattle = true;
                m_vBattleResultProcessed = false;
                m_vHasEndOfBattleResult = false;
                m_vBattleDefenderId = defender.GetPlayerAvatar().GetId();
                m_vBattleDefenderScore = defender.GetPlayerAvatar().GetScore();
                m_vBattleDefenderAllianceId = defender.GetPlayerAvatar().GetAllianceId();
                m_vEndOfBattleValue = 0;
            }
        }

        public void RecordEndOfBattle(int value)
        {
            lock (m_vBattleContextLock)
            {
                if (m_vHasPendingPvpBattle && !m_vBattleResultProcessed)
                {
                    m_vEndOfBattleValue = value;
                    m_vHasEndOfBattleResult = true;
                }
            }
        }

        public bool TryMarkBattleResultProcessed()
        {
            lock (m_vBattleContextLock)
            {
                if (!m_vHasPendingPvpBattle || m_vBattleResultProcessed)
                    return false;
                m_vBattleResultProcessed = true;
                return true;
            }
        }

        public void ClearPvpBattleContext()
        {
            //R-C4: log de diagnostico - mostra QUEM limpou o contexto da batalha
            //(a lista de tropas colocadas sumia e o PvP ficava com 0 tropas).
            try
            {
                System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace();
                string quem = "?";
                for (int i = 1; i < st.FrameCount && i < 6; i++)
                {
                    string m = st.GetFrame(i).GetMethod().Name;
                    if (m != "ClearPvpBattleContext" && m != "Execute" && m != "ProcessPendingPvpBattle")
                    { quem = m; break; }
                }
                Console.WriteLine("[UCS] R-C4: contexto de batalha LIMPO por " + quem +
                    " (tropas registradas = " + m_vBattleUnits.Count + ")");
            }
            catch { }
            lock (m_vBattleContextLock)
            {
                m_vHasPendingPvpBattle = false;
                m_vBattleResultProcessed = false;
                m_vHasEndOfBattleResult = false;
                m_vBattleDefenderId = 0;
                m_vBattleDefenderScore = 0;
                m_vBattleDefenderAllianceId = 0;
                m_vEndOfBattleValue = 0;
                m_vBattleUnits.Clear();
            }
        }

        //R-C4: tropas colocadas na batalha (tipo + tick do colocacao). O Unknown1 do
        //comando 0x258 (PlaceAttacker) e o tick do relogio do JOGO (1/60s). Com isso o
        //servidor consegue estimar o dano causado (e portanto a % de destruicao e o loot).
        public void RecordPlacedUnit(CombatItemData cd, int tick)
        {
            if (cd == null)
                return;
            lock (m_vBattleContextLock)
            {
                m_vBattleUnits.Add(new KeyValuePair<CombatItemData, int>(cd, tick));
            }
        }

        public List<KeyValuePair<CombatItemData, int>> GetBattleUnits()
        {
            lock (m_vBattleContextLock)
            {
                return new List<KeyValuePair<CombatItemData, int>>(m_vBattleUnits);
            }
        }
        private Client m_vClient;
        private ClientAvatar m_vClientAvatar;
        private DateTime m_vTime;//a1 + 40
        private byte m_vAccountPrivileges;
        private byte m_vAccountStatus;
        //MissionManager
        //AchievementManager
        //CooldownManager

        public Level()
        {
            WorkerManager = new WorkerManager();
            GameObjectManager = new GameObjectManager(this);
            m_vClientAvatar = new ClientAvatar();
            m_vAccountPrivileges = 0;
            m_vAccountStatus = 0;
        }

        public Level(long id)
        {
            WorkerManager = new WorkerManager();
            GameObjectManager = new GameObjectManager(this);
            m_vClientAvatar = new ClientAvatar(id);
            m_vTime = DateTime.UtcNow;
            m_vAccountPrivileges = 0;
            m_vAccountStatus = 0;
        }

        public string SaveToJSON()
        {
            return JsonConvert.SerializeObject(GameObjectManager.Save());
        }

        public void LoadFromJSON(string jsonString)
        {
            JObject jsonObject = JObject.Parse(jsonString);
            GameObjectManager.Load(jsonObject);
        }

        public byte GetAccountPrivileges()
        {
            return m_vAccountPrivileges;
        }

        public byte GetAccountStatus()
        {
            return m_vAccountStatus;
        }

        public Client GetClient()
        {
            return m_vClient;
        }

        public ClientAvatar GetHomeOwnerAvatar()
        {
            return this.m_vClientAvatar;
        }

        public ComponentManager GetComponentManager()
        {
            return GameObjectManager.GetComponentManager();
        }

        public ClientAvatar GetPlayerAvatar()
        {
            return this.m_vClientAvatar;
        }

        public DateTime GetTime()
        {
            return m_vTime;
        }

        public bool HasFreeWorkers()
        {
            return WorkerManager.GetFreeWorkers() > 0;
        }

        public void SetAccountStatus(byte status)
        {
            m_vAccountStatus = status;
        }

        public void SetAccountPrivileges(byte privileges)
        {
            m_vAccountPrivileges = privileges;
        }

        public void SetClient(Client client)
        {
            m_vClient = client;
        }

        public void SetHome(string jsonHome)
        {
            GameObjectManager.Load(JObject.Parse(jsonHome));
        }

        public void SetTime(DateTime t)
        {
            m_vTime = t;
        }

        public void Tick()
        {
            SetTime(DateTime.UtcNow);
            GameObjectManager.Tick();
            //LogicMissionManager::tick(*(v1 + 48));
            //LogicAchievementManager::tick(*(v1 + 52));
            //LogicCooldownManager::tick(*(v1 + 68));
        }
    }
}
