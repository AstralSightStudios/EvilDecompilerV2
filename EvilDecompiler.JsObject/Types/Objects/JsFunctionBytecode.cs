namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsFunctionBytecode : JsObject
    {

        public FunctionFlag FunctionFlag;

        public int JsMode;

        public BcIdx FunctionName;

        public ushort ArgCount;

        public ushort VarCount;

        public ushort DefinedArgCount;

        public ushort StackSize;

        public List<VarDef> VarDefs;

        public List<ClosureVarDef> ClosureVarDefs;

        public byte[] Bytecode;

        public DebugInfo? DebugInfo;

        public List<JsObject> CPool;

        public JsFunctionBytecode(FunctionFlag flag, int mode, BcIdx name, ushort arg, ushort var, ushort def, ushort stack, List<VarDef> vars, List<ClosureVarDef> cvars, byte[] code, DebugInfo? debug, List<JsObject> cpool)
        {
            Tag = ObjectTag.BC_TAG_FUNCTION_BYTECODE;
            FunctionFlag = flag;
            JsMode = mode;
            FunctionName = name;
            ArgCount = arg;
            VarCount = var;
            DefinedArgCount = def;
            StackSize = stack;
            VarDefs = vars;
            ClosureVarDefs = cvars;
            Bytecode = code;
            DebugInfo = debug;
            CPool = cpool;
        }

        public override string ToString()
        {
            return "[bytecode "+ GetHashCode().ToString() + "]";
        }

    }
}
