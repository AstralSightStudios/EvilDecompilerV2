using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionGetVar : QuickJsInstruction
    {

        public string Value;

        public QuickJsInstructionGetVar(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {
            Value = operandObjects.ToString();

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_var_ref0 ||
                opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_var_ref1 ||
                opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_var_ref2 ||
                opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_get_var_ref3 )
                Value = "var_ref" + (opCode.OPCode - QuickJsOPCode.OPCodeValue.OP_get_var_ref0).ToString();
        }
    }
}
