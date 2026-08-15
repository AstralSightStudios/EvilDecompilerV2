using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionMath : QuickJsInstruction
    {

        public string Symbol;

        public QuickJsInstructionMath(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {

            switch (opCode.OPCode)
            {
                case QuickJsOPCode.OPCodeValue.OP_mul:
                    Symbol = "*";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_div:
                    Symbol = "/";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_mod:
                    Symbol = "%";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_add:
                    Symbol = "+";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_sub:
                    Symbol = "-";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_pow:
                    Symbol = "pow";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_shl:
                    Symbol = "shl";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_sar:
                    Symbol = ">>";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_shr:
                    Symbol = "shr";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_xor:
                    Symbol = "^";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_and:
                    Symbol = "&";
                    break;
                case QuickJsOPCode.OPCodeValue.OP_or:
                    Symbol = "|";
                    break;

                default:
                    Symbol = "???";
                    break;
            }

        }
    }
}
