namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsFloat64 : JsObject
    {

        public double Value;

        public JsFloat64(double value)
        {
            Tag = ObjectTag.BC_TAG_FLOAT64;
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

    }
}
