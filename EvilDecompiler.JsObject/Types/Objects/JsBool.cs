namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsBool : JsObject
    {
        
        public bool Value;

        public JsBool(bool value) 
        {
            Tag = ObjectTag.BC_TAG_BOOL_FALSE;

            if (value)
                Tag = ObjectTag.BC_TAG_BOOL_TRUE;

            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
