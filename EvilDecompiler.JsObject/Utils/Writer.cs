namespace EvilDecompiler.JsObject.Utils
{
    public class Writer : BinaryWriter
    {

        public Writer(Stream output) : base(output) { }

        public void WriteLeb128(int value)
        {
            // 用无符号移位，负数也能在 5 字节内终止（与 Reader.ReadLeb128 的 5 字节上限对应）
            uint v = (uint)value;
            for (; ; )
            {
                uint a = v & 0x7f;
                v >>= 7;
                if (v != 0)
                {
                    Write((byte)(a | 0x80));
                }
                else
                {
                    Write((byte)a);
                    break;
                }
            }
        }

        public void WriteSLeb128(int value)
        {
            // zigzag 编码，用 uint 防 2*value 溢出
            uint zz = ((uint)value << 1) ^ (uint)(value >> 31);
            WriteLeb128((int)zz);
        }

    }
}
