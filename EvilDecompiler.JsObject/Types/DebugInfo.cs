namespace EvilDecompiler.JsObject.Types
{
    public class DebugInfo
    {

        public BcIdx FileName;

        public int LineNumber;

        public byte[] MapData;

        public DebugInfo(BcIdx file, int line, byte[] map)
        {
            FileName = file;
            LineNumber = line;
            MapData = map;
        }

    }
}
