using EvilDecompiler.ByteCode.Instruction;

namespace EvilDecompiler.Decompiler.Analyze
{
    public class QuickJsBasicBlockDivider
    {

        public struct BlockIdx
        {
            public int[] Index;
        }

        public struct BasicBlock
        {
            public QuickJsInstruction[] Instructions;
            public BlockIdx AfterBlocks;
            public BlockIdx NextBlocks;
        }

        private QuickJsInstruction[] instructions;

        List<BasicBlock> Blocks;

        public QuickJsBasicBlockDivider(QuickJsInstruction[] ins)
        {
            instructions = ins;
            Blocks = new List<BasicBlock>();
        }

        public BasicBlock[] DivideBasicBlock()
        {

            return Blocks.ToArray();
        }

    }
}
