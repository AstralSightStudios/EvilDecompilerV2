namespace EvilDecompiler.JsObject.Types.Objects
{

    public enum ObjectTag
    {
        BC_TAG_NONE,
        BC_TAG_NULL,
        BC_TAG_UNDEFINED,
        BC_TAG_BOOL_FALSE,
        BC_TAG_BOOL_TRUE,
        BC_TAG_INT32,
        BC_TAG_FLOAT64,
        BC_TAG_STRING,
        BC_TAG_OBJECT,
        BC_TAG_ARRAY,
        BC_TAG_BIG_INT,
        BC_TAG_BIG_FLOAT,
        BC_TAG_BIG_DECIMAL,
        BC_TAG_TEMPLATE_OBJECT,
        BC_TAG_FUNCTION_BYTECODE,
        BC_TAG_MODULE,
        BC_TAG_TYPED_ARRAY,
        BC_TAG_ARRAY_BUFFER,
        BC_TAG_SHARED_ARRAY_BUFFER,
        BC_TAG_DATE,
        BC_TAG_OBJECT_VALUE,
        BC_TAG_OBJECT_REFERENCE,
    }

    public class JsObject
    {
        public static int Version = 1;

        public ObjectTag Tag = ObjectTag.BC_TAG_NONE;

        public override string ToString()
        {
            return "[object]";
        }
    }
}
