namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsArrayBuffer : JsObject
    {

        public byte[] Buffer;

        public JsArrayBuffer(byte[] buffer)
        {
            Tag = ObjectTag.BC_TAG_ARRAY_BUFFER;

            Buffer = buffer;
        }

    }
}
