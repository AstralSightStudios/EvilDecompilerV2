namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsModule : JsObject
    {

        public BcIdx ModuleName;

        public List<ReqModuleEntry> ReqModules;

        public List<ExportEntry> ModuleExports;

        public List<StarExportEntry> ModuleStarExports;

        public List<ImportEntry> ModuleImports;

        public JsObject FunctionObject;

        public JsModule(BcIdx name, List<ReqModuleEntry> reqs, List<ExportEntry> exports, List<StarExportEntry> stars, List<ImportEntry> imports, JsObject function)
        {

            Tag = ObjectTag.BC_TAG_MODULE;

            ModuleName = name;
            ReqModules = reqs;
            ModuleExports = exports;
            ModuleStarExports = stars;
            ModuleImports = imports;
            FunctionObject = function;
        }

        public override string ToString()
        {
            return "[module]";
        }

    }
}
