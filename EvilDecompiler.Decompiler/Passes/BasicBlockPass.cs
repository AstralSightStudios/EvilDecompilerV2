using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.Decompiler.IR;
using static EvilDecompiler.ByteCode.Type.QuickJsOPCode;

namespace EvilDecompiler.Decompiler.Passes
{
    /// <summary>
    /// Pass 1: 基本块划分 + CFG 建边。
    ///
    /// 跳转目标统一公式（quickjs.c CASE(OP_goto)/CASE(OP_if_false) 等已验证）：
    ///   target_pc = 指令pc + 1 + (有符号)label操作数
    /// （偏移相对于操作数字段起始 = opcode 字节之后；label8/16/32 仅宽度不同）
    ///
    /// leader 集合 = {0} ∪ 所有跳转目标 ∪ 条件分支的下一条 ∪ catch/gosub 目标 ∪ 终结指令的下一条
    /// </summary>
    public class BasicBlockPass : IFunctionPass
    {
        public string Name => "BasicBlock";

        public void Run(IrFunctionContext ctx)
        {
            QuickJsInstruction[] ins = ctx.Function.Instructions;
            if (ins.Length == 0) return;

            // pc → 指令下标
            Dictionary<long, int> pcToIndex = new Dictionary<long, int>();
            for (int i = 0; i < ins.Length; i++)
                pcToIndex[ins[i].getPC()] = i;

            // ---- 收集 leader ----
            SortedSet<long> leaders = new SortedSet<long>();
            leaders.Add(ins[0].getPC());

            for (int i = 0; i < ins.Length; i++)
            {
                QuickJsOPCode code = ins[i].getOpCode();
                long pc = ins[i].getPC();
                long nextPc = (i + 1 < ins.Length) ? ins[i + 1].getPC() : -1;

                long? target = GetJumpTarget(ins[i]);

                if (IsCondJump(code))
                {
                    if (target.HasValue) leaders.Add(target.Value);
                    if (nextPc >= 0) leaders.Add(nextPc);
                }
                else if (IsUncondJump(code))
                {
                    if (target.HasValue) leaders.Add(target.Value);
                    if (nextPc >= 0) leaders.Add(nextPc); // 死代码也可能是别的块的入口（保守）
                }
                else if (code.OPCode == OPCodeValue.OP_catch || code.OPCode == OPCodeValue.OP_gosub)
                {
                    if (target.HasValue) leaders.Add(target.Value);
                    if (nextPc >= 0) leaders.Add(nextPc);
                }
                else if (IsTerminator(code))
                {
                    if (nextPc >= 0) leaders.Add(nextPc);
                }
            }

            // ---- 划分块 ----
            List<IrBlock> blocks = new List<IrBlock>();
            IrBlock? cur = null;
            for (int i = 0; i < ins.Length; i++)
            {
                if (leaders.Contains(ins[i].getPC()) || cur == null)
                {
                    cur = new IrBlock { Index = blocks.Count, StartPc = ins[i].getPC() };
                    blocks.Add(cur);
                }
                cur.Instructions.Add(ins[i]);
                cur.EndPc = ins[i].getPC();
            }

            // ---- 建边 ----
            Dictionary<long, IrBlock> startPcToBlock = new Dictionary<long, IrBlock>();
            foreach (var b in blocks)
                startPcToBlock[b.StartPc] = b;

            for (int bi = 0; bi < blocks.Count; bi++)
            {
                IrBlock b = blocks[bi];
                QuickJsInstruction last = b.Instructions[b.Instructions.Count - 1];
                QuickJsOPCode code = last.getOpCode();
                IrBlock? next = (bi + 1 < blocks.Count) ? blocks[bi + 1] : null;
                long? target = GetJumpTarget(last);
                IrBlock? targetBlock = (target.HasValue && startPcToBlock.ContainsKey(target.Value))
                    ? startPcToBlock[target.Value] : null;

                if (IsCondJump(code))
                {
                    b.Terminator = BlockTerminator.CondJump;
                    b.JumpTarget = targetBlock;
                    b.NextBlock = next;
                    b.JumpOnFalse = (code.OPCode == OPCodeValue.OP_if_false || code.OPCode == OPCodeValue.OP_if_false8);
                    if (targetBlock != null) Link(b, targetBlock);
                    if (next != null) Link(b, next);
                }
                else if (IsUncondJump(code))
                {
                    b.Terminator = BlockTerminator.Jump;
                    b.JumpTarget = targetBlock;
                    if (targetBlock != null) Link(b, targetBlock);
                }
                else if (code.OPCode == OPCodeValue.OP_catch)
                {
                    // catch 不是控制流终结：顺序进入 try 体，同时建一条到 handler 的
                    // 异常边，让 handler 块在 SSA 提升时拿到栈上的 CatchOffset 标记
                    b.Terminator = (next != null) ? BlockTerminator.FallThrough : BlockTerminator.End;
                    b.NextBlock = next;
                    if (next != null) Link(b, next);
                    if (targetBlock != null)
                    {
                        b.CatchTarget = targetBlock;
                        Link(b, targetBlock);
                    }
                }
                else if (code.OPCode == OPCodeValue.OP_return || code.OPCode == OPCodeValue.OP_return_undef
                         || code.OPCode == OPCodeValue.OP_return_async)
                {
                    b.Terminator = BlockTerminator.Return;
                }
                else if (code.OPCode == OPCodeValue.OP_throw || code.OPCode == OPCodeValue.OP_throw_error)
                {
                    b.Terminator = BlockTerminator.Throw;
                }
                else if (code.OPCode == OPCodeValue.OP_tail_call || code.OPCode == OPCodeValue.OP_tail_call_method)
                {
                    b.Terminator = BlockTerminator.TailCall;
                }
                else if (code.OPCode == OPCodeValue.OP_ret)
                {
                    b.Terminator = BlockTerminator.Indirect;
                }
                else
                {
                    b.Terminator = (next != null) ? BlockTerminator.FallThrough : BlockTerminator.End;
                    b.NextBlock = next;
                    if (next != null) Link(b, next);
                }

                if (targetBlock != null)
                    targetBlock.IsJumpTarget = true;
            }

            ctx.Function.Blocks = blocks;
        }

        private static void Link(IrBlock from, IrBlock to)
        {
            if (!from.Successors.Contains(to)) from.Successors.Add(to);
            if (!to.Predecessors.Contains(from)) to.Predecessors.Add(from);
        }

        /// <summary>取指令的跳转目标 pc（无跳转语义返回 null）。target = insPc + 1 + offset</summary>
        public static long? GetJumpTarget(QuickJsInstruction ins)
        {
            QuickJsOperand op = ins.getOperand();
            long pc = ins.getPC();

            switch (op)
            {
                case QuickJsOperandLabel l:
                    return pc + 1 + (int)l.Label;
                case QuickJsOperandLabel16 l16:
                    return pc + 1 + l16.Label;
                case QuickJsOperandLabel8 l8:
                    return pc + 1 + l8.Label;
                case QuickJsOperandAtomLabelU8 al8:   // throw_error 等：atom + label
                    return pc + 1 + al8.Label;
                case QuickJsOperandAtomLabelU16 al16:
                    return pc + 1 + al16.Label;
                default:
                    return null;
            }
        }

        public static bool IsCondJump(QuickJsOPCode code)
        {
            return code.OPCode == OPCodeValue.OP_if_true || code.OPCode == OPCodeValue.OP_if_false
                || code.OPCode == OPCodeValue.OP_if_true8 || code.OPCode == OPCodeValue.OP_if_false8;
        }

        public static bool IsUncondJump(QuickJsOPCode code)
        {
            return code.OPCode == OPCodeValue.OP_goto || code.OPCode == OPCodeValue.OP_goto8
                || code.OPCode == OPCodeValue.OP_goto16;
        }

        public static bool IsTerminator(QuickJsOPCode code)
        {
            switch (code.OPCode)
            {
                case OPCodeValue.OP_return:
                case OPCodeValue.OP_return_undef:
                case OPCodeValue.OP_return_async:
                case OPCodeValue.OP_throw:
                case OPCodeValue.OP_throw_error:
                case OPCodeValue.OP_tail_call:
                case OPCodeValue.OP_tail_call_method:
                case OPCodeValue.OP_ret:
                    return true;
                default:
                    return false;
            }
        }
    }
}
