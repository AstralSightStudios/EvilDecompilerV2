using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.JsObject.Types
{
    public class VarFlag
    {

        public int VarKind
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 0, 4);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 0, value);
            }
        }

        public int IsConst
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 4, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 4, value);
            }
        }

        public int IsLexical
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 5, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 5, value);
            }
        }

        public int IsCaptured
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 6, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 6, value);
            }
        }

        public int Flag;

        public VarFlag(int flag)
        {
            Flag = flag;
        }

    }
}
