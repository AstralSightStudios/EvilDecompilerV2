using EvilDecompiler.ByteCode.Type;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandRaw : QuickJsOperand
    {
        public byte[] Raw;

        public QuickJsOperandRaw(byte[] raw, QuickJsOPCodeFormat format)
        {
            Raw = raw;
            Format = format;
        }

        public override byte[] GetBytes()
        {
            return Raw;
        }
    }
}
