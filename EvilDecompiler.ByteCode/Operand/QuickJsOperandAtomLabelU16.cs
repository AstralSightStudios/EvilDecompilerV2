using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandAtomLabelU16 : QuickJsOperand
    {

        public AtomIdx AtomIndex;

        public JsString? AtomValue;

        public uint Label;

        public ushort U16;

        public QuickJsOperandAtomLabelU16(uint atomIndex, uint label, ushort u16, AtomSet atoms)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_atom_label_u16;
            AtomIndex = new AtomIdx((int)atomIndex);
            AtomValue = atoms.Get(AtomIndex);
            Label = label;
            U16 = u16;
        }

        public override string ToString()
        {
            string addr = Label < 0 ? Label.ToString() : "+" + Label.ToString();

            if (AtomIndex.IsTaggedInt)
            {
                return AtomIndex.Value.ToString();
            }
            if (AtomValue == null)
            {
                return "[atom: " + AtomIndex.ToString() + "],  [pc: $" + addr + ", ]" + U16.ToString();
            }
            else
            {
                return AtomValue.Value + ",  [pc: $" + addr + "], " + U16.ToString();
            }
        }

        public override byte[] GetBytes()
        {
            return ByteUtils.Combine(BitConverter.GetBytes(AtomIndex.Flag), ByteUtils.Combine(BitConverter.GetBytes(Label), BitConverter.GetBytes(U16)));
        }

    }
}
