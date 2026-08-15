using System.Numerics;

namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsBigInt : JsObject
    {

        // 原始的 sign+exponent 组合值（sleb128 解码后、未拆分的 e），原样写回以保证字节级回环
        public int ESigned;

        // 原始的 mantissa 字节（小端序），expn 为 ZERO/INF/NAN 时为 null
        public byte[]? Mantissa;

        public JsBigInt(int eSigned, byte[]? mantissa)
        {
            Tag = ObjectTag.BC_TAG_BIG_INT;
            ESigned = eSigned;
            Mantissa = mantissa;
        }

        public override string ToString()
        {
            int sign = ESigned & 1;
            int e = ESigned >> 1;

            if (e == 0)
                return "0n";
            if (e == 1)
                return "Infinity";
            if (e == 2)
                return "NaN";

            // quickjs JS_WriteBigNum：写出的是 libbf 归一化尾数（最高位置 1），
            // 且剥离了尾部全零字节；e = expn + 3，expn 是真实值的位长。
            // 还原公式：value = mantissa >> (bitlen(mantissa) - expn)
            // （推导：value = M_full >> (totalBits - expn)，M_full = mantissa << 8k，
            //   totalBits = 8k + bitlen(mantissa)，约掉 k 即得）
            long expn = e >= 3 ? e - 3 : e;
            byte[] le = new byte[Mantissa!.Length + 1];
            Array.Copy(Mantissa, le, Mantissa.Length);
            BigInteger mantissa = new BigInteger(le);

            long shift = (long)mantissa.GetBitLength() - expn;
            BigInteger magnitude = shift > 0 ? mantissa >> (int)shift : mantissa;

            return (sign != 0 ? "-" : "") + magnitude.ToString() + "n";
        }

    }
}
