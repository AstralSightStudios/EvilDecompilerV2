using EvilDecompiler.ByteCode.Instruction;

namespace EvilDecompiler.Decompiler.IR
{
    /// <summary>基本块终结方式</summary>
    public enum BlockTerminator
    {
        FallThrough,    // 顺序流到下一块
        CondJump,       // if_true/if_false：两个出口（Taken + FallThrough）
        Jump,           // goto：单一目标
        Return,         // return/return_undef/return_async
        Throw,          // throw/throw_error
        TailCall,       // tail_call/tail_call_method
        Indirect,       // ret（gosub 的返回，目标是栈上数据）
        End             // 字节码末尾
    }

    /// <summary>控制流图基本块</summary>
    public class IrBlock
    {
        public int Index;
        public long StartPc;
        public long EndPc;   // 最后一条指令的 pc
        public List<QuickJsInstruction> Instructions = new List<QuickJsInstruction>();

        public BlockTerminator Terminator = BlockTerminator.FallThrough;

        /// <summary>条件跳/无条件跳的目标块（CondJump 的"跳转"出口）</summary>
        public IrBlock? JumpTarget;
        /// <summary>条件跳的 fall-through 出口 / FallThrough 的下一块</summary>
        public IrBlock? NextBlock;

        public List<IrBlock> Predecessors = new List<IrBlock>();
        public List<IrBlock> Successors = new List<IrBlock>();

        // ===== SSA 提升产物 =====
        /// <summary>块内语句（SsaLiftPass 生成）</summary>
        public List<IrStatement> Statements = new List<IrStatement>();
        /// <summary>入口操作数栈（来自前驱出口的 phi 占位）</summary>
        public List<IrValue> EntryStack = new List<IrValue>();
        /// <summary>出口操作数栈（符号执行结束时的栈）</summary>
        public List<IrValue> ExitStack = new List<IrValue>();
        /// <summary>条件跳的条件值（CondJump 时有效）</summary>
        public IrValue? Condition;
        /// <summary>条件为假时跳转（if_false 系列）还是为真时跳转（if_true 系列）</summary>
        public bool JumpOnFalse;

        public bool IsJumpTarget;   // 有边跳入（需要输出标签兜底时用）

        /// <summary>for_of_start / for_in_start 记录的被迭代表达式（StructurePass 重组 for-of/for-in 用）</summary>
        public IrValue? ForOfIterable;
        /// <summary>ForOfIterable 来自 for_in_start（true）还是 for_of_start（false）</summary>
        public bool ForOfIsForIn;

        /// <summary>OP_catch 的异常处理块目标（try/catch 结构化用）</summary>
        public IrBlock? CatchTarget;

        public override string ToString() => "block_" + Index + " [pc " + StartPc + ".." + EndPc + "] " + Terminator;
    }
}
