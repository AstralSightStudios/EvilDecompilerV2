namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsNull : JsObject
    {
        public JsNull()
        {
            Tag = ObjectTag.BC_TAG_NULL;
        }

        public override string ToString()
        {
            return "null";
        }
    }
}
