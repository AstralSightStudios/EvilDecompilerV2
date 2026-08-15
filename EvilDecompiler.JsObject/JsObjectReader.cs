using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.JsObject
{
    public class JsObjectReader
    {

        Utils.Reader reader;

        public Types.Objects.JsObject? JsObject = null;

        public AtomSet? Atoms = null;

        public JsObjectReader(Stream stream)
        {

            reader = new Utils.Reader(stream);

            if (reader.ReadByte() != Types.Objects.JsObject.Version)
                throw new Exception("Unsupported Version!");

            Atoms = ReadObjectAtoms();
            JsObject = ReadObjectRec();
        }

        AtomSet ReadObjectAtoms()
        {
            AtomSet result = new AtomSet();

            int count = reader.ReadLeb128();

            for (int i = 0; i < count; i++)
            {
                result.Add(ReadJsString());
            }

            return result;
        }

        Types.Objects.JsObject ReadObjectRec()
        {

            ObjectTag tag = (ObjectTag)reader.ReadByte();

            switch (tag)
            {
                case ObjectTag.BC_TAG_NULL:
                    return new JsNull();

                case ObjectTag.BC_TAG_UNDEFINED:
                    return new JsUndefined();

                case ObjectTag.BC_TAG_BOOL_FALSE:
                case ObjectTag.BC_TAG_BOOL_TRUE:
                    if(tag == ObjectTag.BC_TAG_BOOL_TRUE)
                        return new JsBool(true);
                    else
                        return new JsBool(false);

                case ObjectTag.BC_TAG_INT32:
                    return new JsInt32(reader.ReadSLeb128());

                case ObjectTag.BC_TAG_FLOAT64:
                    return new JsFloat64(BitConverter.ToDouble(reader.ReadBytes(8)));

                case ObjectTag.BC_TAG_STRING:
                    return ReadJsString();

                case ObjectTag.BC_TAG_OBJECT:
                    return ReadJsProperties();

                case ObjectTag.BC_TAG_ARRAY:
                case ObjectTag.BC_TAG_TEMPLATE_OBJECT:
                    return ReadJsArray(tag);

                case ObjectTag.BC_TAG_FUNCTION_BYTECODE:
                    return ReadJsFunction();

                case ObjectTag.BC_TAG_MODULE:
                    return ReadJsModule();

                case ObjectTag.BC_TAG_BIG_INT:
                    return ReadJsBigInt();

                case ObjectTag.BC_TAG_TYPED_ARRAY:
                    return ReadJsTypedArray();

                case ObjectTag.BC_TAG_ARRAY_BUFFER:
                    return ReadJsArrayBuffer();

                case ObjectTag.BC_TAG_SHARED_ARRAY_BUFFER:
                    return ReadSharedArrayBuffer();

                case ObjectTag.BC_TAG_DATE:
                    return ReadJsDate();

                case ObjectTag.BC_TAG_OBJECT_VALUE:
                    return ReadJsObjectVal();

                case ObjectTag.BC_TAG_OBJECT_REFERENCE:
                    return ReadJsObjectRef();

                default:
                    throw new Exception($"Unsupported Tag! Value: {tag}");
            }
        }

        JsDate ReadJsDate()
        {
            return new JsDate(ReadObjectRec());
        }

        JsBigInt ReadJsBigInt()
        {
            int e = reader.ReadSLeb128();

            int expn = e >> 1;
            byte[]? mantissa = null;

            // expn 为 ZERO(0)/INF(1)/NAN(2) 时没有 mantissa
            if (expn != 0 && expn != 1 && expn != 2)
            {
                mantissa = reader.ReadBytes(reader.ReadLeb128());
            }

            return new JsBigInt(e, mantissa);
        }

        JsObjectVal ReadJsObjectVal()
        {
            return new JsObjectVal(ReadObjectRec());
        }

        JsObjectRef ReadJsObjectRef()
        {
            return new JsObjectRef(reader.ReadLeb128());
        }

        JsSharedArrayBuffer ReadSharedArrayBuffer()
        {

            int len = reader.ReadLeb128();
            ulong ptr = reader.ReadUInt64();

            return new JsSharedArrayBuffer(len, ptr);
        }

        JsArrayBuffer ReadJsArrayBuffer()
        {
            return new JsArrayBuffer(reader.ReadBytes(reader.ReadLeb128()));
        }

        JsTypedArray ReadJsTypedArray()
        {
            byte arrayTag = reader.ReadByte();
            int len = reader.ReadLeb128();
            int offset = reader.ReadLeb128();
            Types.Objects.JsObject arrayBuffer = ReadObjectRec();

            return new JsTypedArray(arrayTag, len, offset, arrayBuffer);
        }

        JsArray ReadJsArray(ObjectTag tag)
        {
            int count = reader.ReadLeb128();

            Types.Objects.JsObject? template = null;
            List<Types.Objects.JsObject> array = new List<Types.Objects.JsObject>();

            for (int i = 0; i < count; i++)
            {
                array.Add(ReadObjectRec());
            }

            if (tag == ObjectTag.BC_TAG_TEMPLATE_OBJECT)
            {
                template = ReadObjectRec();
            }

            return new JsArray(tag, array, template);
        }

        JsProperties ReadJsProperties()
        {
            int count = reader.ReadLeb128();
            Dictionary<BcIdx, Types.Objects.JsObject> props = new Dictionary<BcIdx, Types.Objects.JsObject>();

            for (int i = 0; i < count; i++)
            {
                BcIdx key = ReadAtomIdx();
                props.Add(key, ReadObjectRec());
            }

            return new JsProperties(props);
        }

        JsFunctionBytecode ReadJsFunction()
        {
            FunctionFlag func_flag = new FunctionFlag(reader.ReadUInt16());

            int js_mode = reader.ReadByte();
            BcIdx func_name = ReadAtomIdx();

            ushort arg_count = (ushort)reader.ReadLeb128();
            ushort var_count = (ushort)reader.ReadLeb128();
            ushort defined_arg_count = (ushort)reader.ReadLeb128();
            ushort stack_size = (ushort)reader.ReadLeb128();

            int closure_var_count = reader.ReadLeb128();
            int cpool_count = reader.ReadLeb128();
            int byte_code_len = reader.ReadLeb128();
            int local_count = reader.ReadLeb128();

            List<VarDef> var_defs = new List<VarDef>();

            for (int i = 0; i < local_count; i++)
            {
                BcIdx name = ReadAtomIdx();
                int level = reader.ReadLeb128();
                int next = reader.ReadLeb128();
                VarFlag var_flag = new VarFlag(reader.ReadByte());
                var_defs.Add(new VarDef(name, level, next, var_flag));
            }

            List<ClosureVarDef> closure_var_defs = new List<ClosureVarDef>();

            for (int i = 0; i < closure_var_count; i++)
            {
                BcIdx name = ReadAtomIdx();
                int idx = reader.ReadLeb128();
                ClosureVarFlag closure_var_flag = new ClosureVarFlag(reader.ReadByte());
                closure_var_defs.Add(new ClosureVarDef(name, idx, closure_var_flag));
            }

            byte[] bytecode = reader.ReadBytes(byte_code_len);

            DebugInfo? debug_info = null;

            if (func_flag.HasDebug != 0)
            {
                BcIdx file = ReadAtomIdx();
                int line = reader.ReadLeb128();
                byte[] map = reader.ReadBytes(reader.ReadLeb128());

                debug_info = new DebugInfo(file, line, map);
            }

            List<Types.Objects.JsObject> cpool = new List<Types.Objects.JsObject>();

            for (int i = 0; i < cpool_count; i++)
            {
                cpool.Add(ReadObjectRec());
            }

            return new JsFunctionBytecode(func_flag, js_mode, func_name, arg_count, var_count, defined_arg_count, stack_size, var_defs, closure_var_defs, bytecode, debug_info, cpool);
        }

        JsModule ReadJsModule()
        {
            BcIdx name = ReadAtomIdx();
            List<ReqModuleEntry> reqs = new List<ReqModuleEntry>();
            List<ExportEntry> exports = new List<ExportEntry>();
            List<StarExportEntry> stars = new List<StarExportEntry>();
            List<ImportEntry> imports = new List<ImportEntry>();

            int count = reader.ReadLeb128();

            for (int i = 0; i < count; i++)
            {
                ReqModuleEntry entry = new ReqModuleEntry();

                entry.ModuleNames = ReadAtomIdx();

                reqs.Add(entry);
            }

            count = reader.ReadLeb128();

            for (int i = 0; i < count; i++)
            {

                ExportEntry entry = new ExportEntry();

                entry.Type = (ExportType)reader.ReadByte();

                if (entry.Type == ExportType.JS_EXPORT_TYPE_LOCAL)
                {
                    entry.VarIdx = reader.ReadLeb128();
                }
                else
                {
                    entry.ReqModuleIdx = reader.ReadLeb128();
                    entry.LocalName = ReadAtomIdx();
                }

                entry.ExportName = ReadAtomIdx();

                exports.Add(entry);
            }

            count = reader.ReadLeb128();

            for (int i = 0; i < count; i++)
            {
                StarExportEntry entry = new StarExportEntry();

                entry.ReqModuleIdx = reader.ReadLeb128();

                stars.Add(entry);
            }

            count = reader.ReadLeb128();

            for (int i = 0; i < count; i++)
            {
                ImportEntry entry = new ImportEntry();

                entry.VarIdx = reader.ReadLeb128();
                entry.ImportName = ReadAtomIdx();
                entry.ReqModuleIdx = reader.ReadLeb128();

                imports.Add(entry);
            }

            return new JsModule(name, reqs, exports, stars, imports, ReadObjectRec());
        }

        BcIdx ReadAtomIdx()
        {
            return new BcIdx(reader.ReadLeb128());
        }

        JsString ReadJsString()
        {

            int flag = reader.ReadLeb128();
            int len = flag >> 1;
            bool wide = false;

            if ((flag & 1) != 0)
            {
                len <<= 1;
                wide = true;
            }

            return new JsString(reader.ReadBytes(len), wide);
        }

    }
}
