namespace EvilDecompiler.JsObject.Types
{
    public class VarDef
    {

        public BcIdx VarName;

        public int ScopeLevel;

        public int ScopeNext;

        public VarFlag Flag;

        public VarDef(BcIdx name, int level, int next, VarFlag flag)
        {
            VarName = name;
            ScopeLevel = level;
            ScopeNext = next;
            Flag = flag;
        }

    }
}
