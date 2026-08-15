namespace EvilDecompiler.JsObject.Types
{
    // BcIdx是JsObject中的索引格式，AtomIdx才是引擎内部以及操作数中所使用的格式

    public class AtomIdx
    {

        private readonly int mask = 1 << 31;

        public bool IsTaggedInt
        {
            get
            {
                return (Flag & mask) != 0;
            }
            set
            {
                Flag = Value | (value ? mask : 0);
            }
        }

        public int Value
        {
            get
            {
                return Flag & ~mask;
            }
            set
            {
                Flag = value | (IsTaggedInt ? mask : 0);
            }
        }

        public int Flag;

        public AtomIdx(int flag)
        {
            Flag = flag;
        }

        public AtomIdx(int value, bool tagged)
        {
            IsTaggedInt = tagged;
            Value = value;
        }
    }
}
