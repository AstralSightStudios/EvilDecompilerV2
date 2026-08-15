using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionPushValue : QuickJsInstruction
    {

        public string Value;

        public QuickJsInstructionPushValue(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {
            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_push_this)
            {
                Value = "this";
            }
            else if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_push_empty_string)
            {
                Value = "\"\"";
            }
            else if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_push_true)
            {
                Value = "true";
            }
            else if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_push_false)
            {
                Value = "false";
            }
            else
            {
                if (operandObjects as QuickJsOperandAtom != null)
                    Value = "\"" + operandObjects.ToString().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"";
                else
                    Value = operandObjects.ToString();
            }
        }
    }
}
