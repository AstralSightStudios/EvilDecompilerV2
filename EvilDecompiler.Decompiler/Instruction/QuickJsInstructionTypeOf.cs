using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionTypeOf : QuickJsInstruction
    {

        public string Value;

        public string IsType;

        public QuickJsInstructionTypeOf(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {
            Value = operandObjects.ToString();

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_typeof_is_function)
                IsType = "function";
            else if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_typeof_is_undefined)
                IsType = "undefined";
            else
                IsType = "";
        }
    }
}
