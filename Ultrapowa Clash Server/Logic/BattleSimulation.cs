using System;
using System.Collections.Generic;
using System.Linq;
using UCS.GameFiles;
using UCS.Core;

namespace UCS.Logic
{
    //R-C4/P-7: resultado da simulacao de uma batalha PvP.
    //O cliente 7.65 e "client-authoritative": ele calcula a % de destruicao, as estrelas
    //e o loot, mas so manda o TICK final da batalha (comando 0x25B). O servidor entao
    //simula a batalha com os dados que tem (tropas colocadas + ticks + HP dos predios
    //da vila do defensor) para saber se o centro da vila (Town Hall) caiu, quantos
    //prédios foram destruidos e quanto de recurso foi roubado.
    class BattleSimulation
    {
        public int Stars { get; private set; }
        public double DestructionPct { get; private set; }
        public int LootGold { get; private set; }
        public int LootElixir { get; private set; }
        public int LootDarkElixir { get; private set; }
        public bool TownHallDestroyed { get; private set; }
        public int BuildingsDestroyed { get; private set; }
        public int BuildingsTotal { get; private set; }
        public double TotalDamage { get; private set; }

        private class Target
        {
            public Building Building;
            public int Hp;
            public bool CountsForDestruction;
            public bool IsWall;
            public bool IsCollector;
            public string ResourceName;
            public int LootAmount;
        }

        public static BattleSimulation Run(Level attacker, Level defender,
            List<KeyValuePair<CombatItemData, int>> units, int endTick)
        {
            BattleSimulation result = new BattleSimulation();
            if (defender == null || attacker == null)
                return result;

            //1) monta a lista de alvos (predios) da vila do defensor
            List<Target> targets = new List<Target>();
            try
            {
                for (int ci = 0; ci < 12; ci++)
                {
                    List<GameObject> objs = null;
                    try { objs = defender.GameObjectManager.GetGameObjects(ci); }
                    catch { objs = null; }
                    if (objs == null)
                        continue;
                    foreach (GameObject go in objs)
                    {
                        Building b = go as Building;
                        if (b == null)
                            continue;
                        BuildingData bd = b.GetBuildingData();
                        if (bd == null)
                            continue;
                        int lvl = b.GetUpgradeLevel();
                        if (lvl < 0)
                            lvl = 0;
                        int hp = 0;
                        if (bd.Hitpoints != null && lvl < bd.Hitpoints.Count)
                            hp = bd.Hitpoints[lvl];
                        if (hp <= 0)
                            continue;
                        Target t = new Target();
                        t.Building = b;
                        t.Hp = hp;
                        t.IsWall = bd.TID != null && bd.TID.IndexOf("WALL", StringComparison.OrdinalIgnoreCase) >= 0;
                        t.CountsForDestruction = !t.IsWall;
                        t.IsCollector = !String.IsNullOrEmpty(bd.ProducesResource);
                        t.ResourceName = bd.ProducesResource;
                        //conteudo do coletor = 1% do ResourceMax (o loot maximo do coletor)
                        int cap = 0;
                        if (bd.ResourceMax != null && lvl < bd.ResourceMax.Count)
                            cap = bd.ResourceMax[lvl];
                        t.LootAmount = cap / 100;
                        targets.Add(t);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C4: erro ao montar alvos da simulacao: " + ex.Message);
            }

            result.BuildingsTotal = 0;
            foreach (Target t in targets)
            {
                if (t.CountsForDestruction)
                    result.BuildingsTotal++;
            }

            //2) simula: cada tropa causa dano no alvo mais proximo (por posicao) que ainda tenha HP
            double totalDamage = 0;
            ClientAvatar ca = attacker.GetPlayerAvatar();
            foreach (KeyValuePair<CombatItemData, int> unit in units)
            {
                CombatItemData cd = unit.Key;
                if (cd == null)
                    continue;
                int lvl = ca.GetUnitUpgradeLevel(cd);
                CharacterData cdLvl = ObjectManager.DataTables.GetDataById(cd.GetGlobalID() + lvl) as CharacterData;
                if (cdLvl == null)
                    cdLvl = (CharacterData)cd;
                int dmg = cdLvl.Damage;
                int atkMs = cdLvl.AttackSpeed;
                if (atkMs <= 0)
                    atkMs = 1000;
                double ticks = endTick - unit.Value;
                if (ticks < 0)
                    ticks = 0;
                double attacks = (ticks / 60.0) / (atkMs / 1000.0);
                if (attacks <= 0)
                    continue;
                double unitDamage = dmg * attacks;
                totalDamage += unitDamage;

                //distribui o dano nos alvos mais proximos
                double remaining = unitDamage;
                int guard = 0;
                while (remaining > 0.01 && guard < 40)
                {
                    guard++;
                    Target best = null;
                    double bestDist = double.MaxValue;
                    foreach (Target t in targets)
                    {
                        if (t.Hp <= 0)
                            continue;
                        double dx = t.Building.X;
                        double dy = t.Building.Y;
                        double dist = dx * dx + dy * dy;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = t;
                        }
                    }
                    if (best == null)
                        break;
                    if (remaining < best.Hp)
                    {
                        best.Hp -= (int)remaining;
                        remaining = 0;
                    }
                    else
                    {
                        remaining -= best.Hp;
                        best.Hp = 0;
                        result.BuildingsDestroyed++;
                        if (best.Building.GetBuildingData().GetGlobalID() == 1000001)
                        {
                            result.TownHallDestroyed = true;
                        }
                        if (best.IsCollector && best.LootAmount > 0)
                        {
                            if (best.ResourceName == "Gold")
                                result.LootGold += best.LootAmount;
                            else if (best.ResourceName == "DarkElixir")
                                result.LootDarkElixir += best.LootAmount;
                            else
                                result.LootElixir += best.LootAmount;
                        }
                    }
                }
            }

            result.TotalDamage = totalDamage;
            result.DestructionPct = result.BuildingsTotal > 0
                ? (100.0 * result.BuildingsDestroyed / result.BuildingsTotal) : 0.0;

            //estrelas do CoC 2015: 1 = centro (Town Hall) destruido, 2 = 50% de destruicao, 3 = 100%
            result.Stars = 0;
            if (result.TownHallDestroyed)
                result.Stars = 1;
            if (result.DestructionPct >= 50.0)
                result.Stars = Math.Max(result.Stars, 2);
            if (result.DestructionPct >= 100.0)
                result.Stars = 3;

            return result;
        }
    }
}
