using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCS.Core;
using UCS.GameFiles;

namespace UCS.Logic
{
    // X4b: resolve nomes de tropas digitados pelo jogador (pt-BR ou ingles, com ou sem acento,
    // singular/plural) para os dados de personagem do jogo. Usado pelo pedido (0x1FF) e pelo
    // comando de chat /doar.
    static class TroopNameParser
    {
        private static readonly object m_vLock = new object();
        private static bool m_vInitialized;
        private static Dictionary<string, int> m_vAliasToUnitId;   // termo normalizado -> id global
        private static Dictionary<int, string> m_vUnitDisplayName; // id global -> nome pt-BR pra exibir

        // { nome no characters.csv, nome pt-BR p/ exibir, apelidos separados por | }
        // Obs: nesse gamefiles os nomes internos sao Gargoyle (Lacaio), Boar Rider (Ginete),
        // Warrior Girl (Valquiria) e Warlock (Bruxa). "Lava Hound" pode nao existir na data — se
        // nao existir, o apelido so nunca casa (sem erro).
        private static readonly string[][] TroopTable = new string[][]
        {
            new[] { "Barbarian",   "Bárbaro",       "barbaro|barbaros|barbarian|barbarians|barb|barbs" },
            new[] { "Archer",      "Arqueira",      "arqueira|arqueiras|arqueiro|arqueiros|archer|archers|arch" },
            new[] { "Goblin",      "Goblin",        "goblin|goblins" },
            new[] { "Giant",       "Gigante",       "gigante|gigantes|giant|giants" },
            new[] { "Wall Breaker","Quebra-Muros",  "quebra muros|quebramuros|quebra muro|wall breaker|wall breakers|wallbreaker|wallbreakers|wb" },
            new[] { "Balloon",     "Balão",         "balao|baloes|balloon|balloons|loon|loons" },
            new[] { "Wizard",      "Mago",          "mago|magos|maga|magas|wizard|wizards" },
            new[] { "Healer",      "Curandeira",    "curandeira|curandeiras|healer|healers" },
            new[] { "Dragon",      "Dragão",        "dragao|dragoes|dragon|dragons" },
            new[] { "PEKKA",       "P.E.K.K.A",     "pekka|pekkas|p e k k a" },
            new[] { "Gargoyle",    "Lacaio",        "lacaio|lacaios|minion|minions|gargoyle|gargoyles" },
            new[] { "Boar Rider",  "Ginete",        "ginete|ginetes|hog rider|hog riders|hog|hogs|boar rider|boar|javali" },
            new[] { "Warrior Girl","Valquíria",     "valquiria|valquirias|valkyrie|valkyries|warrior girl" },
            new[] { "Golem",       "Golem",         "golem|golems" },
            new[] { "Warlock",     "Bruxa",         "bruxa|bruxas|witch|witches|warlock" },
            new[] { "Lava Hound",  "Cão de Lava",   "cao de lava|caes de lava|sabujo de lava|sabujo|lava hound|lava hounds|lava" },
        };

        private static void EnsureInit()
        {
            if (m_vInitialized)
                return;
            lock (m_vLock)
            {
                if (m_vInitialized)
                    return;
                m_vAliasToUnitId = new Dictionary<string, int>();
                m_vUnitDisplayName = new Dictionary<int, string>();
                foreach (string[] row in TroopTable)
                {
                    CharacterData cd = ObjectManager.DataTables.GetCharacterByName(row[0]);
                    if (cd == null)
                        continue; // tropa que nao existe nessa versao do gamefiles
                    int id = cd.GetGlobalID();
                    if (!m_vUnitDisplayName.ContainsKey(id))
                        m_vUnitDisplayName.Add(id, row[1]);
                    AddAlias(Normalize(row[0]), id);
                    foreach (string alias in row[2].Split('|'))
                        AddAlias(Normalize(alias), id);
                }
                m_vInitialized = true;
            }
        }

        private static void AddAlias(string normalized, int id)
        {
            if (string.IsNullOrEmpty(normalized))
                return;
            if (!m_vAliasToUnitId.ContainsKey(normalized))
                m_vAliasToUnitId.Add(normalized, id);
        }

        // minusculas, sem acento, pontuacao vira espaco, espacos colapsados
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            s = s.ToLowerInvariant();
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else
                    sb.Append(' ');
            }
            string flat = sb.ToString().Normalize(NormalizationForm.FormD);
            sb.Clear();
            foreach (char c in flat)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            string[] parts = sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        // Tenta resolver um termo (1..3 palavras) numa tropa. Retorna null se nao for tropa.
        public static CombatItemData ResolveUnit(string term)
        {
            EnsureInit();
            string key = Normalize(term);
            if (string.IsNullOrEmpty(key))
                return null;
            int id;
            if (!m_vAliasToUnitId.TryGetValue(key, out id))
            {
                bool singular = key.EndsWith("s") && m_vAliasToUnitId.TryGetValue(key.Substring(0, key.Length - 1), out id);
                if (!singular)
                {
                    int directId;
                    if (int.TryParse(key, out directId) && m_vUnitDisplayName.ContainsKey(directId))
                        id = directId;
                    else
                        return null;
                }
            }
            return ObjectManager.DataTables.GetDataById(id) as CombatItemData;
        }

        // Varre um texto (ex: mensagem do pedido) e extrai as tropas citadas, sem repetir.
        public static List<int> ExtractUnitIds(string text)
        {
            List<int> result = new List<int>();
            if (string.IsNullOrEmpty(text))
                return result;
            EnsureInit();
            string[] tokens = Normalize(text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            while (i < tokens.Length)
            {
                bool matched = false;
                int maxLen = Math.Min(3, tokens.Length - i);
                for (int len = maxLen; len >= 1 && !matched; len--)
                {
                    string candidate = string.Join(" ", tokens, i, len);
                    int id;
                    if (m_vAliasToUnitId.TryGetValue(candidate, out id))
                    {
                        if (!result.Contains(id))
                            result.Add(id);
                        i += len;
                        matched = true;
                    }
                }
                if (!matched)
                {
                    int directId;
                    if (int.TryParse(tokens[i], out directId) && m_vUnitDisplayName.ContainsKey(directId))
                    {
                        if (!result.Contains(directId))
                            result.Add(directId);
                    }
                    i++;
                }
            }
            return result;
        }

        public static string GetDisplayName(CombatItemData cd)
        {
            EnsureInit();
            if (cd == null)
                return "?";
            string name;
            if (m_vUnitDisplayName.TryGetValue(cd.GetGlobalID(), out name))
                return name;
            return cd.GetName();
        }

        public static string DescribeWanted(List<int> wantedIds)
        {
            EnsureInit();
            if (wantedIds == null || wantedIds.Count == 0)
                return "";
            List<string> names = new List<string>();
            foreach (int id in wantedIds)
            {
                string n;
                if (!m_vUnitDisplayName.TryGetValue(id, out n))
                {
                    CharacterData cd = ObjectManager.DataTables.GetDataById(id) as CharacterData;
                    n = (cd != null) ? cd.GetName() : ("tropa " + id);
                }
                names.Add(n);
            }
            return string.Join(", ", names);
        }
    }
}
