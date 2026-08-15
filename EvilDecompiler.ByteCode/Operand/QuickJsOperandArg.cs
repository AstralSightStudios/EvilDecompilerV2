namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandArg : QuickJsOperand
    {

        public ushort ArgIndex;

        public QuickJsOperandArg(ushort argIndex)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_arg;
            ArgIndex = argIndex;
        }

        public override string ToString()
        {
            return "arg" + ArgIndex.ToString();
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(ArgIndex);
        }

    }
}
