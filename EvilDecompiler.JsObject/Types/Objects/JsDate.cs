namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsDate : JsObject
    {

        public JsObject Date;

        public JsDate(JsObject date)
        {
            Tag = ObjectTag.BC_TAG_DATE;

            Date = date;
        }

    }
}
