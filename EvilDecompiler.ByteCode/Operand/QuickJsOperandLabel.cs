using System.Reflection.Emit;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandLabel : QuickJsOperand
    {

        public uint Label;

        public QuickJsOperandLabel(uint label)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_label;
            Label = label;
        }

        public override string ToString()
        {
            string addr = Label < 0 ? Label.ToString() : "+" + Label.ToString();
            return "[pc: $" + addr + "]";
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(Label);
        }

    }
}
