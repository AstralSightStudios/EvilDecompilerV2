namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandU8 : QuickJsOperand
    {

        public byte Value;

        public QuickJsOperandU8(byte num)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_u8;
            Value = num;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override byte[] GetBytes()
        {
            return [Value];
        }

    }
}
