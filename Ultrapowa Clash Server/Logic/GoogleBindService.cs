using System;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;

namespace UCS.Logic
{
    //R-T7 (dev): o login Google do cliente e local/fake ("local-google").
    //Este servico guarda a que jogador a identidade Google esta vinculada e
    //decide qual vila oferecer no popup nativo "conta vinculada a outra vila -> carregar?".
    static class GoogleBindService
    {
        private static ConcurrentDictionary<string, long> m_vBindings = new ConcurrentDictionary<string, long>();
        private static ConcurrentDictionary<long, string> m_vPassTokens = new ConcurrentDictionary<long, string>();
        private static string m_vFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_bind.json");

        static GoogleBindService()
        {
            m_vPassTokens[1] = "local-pass-1";
            m_vPassTokens[2] = "local-pass-2";
            try
            {
                if (File.Exists(m_vFilePath))
                {
                    var loaded = JsonConvert.DeserializeObject<ConcurrentDictionary<string, long>>(File.ReadAllText(m_vFilePath));
                    if (loaded != null)
                        m_vBindings = loaded;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] GoogleBindService: falha ao carregar google_bind.json: " + ex.Message);
            }
        }

        public static long GetBoundPlayer(string googleServiceId)
        {
            long id;
            if (!string.IsNullOrEmpty(googleServiceId) && m_vBindings.TryGetValue(googleServiceId, out id))
                return id;
            return 0;
        }

        public static void Bind(string googleServiceId, long playerId)
        {
            if (string.IsNullOrEmpty(googleServiceId))
                return;
            m_vBindings[googleServiceId] = playerId;
            Save();
        }

        public static string GetPassToken(long playerId)
        {
            string token;
            if (m_vPassTokens.TryGetValue(playerId, out token))
                return token;
            token = "local-pass-" + playerId;
            m_vPassTokens[playerId] = token;
            return token;
        }

        //Suporta N contas: retorna o proximo jogador (por id) diferente do atual.
        public static long GetOtherPlayerId(long currentId)
        {
            try
            {
                var db = new UCS.Core.DatabaseManager();
                long max = db.GetMaxPlayerId();
                for (long i = currentId + 1; i <= max; i++)
                {
                    var lvl = db.GetAccount(i);
                    if (lvl != null)
                        return i;
                }
                for (long i = 1; i < currentId; i++)
                {
                    var lvl = db.GetAccount(i);
                    if (lvl != null)
                        return i;
                }
            }
            catch { }
            //Fallback para 2 contas
            return currentId == 1 ? 2 : 1;
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(m_vFilePath, JsonConvert.SerializeObject(m_vBindings));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] GoogleBindService: falha ao salvar google_bind.json: " + ex.Message);
            }
        }
    }
}
