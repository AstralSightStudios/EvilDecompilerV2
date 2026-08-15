namespace EvilDecompiler.JsObject.Types
{

    public enum ExportType
    {
        JS_EXPORT_TYPE_LOCAL,
        JS_EXPORT_TYPE_INDIRECT,
    }

    public class ExportEntry
    {

        public ExportType Type;

        public int VarIdx;

        public int ReqModuleIdx;

        public BcIdx? LocalName;

        public BcIdx? ExportName;

    }
}
