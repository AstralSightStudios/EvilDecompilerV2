using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.JsObject.Types
{
    public class FunctionFlag
    {

        public int HasPrototype
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

        public int HasSimpleParameterList
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

        public int IsDerivedClassConstructor
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

        public int NeedHomeObject
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

        public int FuncKind
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

        public int NewTargetAllowed
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

        public int SuperCallAllowed
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 7, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 7, value);
            }
        }

        public int SuperAllowed
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 8, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 8, value);
            }
        }

        public int ArgumentsAllowed
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 9, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 9, value);
            }
        }

        public int HasDebug
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 10, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 10, value);
            }
        }

        public int BacktraceBarrier
        {
            get
            {
                return FlagUtils.GetFlags(Flag, 11, 1);
            }
            set
            {
                FlagUtils.SetFlags(ref Flag, 11, value);
            }
        }

        public int Flag;

        public FunctionFlag(int flag)
        {
            Flag = flag;
        }

    }
}
