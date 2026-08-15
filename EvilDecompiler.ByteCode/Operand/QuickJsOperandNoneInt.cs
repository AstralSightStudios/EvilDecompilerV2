namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandNoneInt : QuickJsOperand
    {

        public readonly int Value;

        public QuickJsOperandNoneInt(int num)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_none_int;
            Value = num;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

    }
}
