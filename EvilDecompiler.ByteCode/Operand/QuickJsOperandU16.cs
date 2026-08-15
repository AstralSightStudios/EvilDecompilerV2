namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandU16 : QuickJsOperand
    {

        public ushort Value;

        public QuickJsOperandU16(ushort num)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_u16;
            Value = num;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(Value);
        }

    }
}
