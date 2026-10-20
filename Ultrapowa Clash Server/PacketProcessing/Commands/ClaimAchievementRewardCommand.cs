using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UCS.Logic;
using UCS.Helpers;
using UCS.GameFiles;
using UCS.Core;

namespace UCS.PacketProcessing
{
    //Commande 0x20B
    class ClaimAchievementRewardCommand : Command
    {
        public ClaimAchievementRewardCommand(BinaryReader br)
        {
            AchievementId = br.ReadUInt32WithEndian();//= achievementId - 0x015EF3C0;
            Unknown1 = br.ReadUInt32WithEndian();
        }

        //00 00 02 0B 01 5E F3 C6 00 00 06 53

        public uint AchievementId { get; set; }
        public uint Unknown1 { get; set; }

        //Tutorial step13 = achievements
        public override void Execute(Level level)
        {
            ClientAvatar ca = level.GetPlayerAvatar();
            ca.TutorialStepsCount = Math.Max(ca.TutorialStepsCount, 13u);

            //R-C5: credita a RECOMPENSA da conquista (gemas + xp) no servidor.
            //Antes este comando so marcava o passo do tutorial: as gemas do resgate
            //existiam apenas no cliente e sumiam ao recarregar o jogo. Como o servidor
            //ficava com 0 gemas, o HasEnoughDiamonds() era falso e o "concluir
            //treinamento" (speed-up) era bloqueado -> as tropas nunca eram entregues.
            try
            {
                long index = (long)AchievementId - 0x015EF3C0;
                if (index < 0)
                    return;
                DataTable dt = ObjectManager.DataTables.GetTable(22); //AchievementData
                if (dt == null || index >= dt.GetItemCount())
                    return;
                AchievementData ad = dt.GetItemAt((int)index) as AchievementData;
                if (ad == null)
                    return;

                //nao creditar duas vezes a mesma conquista
                bool alreadyClaimed = false;
                foreach (DataSlot slot in ca.Achievements)
                {
                    if (slot.Data != null && slot.Data.GetName() == ad.GetName())
                    {
                        alreadyClaimed = true;
                        break;
                    }
                }
                if (alreadyClaimed)
                {
                    Debugger.WriteLine("[UCS] Conquista ja resgatada: " + ad.GetName());
                    return;
                }

                int gems = ad.DiamondReward;
                if (gems > 0)
                {
                    //as gemas existem no escalar (m_vCurrentGems) E no slot "Diamonds"
                    //dos recursos: o Encode usa o escalar e o client mostra o slot.
                    ca.SetDiamonds(ca.GetDiamonds() + gems);
                    ResourceData diamonds = ObjectManager.DataTables.GetResourceByName("Diamonds");
                    if (diamonds != null)
                        ca.SetResourceCount(diamonds, ca.GetResourceCount(diamonds) + gems);
                }

                ca.Achievements.Add(new DataSlot(ad, 1));
                Debugger.WriteLine("[UCS] Conquista resgatada: " + ad.GetName() +
                    " +" + gems + " gemas (total " + ca.GetDiamonds() + ")");

                //R-C5: persiste na hora (as gemas nao podem sumir)
                ObjectManager.SavePlayerNow(level);
            }
            catch (Exception ex)
            {
                Debugger.WriteLine("[UCS] ClaimAchievementReward falhou: " + ex.Message);
            }
        }
    }
}
