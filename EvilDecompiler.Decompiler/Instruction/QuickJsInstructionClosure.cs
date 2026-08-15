using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionClosure : QuickJsInstruction
    {

        public JsFunctionBytecode? Function;

        public QuickJsInstructionClosure(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {

            QuickJsOperandConst8? constFunction = operandObjects as QuickJsOperandConst8;
            if (constFunction != null)
            {
                Function = constFunction.ConstValue as JsFunctionBytecode;
            }

        }
    }
}
