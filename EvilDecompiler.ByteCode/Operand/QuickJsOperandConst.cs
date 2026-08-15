namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandConst : QuickJsOperand
    {

        public uint ConstIndex;

        public JsObject.Types.Objects.JsObject ConstValue;

        public QuickJsOperandConst(uint constIndex, List<JsObject.Types.Objects.JsObject> cPool)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_const;
            ConstIndex = constIndex;
            ConstValue = cPool[(int)constIndex];
        }

        public override string ToString()
        {
            return ConstIndex.ToString() + ": " + ConstValue.ToString();
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(ConstIndex);
        }

    }
}
