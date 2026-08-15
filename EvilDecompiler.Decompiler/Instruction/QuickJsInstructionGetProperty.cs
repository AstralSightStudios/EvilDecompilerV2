using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionGetProperty : QuickJsInstruction
    {

        public string Value;

        public bool PushOrigin;

        public bool StackValue;

        public QuickJsInstructionGetProperty(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {
            Value = operandObjects.ToString();
            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_field2 || opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_array_el2)
            {
                PushOrigin = true;
            }
            else
            {
                PushOrigin = false;
            }

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_length)
                Value = "length";

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_array_el2 || opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_array_el)
                StackValue = true;
            else
                StackValue = false;
        }
    }
}
