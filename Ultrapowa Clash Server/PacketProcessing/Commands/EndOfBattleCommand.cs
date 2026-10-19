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
            Console.WriteLine("[UCS] R-C4: comando 603 recebido, value=" + ResultValue +
                ", pendingPvP=" + level.HasPendingPvpBattle);
        }
    }
}
