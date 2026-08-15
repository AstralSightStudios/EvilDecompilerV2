using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandNPopU16 : QuickJsOperand
    {

        public ushort NPop;

        public ushort U16;

        public QuickJsOperandNPopU16(ushort nPop, ushort u16)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_npop_u16;
            NPop = nPop;
            U16 = u16;
        }

        public override string ToString()
        {
            return NPop.ToString() + "," + U16.ToString();
        }

        public override byte[] GetBytes()
        {
            return ByteUtils.Combine(BitConverter.GetBytes(NPop), BitConverter.GetBytes(U16));
        }

    }
}
