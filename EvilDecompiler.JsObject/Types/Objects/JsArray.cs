namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsArray : JsObject
    {

        public List<JsObject> Array;

        public JsObject? Template;

        public JsArray(ObjectTag tag, List<JsObject> array, JsObject? template)
        {
            Tag = tag;

            Array = array;
            Template = template;
        }
    }
}
