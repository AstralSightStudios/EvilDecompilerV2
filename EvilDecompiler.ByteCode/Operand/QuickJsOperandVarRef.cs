namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandVarRef : QuickJsOperand
    {

        public ushort RefIndex;

        public QuickJsOperandVarRef(ushort refIndex)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_var_ref;
            RefIndex = refIndex;
        }

        public override string ToString()
        {
            return "var_ref" + RefIndex.ToString();
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(RefIndex);
        }

    }
}
