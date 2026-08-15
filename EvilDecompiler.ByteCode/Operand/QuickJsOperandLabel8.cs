namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandLabel8 : QuickJsOperand
    {

        public sbyte Label;

        public QuickJsOperandLabel8(sbyte label)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_label8;
            Label = label;
        }

        public override string ToString()
        {
            string addr = Label < 0 ? Label.ToString() : "+" + Label.ToString();
            return "[pc: $" + addr + "]";
        }

        public override byte[] GetBytes()
        {
            return [(byte)Label];
        }

    }
}
