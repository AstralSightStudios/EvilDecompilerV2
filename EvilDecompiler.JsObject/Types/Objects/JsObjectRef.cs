namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsObjectRef : JsObject
    {

        public int Index;

        public JsObjectRef(int index)
        {
            Tag = ObjectTag.BC_TAG_OBJECT_REFERENCE;

            Index = index;
        }

    }
}
