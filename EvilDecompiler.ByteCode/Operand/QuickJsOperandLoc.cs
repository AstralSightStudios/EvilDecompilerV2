namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandLoc : QuickJsOperand
    {

        public ushort LocIndex;

        public QuickJsOperandLoc(ushort locIndex)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_loc;
            LocIndex = locIndex;
        }

        public override string ToString()
        {
            return "loc" + LocIndex.ToString();
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(LocIndex);
        }

    }
}
