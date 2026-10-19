using System;
using System.IO;
using UCS.Helpers;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    // Command 603 (0x025B): enviado pelo cliente ao concluir um ataque.
    // A semantica/escala do inteiro ainda precisa ser confirmada por captura controlada.
    class EndOfBattleCommand : Command
    {
        public EndOfBattleCommand(BinaryReader br)
        {
            if (br == null)
                throw new ArgumentNullException("br");
            if (br.BaseStream.Length - br.BaseStream.Position < 4)
                throw new EndOfStreamException("Comando 603 requer payload de 4 bytes.");

            ResultValue = br.ReadInt32WithEndian();
        }

        public int ResultValue { get; private set; }

        public override void Execute(Level level)
        {
            if (level == null)
                return;

            level.RecordEndOfBattle(ResultValue);
            string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
            //R-C4b: calibracao da escala do 603 -> arquivo proprio, uma linha por 603.
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "r-c4-603.log"),
                    stamp + " attacker=" + level.GetPlayerAvatar().GetId() +
                    " defender=" + level.BattleDefenderId +
                    " value=" + ResultValue + Environment.NewLine);
            }
            catch { }
            Console.WriteLine("[UCS] R-C4: " + stamp + " comando 603 recebido, value=" + ResultValue +
                ", pendingPvP=" + level.HasPendingPvpBattle);
        }
    }
}
