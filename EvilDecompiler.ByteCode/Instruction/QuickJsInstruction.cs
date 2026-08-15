using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Utils;


namespace EvilDecompiler.ByteCode.Instruction
{

    public class QuickJsInstruction : IQuickJsInstruction
    {

        protected long pc;
        protected QuickJsOPCode opCode;
        protected QuickJsOperand operandObjects;

        public JsFunctionBytecode FunctionBytecode;
        public AtomSet AtomSet;

        public QuickJsInstruction(long pc, QuickJsOPCode opCode, byte[] operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : this(pc, opCode, parseOperandByFormat(opCode, operand, quickJsMethod, atoms), quickJsMethod, atoms)
        {
        }

        public QuickJsInstruction(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms)
        {
            this.pc = pc;
            this.opCode = opCode;
            operandObjects = operand;
            FunctionBytecode = quickJsMethod;
            AtomSet = atoms;
        }

        public QuickJsOPCodeFormat getFormat()
        {
            return opCode.Format;
        }

        public QuickJsOPCode getOpCode()
        {
            return opCode;
        }

        public QuickJsOperand getOperand()
        {
            return operandObjects;
        }

        public long getPC()
        {
            return pc;
        }

        private static QuickJsOperand parseOperandByFormat(QuickJsOPCode opCode, byte[] operand, JsFunctionBytecode quickJsMethod, AtomSet atoms)
        {
            Reader reader = new Reader(new MemoryStream(operand));
            QuickJsOperand result = new QuickJsOperandNone();

            QuickJsOPCodeFormat format = opCode.Format;

            switch (format)
            {

                case QuickJsOPCodeFormat.OP_FMT_none:
                    result = new QuickJsOperandNone();
                    break;

                case QuickJsOPCodeFormat.OP_FMT_none_int:
                    result = new QuickJsOperandNoneInt(opCode.OPCode - QuickJsOPCode.OPCodeValue.OP_push_0);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_npopx:
                    result = new QuickJsOperandNPopX(opCode.OPCode - QuickJsOPCode.OPCodeValue.OP_call0);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_none_var_ref:
                    result = new QuickJsOperandNoneVarRef((opCode.OPCode - QuickJsOPCode.OPCodeValue.OP_get_var_ref0) % 4);
                    break;


                case QuickJsOPCodeFormat.OP_FMT_none_arg:
                    result = new QuickJsOperandNoneArg((opCode.OPCode - QuickJsOPCode.OPCodeValue.OP_get_arg0) % 4);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_none_loc:
                    result = new QuickJsOperandNoneLoc((opCode.OPCode - QuickJsOPCode.OPCodeValue.OP_get_loc0) % 4);
                    break;

                // 以上Format均是伪操作数(无实际字节)

                case QuickJsOPCodeFormat.OP_FMT_u8:
                    result = new QuickJsOperandU8(reader.ReadByte());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_i8:
                    result = new QuickJsOperandI8(reader.ReadSByte());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_u16:
                    result = new QuickJsOperandU16(reader.ReadUInt16());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_i16:
                    result = new QuickJsOperandI16(reader.ReadInt16());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_u32:
                    result = new QuickJsOperandU32(reader.ReadUInt32());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_i32:
                    result = new QuickJsOperandI32(reader.ReadInt32());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_npop:
                    result = new QuickJsOperandNPop(reader.ReadUInt16());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_npop_u16:
                    result = new QuickJsOperandNPopU16(reader.ReadUInt16(), reader.ReadUInt16());
                    break;

                // label与pc对应
                case QuickJsOPCodeFormat.OP_FMT_label8:
                    result = new QuickJsOperandLabel8(reader.ReadSByte());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_label16:
                    result = new QuickJsOperandLabel16(reader.ReadInt16());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_label:
                    result = new QuickJsOperandLabel(reader.ReadUInt32());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_const8:
                    result = new QuickJsOperandConst8(reader.ReadByte(), quickJsMethod.CPool);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_const:
                    result = new QuickJsOperandConst(reader.ReadUInt32(), quickJsMethod.CPool);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_atom_u8:
                    result = new QuickJsOperandAtomU8(reader.ReadUInt32(), reader.ReadByte(), atoms);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_atom_u16:
                    result = new QuickJsOperandAtomU16(reader.ReadUInt32(), reader.ReadUInt16(), atoms);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_atom:
                    result = new QuickJsOperandAtom(reader.ReadUInt32(), atoms);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_atom_label_u8:
                    result = new QuickJsOperandAtomLabelU8(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadByte(), atoms);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_atom_label_u16:
                    result = new QuickJsOperandAtomLabelU16(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt16(), atoms);
                    break;

                case QuickJsOPCodeFormat.OP_FMT_loc8:
                    result = new QuickJsOperandLoc8(reader.ReadByte());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_loc:
                    result = new QuickJsOperandLoc(reader.ReadUInt16());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_arg:
                    result = new QuickJsOperandArg(reader.ReadUInt16());
                    break;

                case QuickJsOPCodeFormat.OP_FMT_var_ref:
                    result = new QuickJsOperandVarRef(reader.ReadUInt16());
                    break;

                default:
                    result = new QuickJsOperandRaw(operand, format);
                    break;
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new Exception("reader.BaseStream.Position != reader.BaseStream.Length");

            if (format != result.Format)
                throw new Exception("format != result.Format");


            return result;
        }

        public override string ToString()
        {
            return pc.ToString() + ": " + opCode.Name + " " + operandObjects.ToString();
        }

    }

}
