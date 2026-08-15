namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsUndefined : JsObject
    {
        public JsUndefined()
        {
            Tag = ObjectTag.BC_TAG_UNDEFINED;
        }

        public override string ToString()
        {
            return "undefined";
        }
    }
}
