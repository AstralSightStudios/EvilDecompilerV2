namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsProperties : JsObject
    {

        public Dictionary<BcIdx, JsObject> Properties;

        public JsProperties(Dictionary<BcIdx, JsObject> props)
        {
            Tag = ObjectTag.BC_TAG_OBJECT;
            Properties = props;
        }

    }
}
