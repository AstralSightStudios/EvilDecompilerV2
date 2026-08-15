using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.ByteCode
{
    public class QuickJsAssembler
    {

        Writer writer;
        
        public QuickJsAssembler(Stream stream)
        {
            writer = new Writer(stream);
        }

        public void WriteInstruction(QuickJsInstruction instruction)
        {
            writer.Write((byte)instruction.getOpCode().OPCode);
            writer.Write(instruction.getOperand().GetBytes());
        }

        public void WriteAllInstructions(QuickJsInstruction[] instruction)
        {
            for (int i = 0; i < instruction.Length; i++)
            {
                WriteInstruction(instruction[i]);
            }
        }

    }
}
