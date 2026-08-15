namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsObjectVal : JsObject
    {

        public JsObject Value;

        public JsObjectVal(JsObject value)
        {
            Tag = ObjectTag.BC_TAG_OBJECT_VALUE;

            Value = value;
        }

    }
}
