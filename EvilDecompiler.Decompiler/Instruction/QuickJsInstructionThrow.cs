using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionThrow : QuickJsInstruction
    {

        public bool NoArg;

        public QuickJsInstructionThrow(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_throw_error)
                NoArg = true;
            else
                NoArg = false;

        }
    }
}
