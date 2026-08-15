namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandNone : QuickJsOperand
    {

        public QuickJsOperandNone()
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_none;
        }

        public override string ToString()
        {
            return "";
        }

    }
}
