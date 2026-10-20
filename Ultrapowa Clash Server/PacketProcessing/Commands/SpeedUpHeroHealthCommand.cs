using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;

namespace UCS.PacketProcessing
{
    //Commande 0x0212
    //R-C5: reviver o heroi (Rei/Rainha) gastando gemas.
    //ANTES este comando NAO TINHA Execute(): o servidor ignorava o pedido, o
    //cliente abatia as gemas so na tela (HUD local) e, ao recarregar a vila, a
    //vida do heroi e o saldo de gemas voltavam ao valor antigo. Era a mesma
    //categoria de bug do coletor/tropas: "o cliente aplica, o servidor nao sabe".
    //
    //Payload: 00 00 02 12 | buildingId (GlobalId do altar, ex.: 0x1DCD654B) | custo
    //O segundo int e o CUSTO em gemas que o proprio cliente calculou (o jogo usa
    //HERO_HEALTH_SPEED_UP_COST_MULTIPLIER = 300). Usamos esse valor para cobrar
    //exatamente o que o botao do cliente prometeu.
    class SpeedUpHeroHealthCommand : Command
    {
        private int m_vBuildingId;
        private int m_vCost;

        public SpeedUpHeroHealthCommand(BinaryReader br)
        {
            m_vBuildingId = br.ReadInt32WithEndian();
            m_vCost = br.ReadInt32WithEndian();
        }

        public override void Execute(Level level)
        {
            ClientAvatar ca = level.GetPlayerAvatar();
            GameObject go = level.GameObjectManager.GetGameObjectByID(m_vBuildingId);
            Building b = go as Building;
            if (b == null || b.GetBuildingData() == null)
            {
                Debugger.WriteLine("[UCS] R-C5: SpeedUpHeroHealth - altar nao encontrado (" + m_vBuildingId + ")");
                return;
            }

            HeroBaseComponent hbc = b.GetHeroBaseComponent();
            if (hbc == null)
            {
                Debugger.WriteLine("[UCS] R-C5: SpeedUpHeroHealth - predio " + b.GetBuildingData().GetGlobalID() +
                    " nao e um altar de heroi");
                return;
            }

            HeroData hd = ObjectManager.DataTables.GetHeroByName(b.GetBuildingData().HeroType);
            if (hd == null)
            {
                Debugger.WriteLine("[UCS] R-C5: SpeedUpHeroHealth - heroi desconhecido (" +
                    b.GetBuildingData().HeroType + ")");
                return;
            }

            //custo informado pelo cliente (gemas). Protecoes: nunca 0 (gratis) e
            //nunca absurdo (o cliente mandaria 1..~5000 para reviver um heroi).
            int cost = m_vCost;
            if (cost < 1)
                cost = 1;
            if (cost > 5000)
                cost = 5000;

            //R-C5: o cliente 7.65 REENVIA este comando sozinho toda vez que a tela de
            //ataque e montada (ele acha que o heroi continua ferido). Sem esta
            //checagem o servidor cobrava as gemas varias vezes (21 vezes num teste,
            //mais de 10.000 gemas). Se o heroi ja esta com a vida cheia e disponivel,
            //o comando e um no-op: nao cobra nada.
            int curLvl = ca.GetUnitUpgradeLevel(hd);
            int maxHp = 0;
            if (hd.Hitpoints != null && hd.Hitpoints.Count > 0)
            {
                int hidx = (curLvl > 0 && curLvl < hd.Hitpoints.Count) ? curLvl : 0;
                maxHp = hd.Hitpoints[hidx];
            }
            if (maxHp > 0 && ca.GetHeroHealth(hd) >= maxHp && ca.GetHeroState(hd) == 2)
            {
                Debugger.WriteLine("[UCS] R-C5: heroi " + hd.GetName() +
                    " ja esta vivo e disponivel - revive ignorado (sem custo)");
                return;
            }

            if (!ca.HasEnoughDiamonds(cost))
            {
                Debugger.WriteLine("[UCS] R-C5: SpeedUpHeroHealth - gemas insuficientes (" +
                    ca.GetDiamonds() + " < " + cost + ")");
                return;
            }
            ca.UseDiamonds(cost);

            //vida cheia do nivel atual e heroi "no altar" (disponivel para atacar)
            int lvl = ca.GetUnitUpgradeLevel(hd);
            int hp = 0;
            if (hd.Hitpoints != null && hd.Hitpoints.Count > 0)
            {
                int idx = (lvl > 0 && lvl < hd.Hitpoints.Count) ? lvl : 0;
                hp = hd.Hitpoints[idx];
            }
            if (hp <= 0)
                hp = 1;
            ca.SetHeroHealth(hd, hp);
            ca.SetHeroState(hd, 2);   //R-C5: 2 = disponivel para atacar

            //R-C5: persiste IMEDIATAMENTE. Sem isso a vida do heroi e o saldo de
            //gemas voltavam ao valor anterior quando a vila era recarregada.
            ObjectManager.SavePlayerNow(level);

            Debugger.WriteLine("[UCS] R-C5: heroi " + hd.GetName() + " revivido por " + cost +
                " gemas (vida " + hp + ", restam " + ca.GetDiamonds() + ")");
        }
    }
}
