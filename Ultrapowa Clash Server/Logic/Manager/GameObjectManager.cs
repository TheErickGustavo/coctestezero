using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UCS.PacketProcessing;
using UCS.Core;
using UCS.GameFiles;

namespace UCS.Logic
{
    class GameObjectManager
    {
        //R-C6: periodo do respawn global (12h) e seed do jogo.
        public const long RespawnPeriod = 43200;
        public const int RespawnSeed = 0;

        private List<List<GameObject>> m_vGameObjects;
        private List<int> m_vGameObjectsIndex;
        private ComponentManager m_vComponentManager;
        private Level m_vLevel;

        public GameObjectManager(Level l) 
        {
            m_vLevel = l;
            m_vGameObjects = new List<List<GameObject>>();
            m_vGameObjectsIndex = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                m_vGameObjects.Add(new List<GameObject>());
                m_vGameObjectsIndex.Add(0);
            }
            m_vComponentManager = new ComponentManager(m_vLevel);
        }

        public ComponentManager GetComponentManager()
        {
            return m_vComponentManager;
        }

        public GameObject GetGameObjectByID(int id)
        {
            int classId = GlobalID.GetClassID(id) - 500;
            return m_vGameObjects[classId].Find(g => g.GlobalId == id);
        }

        public List<GameObject> GetGameObjects(int id)
        {
            return m_vGameObjects[id];
        }

        public void AddGameObject(GameObject go)
        {
            go.GlobalId = GenerateGameObjectGlobalId(go);
            if (go.ClassId == 0)
            {
                Building b = (Building)go;
                BuildingData bd = b.GetBuildingData();
                if (bd.IsWorkerBuilding())
                {
                    m_vLevel.WorkerManager.IncreaseWorkerCount();
                }
            }
            m_vGameObjects[go.ClassId].Add(go);
        }

        public void RemoveGameObject(GameObject go)
        {
            m_vGameObjects[go.ClassId].Remove(go);
            if(go.ClassId == 0)
            {
                Building b = (Building)go;
                BuildingData bd = b.GetBuildingData();
                if(bd.IsWorkerBuilding())
                {
                    m_vLevel.WorkerManager.DecreaseWorkerCount();
                }
            }
            RemoveGameObjectReferences(go);
        }

        public void RemoveGameObjectReferences(GameObject go)
        {
            m_vComponentManager.RemoveGameObjectReferences(go);
        }

        private int GenerateGameObjectGlobalId(GameObject go)
        {
            int index = m_vGameObjectsIndex[go.ClassId];
            m_vGameObjectsIndex[go.ClassId]++;
            return GlobalID.CreateGlobalID(go.ClassId + 500, index); 
        }

        public void Tick()
        {
            m_vComponentManager.Tick();
            foreach(var l in m_vGameObjects)
            {
                foreach (GameObject go in l)
                    go.Tick();
            }
        }

        public JObject Save()
        {
            JObject jsonData = new JObject();

            //Buildings
            JArray jsonBuildingsArray = new JArray();
            foreach (var go in m_vGameObjects[0])
            {
                Building b = (Building)go;
                JObject jsonObject = new JObject();
                jsonObject.Add("data", b.GetBuildingData().GetGlobalID());
                //R-C5: o jogo 7.65 manda o "id" do objeto (ex.: 500000171). Sem ele o
                //cliente 7.65 nao consegue ligar o heroi ao altar nem achar o castelo do
                //cla (que nao aparecia na tela de ataque) - e os ids que o cliente manda
                //nos comandos (ex.: 0x1DCD654B) sao esses mesmos.
                jsonObject.Add("id", b.GlobalId);
                b.Save(jsonObject);
                //R-T7: o castelo do cla e os altares de heroi recebem, no jogo 7.65, o
                //campo "mode" e as posicoes l1x/l1y, l2x/l2y, l6x/l6y (a posicao do
                //predio em cada nivel/modo). Sem eles o cliente 7.65 nao desenhava o
                //slot do escudo do cla na barra de ataque.
                //Exemplo real (ALL_LEVELS v7.65): {"data":1000014,"id":500000004,
                //"lvl":0,"x":24,"y":19,"mode":0,"l2x":24,"l2y":19,"l6x":24,"l6y":19}
                int bid = b.GetBuildingData().GetGlobalID();
                if (bid == 1000014)   //castelo do cla
                {
                    jsonObject["mode"] = 0;
                }
                if (bid == 1000014 || bid == 1000022 || bid == 1000025) //castelo/altares
                {
                    jsonObject["l1x"] = b.X;
                    jsonObject["l1y"] = b.Y;
                    jsonObject["l2x"] = b.X;
                    jsonObject["l2y"] = b.Y;
                    jsonObject["l6x"] = b.X;
                    jsonObject["l6y"] = b.Y;
                }
                jsonBuildingsArray.Add(jsonObject);
            }
            jsonData.Add("buildings",jsonBuildingsArray);

            //Traps
            JArray jsonTrapsArray = new JArray();
            foreach (var go in m_vGameObjects[4])
            {
                Trap t = (Trap)go;
                JObject jsonObject = new JObject();
                jsonObject.Add("data", t.GetTrapData().GetGlobalID());
                t.Save(jsonObject);
                jsonTrapsArray.Add(jsonObject);
            }
            jsonData.Add("traps",jsonTrapsArray);

            //Decos
            JArray jsonDecosArray = new JArray();
            foreach (var go in m_vGameObjects[6])
            {
                Deco d = (Deco)go;
                JObject jsonObject = new JObject();
                jsonObject.Add("data", d.GetDecoData().GetGlobalID());
                d.Save(jsonObject);
                jsonDecosArray.Add(jsonObject);
            }
            jsonData.Add("decos", jsonDecosArray);

            //Obstacles
            JArray jsonObstaclesArray = new JArray();
            foreach (var go in m_vGameObjects[3])
            {
                Obstacle o = (Obstacle)go;
                JObject jsonObstacle = new JObject();
                jsonObstacle.Add("data", o.GetObstacleData().GetGlobalID());
                o.Save(jsonObstacle);
                jsonObstacle.Add("loot_multiply_ver", 1);
                jsonObstaclesArray.Add(jsonObstacle);
            }
            jsonData.Add("obstacles", jsonObstaclesArray);

            //R-C6: o cliente 7.65 recalcula o acumulado do coletor pelo RELOGIO DA
            //SESSAO DA VILA (nao pelo servidor): ele ignora o "avail" e o "t" que o
            //servidor manda. Com "secondsFromLastRespawn" = 0 o cliente acumula
            //normalmente durante a sessao da vila (e zera ao trocar de vila, como no
            //jogo real). Valores "reais" aqui fazem o cliente mostrar 0 (reset).
            JObject jsonRespawn = new JObject();
            jsonRespawn.Add("secondsFromLastRespawn", 0);
            jsonRespawn.Add("respawnSeed", RespawnSeed);
            jsonRespawn.Add("time_to_gembox_drop", 2147000000);
            jsonRespawn.Add("time_in_gembox_period", 2147000000);
            jsonRespawn.Add("time_to_special_drop", 2147000000);
            jsonRespawn.Add("time_in_special_period", 2147000000);
            jsonRespawn.Add("time_to_special_period", 2147000000);
            jsonData.Add("respawnVars", jsonRespawn);
            jsonData.Add("last_league_rank", 0);
            jsonData.Add("last_league_shuffle", 150);

            return jsonData;
        }

        public void Load(JObject jsonObject)
        {
            JArray jsonBuildings = (JArray)jsonObject["buildings"];
            foreach(JObject jsonBuilding in jsonBuildings)
            {
                BuildingData bd = (BuildingData)ObjectManager.DataTables.GetDataById(jsonBuilding["data"].ToObject<int>());
                Building b = new Building(bd, m_vLevel);
                AddGameObject(b);
                b.Load(jsonBuilding);
                //R-C5: restaura o "id" original do objeto (o jogo real salva o id no
                //JSON). Sem isso o id mudava a cada carga da vila e o cliente perdia a
                //ligacao entre o heroi/castelo e o predio.
                if (jsonBuilding["id"] != null)
                {
                    try
                    {
                        int savedId = jsonBuilding["id"].ToObject<int>();
                        if (savedId > 0)
                        {
                            b.GlobalId = savedId;
                            int idx = savedId - GlobalID.CreateGlobalID(500, 0);
                            if (idx >= m_vGameObjectsIndex[0])
                                m_vGameObjectsIndex[0] = idx + 1;
                        }
                    }
                    catch { }
                }
            }

            JArray jsonTraps = (JArray)jsonObject["traps"];
            foreach (JObject jsonTrap in jsonTraps)
            {
                TrapData td = (TrapData)ObjectManager.DataTables.GetDataById(jsonTrap["data"].ToObject<int>());
                Trap t = new Trap(td, m_vLevel);
                AddGameObject(t);
                t.Load(jsonTrap);
            }

            JArray jsonDecos = (JArray)jsonObject["decos"];
            foreach (JObject jsonDeco in jsonDecos)
            {
                DecoData dd = (DecoData)ObjectManager.DataTables.GetDataById(jsonDeco["data"].ToObject<int>());
                Deco d = new Deco(dd, m_vLevel);
                AddGameObject(d);
                d.Load(jsonDeco);
            }

            JArray jsonObstacles = (JArray)jsonObject["obstacles"];
            if (jsonObstacles == null)
            {
                try
                {
                    if (File.Exists("gamefiles/default/home.json"))
                    {
                        JObject defJson = JObject.Parse(File.ReadAllText("gamefiles/default/home.json"));
                        jsonObstacles = (JArray)defJson["obstacles"];
                        Console.WriteLine("[UCS] Obstaculos: fallback home.json padrao (" + (jsonObstacles != null ? jsonObstacles.Count : 0) + " itens)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[UCS] Obstaculos fallback falhou: " + ex.Message);
                }
            }
            if (jsonObstacles != null)
            {
                foreach (JObject jsonObstacle in jsonObstacles)
                {
                    ObstacleData od = (ObstacleData)ObjectManager.DataTables.GetDataById(jsonObstacle["data"].ToObject<int>());
                    Obstacle o = new Obstacle(od, m_vLevel);
                    AddGameObject(o);
                    o.Load(jsonObstacle);
                }
            }
        }
    }
}
