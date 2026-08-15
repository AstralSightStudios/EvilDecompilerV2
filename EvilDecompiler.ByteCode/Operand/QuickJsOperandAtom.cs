using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandAtom : QuickJsOperand
    {

        public AtomIdx AtomIndex;

        public JsString? AtomValue;

        public QuickJsOperandAtom(uint atomIndex, AtomSet atoms)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_atom;
            AtomIndex = new AtomIdx((int)atomIndex);
            AtomValue = atoms.Get(AtomIndex);
        }

        public override string ToString()
        {
            if (AtomIndex.IsTaggedInt)
            {
                return AtomIndex.Value.ToString();
            }
            if (AtomValue == null)
            {
                return "[atom: " + AtomIndex.ToString() + "]";
            }
            else
            {
                return AtomValue.Value;
            }
        }

        public override byte[] GetBytes()
        {
            return BitConverter.GetBytes(AtomIndex.Flag);
        }

    }
}
