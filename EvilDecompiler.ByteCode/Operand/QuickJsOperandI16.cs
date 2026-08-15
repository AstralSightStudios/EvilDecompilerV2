namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandI16 : QuickJsOperand
    {

        public short Value;

        public QuickJsOperandI16(short num)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_i16;
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
