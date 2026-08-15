namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsSharedArrayBuffer : JsObject
    {

        public int Length;

        public ulong Pointer;

        public JsSharedArrayBuffer(int len, ulong pointer)
        {
            Tag = ObjectTag.BC_TAG_SHARED_ARRAY_BUFFER;

            Length = len;
            Pointer = pointer;
        }

    }
}
