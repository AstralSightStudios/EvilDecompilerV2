using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandAtomU8 : QuickJsOperand
    {

        public AtomIdx AtomIndex;

        public JsString? AtomValue;

        public byte U8;

        public QuickJsOperandAtomU8(uint atomIndex, byte u8, AtomSet atoms)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_atom_u8;
            AtomIndex = new AtomIdx((int)atomIndex);
            AtomValue = atoms.Get(AtomIndex);
            U8 = u8;
        }

        public override string ToString()
        {
            if (AtomIndex.IsTaggedInt)
            {
                return AtomIndex.Value.ToString();
            }
            if (AtomValue == null)
            {
                return "[atom: " + AtomIndex.ToString() + "], " + U8.ToString();
            }
            else
            {
                return AtomValue.Value + ", " + U8.ToString();
            }
        }

        public override byte[] GetBytes()
        {
            return ByteUtils.Combine(BitConverter.GetBytes(AtomIndex.Flag), [U8]);
        }

    }
}
