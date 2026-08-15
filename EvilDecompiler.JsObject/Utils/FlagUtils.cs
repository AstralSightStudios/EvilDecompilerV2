namespace EvilDecompiler.JsObject.Utils
{
    public class FlagUtils
    {

        public static int GetFlags(int flags, ref int index, int n)
        {
            int val;
            /* XXX: this does not work for n == 32 */
            val = (flags >> index) & ((1 << n) - 1);
            index += n;
            return val;
        }

        public static int GetFlags(int flags, int index, int n)
        {
            return (flags >> index) & ((1 << n) - 1);
        }

        public static void SetFlags(ref int flags, ref int index, int val, int n)
        {
            flags = flags | (val << index);
            index += n;
        }

        public static void SetFlags(ref int flags, int index, int val)
        {
            flags = flags | (val << index);
        }

    }
}
