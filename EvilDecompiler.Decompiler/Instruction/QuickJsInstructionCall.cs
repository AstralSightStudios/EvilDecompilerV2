using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Instruction
{
    public class QuickJsInstructionCall : QuickJsInstruction
    {

        public int ExtPopCount;

        public bool HasResult;

        public bool Constructor;

        public QuickJsInstructionCall(long pc, QuickJsOPCode opCode, QuickJsOperand operand, JsFunctionBytecode quickJsMethod, AtomSet atoms) : base(pc, opCode, operand, quickJsMethod, atoms)
        {

            QuickJsOperandNPop? pop = operandObjects as QuickJsOperandNPop;
            if (pop != null)
                ExtPopCount = pop.NPop;

            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_tail_call_method)
                HasResult = false;
            else
                HasResult = true;


            if (opCode.OPCode == QuickJsOPCode.OPCodeValue.OP_call_constructor)
                Constructor = true;
            else
                Constructor = false;
        }
    }
}
