using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;

namespace EvilDecompiler.ByteCode.Instruction
{

    public interface IQuickJsInstruction
    {
        long getPC();

        QuickJsOPCode getOpCode();

        QuickJsOPCodeFormat getFormat();

        QuickJsOperand getOperand();
    }

}
