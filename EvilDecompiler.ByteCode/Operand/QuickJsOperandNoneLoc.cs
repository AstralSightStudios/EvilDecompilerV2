namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandNoneLoc : QuickJsOperand
    {

        public readonly int LocIndex;

        public QuickJsOperandNoneLoc(int locIndex)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_none_loc;
            LocIndex = locIndex;
        }

        public override string ToString()
        {
            return "loc" + LocIndex.ToString();
        }

    }
}
