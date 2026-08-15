namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandLoc8 : QuickJsOperand
    {

        public byte LocIndex;

        public QuickJsOperandLoc8(byte locIndex)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_loc8;
            LocIndex = locIndex;
        }

        public override string ToString()
        {
            return "loc" + LocIndex.ToString();
        }

        public override byte[] GetBytes()
        {
            return [LocIndex];
        }

    }
}
