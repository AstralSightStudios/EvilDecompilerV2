namespace EvilDecompiler.JsObject.Types
{
    public class ClosureVarDef
    {

        public BcIdx VarName;

        public int VarIdx;

        public ClosureVarFlag Flag;

        public ClosureVarDef(BcIdx name, int idx, ClosureVarFlag flag)
        {
            VarName = name;
            VarIdx = idx;
            Flag = flag;
        }
    }
}
