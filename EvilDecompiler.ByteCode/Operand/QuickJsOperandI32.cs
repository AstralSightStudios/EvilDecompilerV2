namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandI32 : QuickJsOperand
    {

        public int Value;

        public QuickJsOperandI32(int num)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_i32;
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
