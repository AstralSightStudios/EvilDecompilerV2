using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionCompare : QuickJsInstruction
    {

        public string Symbol;

        public QuickJsInstructionCompare(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {

            switch (opCode.OPCode)
            {
                case QuickJsOPCode.OPCodeValue.OP_lt:
                    Symbol = "<";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_lte:
                    Symbol = "<=";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_gt:
                    Symbol = ">";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_gte:
                    Symbol = ">=";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_eq:
                    Symbol = "==";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_neq:
                    Symbol = "!=";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_strict_neq:
                    Symbol = "!==";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_strict_eq:
                    Symbol = "===";
                    break;

                default:
                    Symbol = "???";
                    break;
            }

        }
    }
}
