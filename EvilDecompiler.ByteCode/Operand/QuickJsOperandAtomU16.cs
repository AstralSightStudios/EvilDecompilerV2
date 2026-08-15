using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandAtomU16 : QuickJsOperand
    {

        public AtomIdx AtomIndex;

        public JsString? AtomValue;

        public ushort U16;

        public QuickJsOperandAtomU16(uint atomIndex, ushort u16, AtomSet atoms)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_atom_u16;
            AtomIndex = new AtomIdx((int)atomIndex);
            AtomValue = atoms.Get(AtomIndex);
            U16 = u16;
        }

        public override string ToString()
        {
            if (AtomValue == null)
            {
                return "[atom: " + AtomIndex.ToString() + "], " + U16.ToString();
            }
            else
            {
                return AtomValue.Value + ", " + U16.ToString();
            }
        }

        public override byte[] GetBytes()
        {
            return ByteUtils.Combine(BitConverter.GetBytes(AtomIndex.Flag), BitConverter.GetBytes(U16));
        }

    }
}
