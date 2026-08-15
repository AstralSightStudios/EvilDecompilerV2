namespace EvilDecompiler.JsObject.Utils
{
    public class Reader : BinaryReader
    {
        public Reader(Stream input) : base(input) { }

        public int ReadLeb128()
        {
            int a, v = 0;

            for (int i = 0; i < 5; i++)
            {
                a = ReadByte();
                v |= (a & 0x7f) << (i * 7);
                if ((a & 0x80) == 0)
                {
                    return v;
                }
            }

            throw new Exception("Error decoding byte array. Cannot fit in int32.");
        }

        public int ReadSLeb128()
        {
            int val = ReadLeb128();
            return (val >> 1) ^ -(val & 1);
        }

    }
}
