namespace EvilDecompiler.JsObject.Types
{
    public class BcIdx
    {

        public bool IsTaggedInt
        {
            get
            {
                return (Flag & 1) != 0;
            }
            set
            {
                Flag = (Value << 1) | (value ? 1 : 0);
            }
        }

        public int Value
        {
            get
            {
                return Flag >> 1;
            }
            set
            {
                Flag = (value << 1) | (IsTaggedInt ? 1 : 0);
            }
        }

        public int Flag;

        public BcIdx(int flag)
        {
            Flag = flag;
        }

        public BcIdx(int value, bool tagged)
        {
            IsTaggedInt = tagged;
            Value = value;
        }

        public AtomIdx ToAtomIdx()
        {
            return new AtomIdx(Value, IsTaggedInt);
        }
    }
}
