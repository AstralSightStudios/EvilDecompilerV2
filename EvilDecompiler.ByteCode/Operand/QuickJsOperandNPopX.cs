namespace EvilDecompiler.ByteCode.Operand
{
    public class QuickJsOperandNPopX : QuickJsOperand
    {

        public readonly int NPop;

        public QuickJsOperandNPopX(int nPop)
        {
            Format = Type.QuickJsOPCodeFormat.OP_FMT_npopx;
            NPop = nPop;
        }

        public override string ToString()
        {
            return NPop.ToString();
        }

    }
}
