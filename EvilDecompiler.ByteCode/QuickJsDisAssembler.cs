using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Utils;
using static EvilDecompiler.ByteCode.Type.QuickJsOPCode;

namespace EvilDecompiler.ByteCode
{
    public class QuickJsDisAssembler
    {

        Reader reader;
        JsFunctionBytecode jsFunctionBytecode;
        AtomSet Atoms;

        public QuickJsDisAssembler(Stream stream, JsFunctionBytecode quickJsMethod, AtomSet atoms)
        {
            reader = new Reader(stream);
            jsFunctionBytecode = quickJsMethod;
            Atoms = atoms;
        }

        public QuickJsInstruction ReadInstruction()
        {
            long pc = reader.BaseStream.Position;
            QuickJsOPCode opcode = new QuickJsOPCode(reader.ReadByte());
            byte[] operand = reader.ReadBytes(opcode.Size - 1);

            return new QuickJsInstruction(pc, opcode, operand, jsFunctionBytecode, Atoms);
        }

        public QuickJsInstruction[] ReadAllInstructions()
        {

            List<QuickJsInstruction> list = new List<QuickJsInstruction>();

            while (true) {
                if (IsEnd())
                    break;
                list.Add(ReadInstruction());
            }

            return list.ToArray();
        }

        public bool IsEnd()
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return true;
            }
            return false;
        }

    }
}
