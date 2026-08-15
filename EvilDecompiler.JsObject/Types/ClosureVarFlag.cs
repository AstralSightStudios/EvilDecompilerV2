using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.JsObject.Types
{
    public class ClosureVarFlag
    {

        public int IsLocal
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 0, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 0, value);
            }
        }

        public int IsArg
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 1, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 1, value);
            }
        }

        public int IsConst
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 2, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 2, value);
            }
        }

        public int IsLexical
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 3, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 3, value);
            }
        }

        public int VarKind
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 4, 4);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 4, value);
            }
        }

        public int Flag;

        public ClosureVarFlag(int flag)
        {
            Flag = flag;
        }

    }
}
