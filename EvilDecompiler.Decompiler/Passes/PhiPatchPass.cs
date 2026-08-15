using EvilDecompiler.Decompiler.IR;

namespace EvilDecompiler.Decompiler.Passes
{
    /// <summary>
    /// Pass 3: φ 校验。
    ///
    /// 值级分歧由 SsaLiftPass 的入口栈合并处理（undefined 哨兵折叠、?? 默认值折叠、
    /// 无法折叠时 SsaLift 自行插入注释）。本 pass 只校验真正的异常情况：
    /// 多前驱块的出口栈**深度**不一致（分析错误或特殊栈标记泄漏）。
    /// </summary>
    public class PhiPatchPass : IFunctionPass
    {
        public string Name => "PhiPatch";

        public void Run(IrFunctionContext ctx)
        {
            foreach (IrBlock b in ctx.Function.Blocks)
            {
                if (b.Predecessors.Count <= 1)
                    continue;

                int? depth = null;
                foreach (IrBlock pred in b.Predecessors)
                {
                    // 回边前驱（跳回本块且 pc 更靠后）不参与 SSA 入口合并，跳过检查
                    if (pred.JumpTarget == b && pred.StartPc >= b.StartPc)
                        continue;
                    if (depth == null)
                    {
                        depth = pred.ExitStack.Count;
                        continue;
                    }
                    if (pred.ExitStack.Count == depth)
                        continue;

                    // 深度差由 for-of/for-in 迭代器状态（特殊标记槽位）携带导致 = 正常现象
                    bool hasMarker = b.Predecessors.Any(p =>
                        p.ExitStack.Any(v => v is IrSpecialMarker));
                    if (hasMarker)
                        break;

                    b.Statements.Insert(0, new IrRawLine(
                        "// phi: 多前驱块栈深度不一致（preds=" +
                        string.Join(",", b.Predecessors.Select(p => "b" + p.Index + ":" + p.ExitStack.Count)) + "），需人工检查"));
                    break;
                }
            }
        }
    }
}
