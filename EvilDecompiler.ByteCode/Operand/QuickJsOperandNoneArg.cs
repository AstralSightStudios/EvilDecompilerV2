namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandNoneArg : QuickJsOperand
    {

        public readonly int ArgIndex;

        public QuickJsOperandNoneArg(int argIndex)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_none_arg;
            ArgIndex = argIndex;
        }

        public override string ToString()
        {
            return "arg" + ArgIndex.ToString();
        }

    }
}
