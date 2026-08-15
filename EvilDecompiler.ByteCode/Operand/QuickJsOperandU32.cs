namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandU32 : QuickJsOperand
    {

        public uint Value;

        public QuickJsOperandU32(uint num)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_u32;
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
