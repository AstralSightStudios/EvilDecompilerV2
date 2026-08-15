using EvilDecompiler.JsObject.Types.Objects;
using EvilDecompiler.JsObject.Utils;

namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandConst8 : QuickJsOperand
    {

        public byte ConstIndex;

        public JsObject.Types.Objects.JsObject ConstValue;

        public JsString? JsStr;

        public bool IsString;

        public QuickJsOperandConst8(byte constIndex, List<JsObject.Types.Objects.JsObject> cPool)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_const8;
            ConstIndex = constIndex;
            ConstValue = cPool[ConstIndex];
            JsStr = cPool[ConstIndex] as JsString;
        }

        public override string ToString()
        {
            if (JsStr != null)
            {
                if (!JsStr.IsWide)
                {
                    if (!ByteUtils.IsValidUtf8(JsStr.Raw))
                        return "\"" + ByteUtils.StringToUnicode(JsStr.Value) + "\"";
                }

                return "\"" + JsStr.Value + "\"";
            }
            return ConstValue.ToString();
        }

        public override byte[] GetBytes()
        {
            return [ConstIndex];
        }

    }
}
