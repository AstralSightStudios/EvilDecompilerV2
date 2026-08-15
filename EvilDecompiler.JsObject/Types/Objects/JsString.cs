using System.Text;

namespace EvilDecompiler.JsObject.Types.Objects
{
    public class JsString : JsObject
    {
        public string Value
        {
            get
            {
                if (IsWide)
                    return Encoding.Unicode.GetString(Raw);
                else
                    return Encoding.UTF8.GetString(Raw);
            }
            set
            {
                if(IsWide)
                    Raw = Encoding.Unicode.GetBytes(value);
                else
                    Raw = Encoding.UTF8.GetBytes(value);
            }
        }

        public bool IsWide = false;

        public byte[] Raw;

        public JsString(string value)
        {
            Tag = ObjectTag.BC_TAG_STRING;
            Raw = Encoding.UTF8.GetBytes(value);
        }

        public JsString(byte[] value, bool wide)
        {
            Tag = ObjectTag.BC_TAG_STRING;

            IsWide = wide;
            Raw = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
