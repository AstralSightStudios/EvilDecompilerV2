using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionReturn : QuickJsInstruction
    {

        public bool HasValue;

        public QuickJsInstructionReturn(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_return ||
                opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_return_async)
                HasValue = true;
            else
                HasValue = false;

        }
    }
}
