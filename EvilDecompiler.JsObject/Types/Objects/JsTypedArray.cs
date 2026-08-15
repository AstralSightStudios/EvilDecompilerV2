namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsTypedArray : JsObject
    {

        public byte ArrayTag;

        public int Length;

        public int Offset;

        public JsObject ArrayBuffer;

        public JsTypedArray(byte tag, int len, int offset, JsObject buffer)
        {
            Tag = ObjectTag.BC_TAG_TYPED_ARRAY;

            ArrayTag = tag;
            Length = len;
            Offset = offset;
            ArrayBuffer = buffer;
        }
    }
}
