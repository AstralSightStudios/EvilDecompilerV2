namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandLabel16 : QuickJsOperand
    {

        public short Label;

        public QuickJsOperandLabel16(short label)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_label16;
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
