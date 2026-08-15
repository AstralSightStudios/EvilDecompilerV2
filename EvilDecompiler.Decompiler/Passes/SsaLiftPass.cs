using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.ByteCode.Operand;
using EvilDecompiler.ByteCode.Type;
using EvilDecompiler.Decompiler.IR;
using EvilDecompiler.JsObject.Types.Objects;
using static EvilDecompiler.ByteCode.Type.QuickJsOPCode;

namespace EvilDecompiler.Decompiler.Passes
{
    /// <summary>
    /// Pass 2: SSA 提升。逐基本块符号执行，把操作数栈提升为 SSA 值（表达式树），
    /// 副作用操作（调用/赋值/return/throw）物化为块内语句。
    ///
    /// 栈语义全部以 quickjs-native/quickjs/quickjs.c 的 CASE 实现为准（权威）。
    /// 关键差异点（相对旧 lifter 的修正）：
    ///   - call_method 栈布局 [this, func, args...]（this 在 func 之下）
    ///   - call_constructor 栈布局 [ctor, new.target, args...]
    ///   - 参数从栈顶弹出是逆序，需要反转
    ///   - dup1: a b → a a b（复制次顶插入），不是简单复制栈顶
    ///   - get_field2/get_array_el2 保留 obj（作为方法调用的 this）
    ///   - pow=**, shl=&lt;&lt;, sar=&gt;&gt;, shr=&gt;&gt;&gt;
    ///   - 局部变量/参数/闭包变量用 VarDefs/ClosureVarDefs 真名
    /// </summary>
    public class SsaLiftPass : IFunctionPass
    {
        public string Name => "SsaLift";

        private IrFunction func = null!;
        private readonly HashSet<int> processed = new HashSet<int>();
        private readonly HashSet<int> visiting = new HashSet<int>();
        private List<IrValue> stack = null!;
        private IrBlock block = null!;
        // 局部变量统一在函数头声明（AstBuildPass 用 GetLocalNames 生成），
        // 这里只跟踪 dup 物化产生的 tmp 变量
        private int tmpCounter;
        /// <summary>dup 物化产生的临时变量（需要 let 声明）</summary>
        public List<string> TmpVariables = new List<string>();
        /// <summary>define_var 收集到的全局 var 声明（顶层函数头输出 var ...）</summary>
        public List<string> GlobalVars = new List<string>();
        /// <summary>已被语句承载过的值实例（引用相等）：残留副本再次 drop 时不再物化</summary>
        private readonly HashSet<IrValue> consumed = new HashSet<IrValue>();

        public void Run(IrFunctionContext ctx)
        {
            func = ctx.Function;
            tmpCounter = 0;
            TmpVariables.Clear();
            GlobalVars.Clear();

            processed.Clear();
            visiting.Clear();

            foreach (IrBlock b in func.Blocks)
                ProcessBlock(b);
        }

        /// <summary>
        /// 按语义顺序（DFS）处理基本块：先递归处理未处理的前驱，保证入口栈可用。
        /// 回边前驱（visiting 中）跳过——QuickJS 产物中回边处栈为空。
        /// 例如 for-of 的条件块 pc 在循环体之后，体块的入口栈来自条件块的出口栈。
        /// </summary>
        private void ProcessBlock(IrBlock b)
        {
            if (processed.Contains(b.Index) || visiting.Contains(b.Index))
                return;

            visiting.Add(b.Index);

            // for-of/for-in 条件块：只从"设立块"（含 for_of_start/for_in_start 的前驱）
            // 取入口栈。其余前驱（体尾块）是 pc 前向但逻辑上的回边——递归它们会形成
            // 死锁链（体首块需要本块出口，本块又递归体尾，体尾再递归回体首块）
            IEnumerable<IrBlock> predList = b.Predecessors;
            if (b.Instructions.Count > 0)
            {
                OPCodeValue firstOp = b.Instructions[0].getOpCode().OPCode;
                if (firstOp == OPCodeValue.OP_for_of_next || firstOp == OPCodeValue.OP_for_in_next)
                {
                    var setupPreds = b.Predecessors.Where(p => p.Instructions.Any(ins =>
                        ins.getOpCode().OPCode == OPCodeValue.OP_for_of_start
                        || ins.getOpCode().OPCode == OPCodeValue.OP_for_in_start)).ToList();
                    if (setupPreds.Count > 0)
                        predList = setupPreds;
                }
            }

            // 先确保前驱已处理（回边除外），入口栈合并所有可用前驱的出口栈
            // 合并规则：槽位值相同直接用；不同时若一方是 undefined 常量取另一方
            // （覆盖 hole-check 模板：undefined 哨兵 vs 真实源表达式）；
            // 再尝试识别短路逻辑（&&/||/??）与三元表达式；其余建 IrPhi 占位
            List<IrValue> entry = new List<IrValue>(b.EntryStack);
            // 每个槽位当前值的来源前驱（用于短路/三元识别），null = 复合产物或未知
            List<IrBlock?> origins = new List<IrBlock?>(new IrBlock?[b.EntryStack.Count]);
            // 链条起始前驱（折叠后保留，用于嵌套三元的外层条件识别）
            List<IrBlock?> chainStart = new List<IrBlock?>(new IrBlock?[b.EntryStack.Count]);
            // 每个槽位是否已在本次合并中折出逻辑表达式（&& / ||）——用于多项短路链续折
            List<bool> logicalMerge = new List<bool>(new bool[b.EntryStack.Count]);
            bool entryInit = b.EntryStack.Count > 0;
            foreach (IrBlock pred in predList)
            {
                if (visiting.Contains(pred.Index))
                {
                    // 回边：跳过。例外：for-of/for-in 条件块（首指令 for_of_next/for_in_next）
                    // 正在处理中——其出口栈只依赖循环设立块 + for_of_next 新值，
                    // 与体块无关，可现场合成（嵌套 for-of 时体块会先于条件块完成）
                    if (!entryInit && TrySynthesizeIteratorCondExit(pred, out List<IrValue>? synth))
                    {
                        entry = synth;
                        origins = synth.Select(_ => (IrBlock?)pred).ToList();
                        chainStart = synth.Select(_ => (IrBlock?)pred).ToList();
                        logicalMerge = synth.Select(_ => false).ToList();
                        entryInit = true;
                    }
                    continue;
                }
                if (!processed.Contains(pred.Index))
                    ProcessBlock(pred);

                if (!entryInit)
                {
                    entry = new List<IrValue>(pred.ExitStack);
                    origins = pred.ExitStack.Select(_ => (IrBlock?)pred).ToList();
                    chainStart = pred.ExitStack.Select(_ => (IrBlock?)pred).ToList();
                    logicalMerge = pred.ExitStack.Select(_ => false).ToList();
                    entryInit = true;
                    continue;
                }

                // 逐槽位合并（深度不一致时按较短者合并，保留较长者的尾部）
                if (pred.ExitStack.Count != entry.Count)
                {
                    // 取较深的一侧为基础（可选链长短路径汇合时深度可能不同）
                    if (pred.ExitStack.Count > entry.Count)
                    {
                        entry = new List<IrValue>(pred.ExitStack);
                        origins = pred.ExitStack.Select(_ => (IrBlock?)pred).ToList();
                        chainStart = pred.ExitStack.Select(_ => (IrBlock?)pred).ToList();
                        logicalMerge = pred.ExitStack.Select(_ => false).ToList();
                    }
                    continue;
                }
                for (int s = 0; s < entry.Count; s++)
                {
                    string curText = entry[s].Emit();
                    string newText = pred.ExitStack[s].Emit();
                    if (curText == newText)
                        continue;
                    if (curText == "undefined" && newText != "undefined")
                    {
                        entry[s] = pred.ExitStack[s];
                        origins[s] = pred;
                        logicalMerge[s] = false;
                        continue;
                    }
                    if (newText == "undefined")
                        continue;

                    // 多项短路链（a && b && c / a || b || c）：上一轮已折出逻辑表达式，
                    // 本前驱（顺序路径）的值直接并入
                    if (logicalMerge[s] && entry[s] is IrBinaryOp lg && (lg.Op == "&&" || lg.Op == "||"))
                    {
                        // 嵌套三元优先：折叠链起始前驱与本前驱有共同条件分裂块
                        // （is_serial ? (chapterName || name) : "" 的外层条件）
                        IrBlock? c0 = FindCondSplit(chainStart[s], pred);
                        if (c0 != null && c0.Condition != null)
                        {
                            bool curIsFall = c0.NextBlock == chainStart[s];
                            IrValue thenV = (c0.JumpOnFalse == curIsFall) ? entry[s] : pred.ExitStack[s];
                            IrValue elseV = (c0.JumpOnFalse == curIsFall) ? pred.ExitStack[s] : entry[s];
                            entry[s] = new IrTernary(c0.Condition, thenV, elseV) { Id = func.AllocValueId() };
                            logicalMerge[s] = false;
                            continue;
                        }
                        entry[s] = new IrBinaryOp(lg.Op, entry[s], pred.ExitStack[s],
                            lg.Op == "&&" ? 4 : 3) { Id = func.AllocValueId() };
                        continue;
                    }

                    // 短路逻辑（&& / || / ??）与三元（cond ? a : b）识别
                    IrValue? resolved = TryResolveMerged(entry[s], origins[s] ?? chainStart[s], pred.ExitStack[s], pred, b);
                    if (resolved != null)
                    {
                        logicalMerge[s] = resolved is IrBinaryOp rb && (rb.Op == "&&" || rb.Op == "||");
                        entry[s] = resolved;
                        origins[s] = null;
                        continue;
                    }

                    var phi = new IrPhi { Id = func.AllocValueId(), Block = b };
                    phi.Sources.Add(entry[s]);
                    phi.Sources.Add(pred.ExitStack[s]);
                    // 折叠失败（仍是 phi_N 形式）立即告警，不静默吸收
                    if (phi.Emit().StartsWith("phi_"))
                        b.Statements.Insert(0, new IrRawLine(
                            "// phi: 汇合点栈值无法自动合并（" + phi.Emit() + "），需人工检查"));
                    entry[s] = phi;
                    origins[s] = null;
                    logicalMerge[s] = false;
                }
            }

            block = b;
            stack = entry;

            foreach (QuickJsInstruction ins in b.Instructions)
            {
                LiftInstruction(ins);
            }

            b.ExitStack = new List<IrValue>(stack);
            visiting.Remove(b.Index);
            processed.Add(b.Index);

            // 分支末块残留收编：出口栈上的副作用残留若流向"首指令为 drop 的汇合块"
            // （QuickJS 语句级分支的栈清理特征），属于本分支的计算，物化到本块末尾；
            // 槽位替换为 undefined，避免汇合点把它和条件幸存值折叠成 cond && call() 垃圾
            AbsorbExitResidue(b);
        }

        /// <summary>递归把值树所有节点标记为"已被语句承载"</summary>
        private void MarkConsumed(IrValue v)
        {
            if (!consumed.Add(v))
                return;
            switch (v)
            {
                case IrUnaryOp u: MarkConsumed(u.Operand); break;
                case IrBinaryOp b: MarkConsumed(b.Left); MarkConsumed(b.Right); break;
                case IrTernary t: MarkConsumed(t.Condition); MarkConsumed(t.Then); MarkConsumed(t.Else); break;
                case IrCall c:
                    MarkConsumed(c.Func);
                    if (c.ThisArg != null) MarkConsumed(c.ThisArg);
                    foreach (var a in c.Args) MarkConsumed(a);
                    break;
                case IrGetProperty g:
                    MarkConsumed(g.Object);
                    if (g.KeyExpr != null) MarkConsumed(g.KeyExpr);
                    break;
                case IrLiteralContainer ct:
                    foreach (var it in ct.Items)
                    {
                        if (it.KeyExpr != null) MarkConsumed(it.KeyExpr);
                        MarkConsumed(it.Value);
                    }
                    break;
                case IrPhi p: foreach (var s in p.Sources) MarkConsumed(s); break;
                case IrClassValue cls:
                    if (cls.Ctor != null) MarkConsumed(cls.Ctor);
                    if (cls.Parent != null) MarkConsumed(cls.Parent);
                    MarkConsumed(cls.Proto);
                    MarkConsumed(cls.StaticItems);
                    break;
            }
        }

        /// <summary>值树的全部副作用/可能抛异常的叶都已被语句承载（重复物化是冗余读取）</summary>
        private bool AllEffectsConsumed(IrValue v)
        {
            if (consumed.Contains(v))
                return true;
            if (!v.NeedsPreserve)
                return true;
            switch (v)
            {
                // ++/-- 自身有副作用：未被承载过就不算 consumed（顶部已查 consumed.Contains(v)）
                case IrUnaryOp u: return !u.IsSideEffect && AllEffectsConsumed(u.Operand);
                case IrBinaryOp b: return AllEffectsConsumed(b.Left) && AllEffectsConsumed(b.Right);
                case IrTernary t:
                    return AllEffectsConsumed(t.Condition) && AllEffectsConsumed(t.Then) && AllEffectsConsumed(t.Else);
                case IrGetProperty g:
                    // 属性读取本身可能抛异常（MustPreserve），必须被承载过；子树另查
                    return consumed.Contains(g)
                        && AllEffectsConsumed(g.Object)
                        && (g.KeyExpr == null || AllEffectsConsumed(g.KeyExpr));
                case IrLiteralContainer ct:
                    return ct.Items.All(it => AllEffectsConsumed(it.Value)
                        && (it.KeyExpr == null || AllEffectsConsumed(it.KeyExpr)));
                case IrPhi p: return p.Sources.All(AllEffectsConsumed);
                case IrClassValue cls: return AllEffectsConsumed(cls.Proto) && AllEffectsConsumed(cls.StaticItems);
                default: return false; // IrCall 等顶层副作用未被承载
            }
        }

        /// <summary>
        /// 分支末块出口残留收编。语句级 if/while 分支的 QuickJS 产物：
        /// 分支末块出口残留一个值，汇合块首指令 drop 清理。残留带副作用时
        /// （then 体末尾的调用结果），必须在本块物化，否则会被带到汇合点错误折叠。
        /// </summary>
        private void AbsorbExitResidue(IrBlock b)
        {
            if (b.ExitStack.Count == 0)
                return;
            // 只处理栈顶槽位（drop 清的是栈顶）；栈底可能是 try 的 catch 标记等，不动
            IrValue v = b.ExitStack[b.ExitStack.Count - 1];
            // 确定的语句级清理汇合才收编：所有后继都是"首指令 drop 或 return_undef
            // 的多前驱汇合块"（否则残留值可能是后继要正常消费的表达式值，不能动）。
            // return_undef 不消费栈：流入其汇合块的残留同样是垃圾，需要在本块物化
            if (b.Successors.Count == 0)
                return;
            foreach (IrBlock succ in b.Successors)
            {
                if (succ.Predecessors.Count < 2
                    || succ.Instructions.Count == 0
                    || (succ.Instructions[0].getOpCode().OPCode != OPCodeValue.OP_drop
                        && succ.Instructions[0].getOpCode().OPCode != OPCodeValue.OP_return_undef))
                    return;
            }
            if (v.NeedsPreserve && !AllEffectsConsumed(v))
            {
                // 未承载的副作用残留（then 体末尾的调用结果等）：物化到本块末
                b.Statements.Add(new IrExprStatement(v) { Pc = b.EndPc });
                MarkConsumed(v);
            }
            // 残留槽位换成 undefined：汇合点按哨兵规则取另一侧/忽略，不再参与折叠
            if (!(v is IrConstant c0 && c0.Text == "undefined"))
                b.ExitStack[b.ExitStack.Count - 1] = new IrConstant("undefined") { Id = func.AllocValueId() };
        }

        // ================= 汇合点值识别（&& / || / ?? / 三元） =================

        /// <summary>
        /// 合成 for-of/for-in 条件块的出口栈：条件块首指令是 for_of_next/for_in_next，
        /// 出口 = 循环设立块（已处理的非回边前驱）出口 + for_of_next 产生的 value
        /// （done 被 if_false 弹掉）。
        /// </summary>
        private bool TrySynthesizeIteratorCondExit(IrBlock cond, out List<IrValue>? exit)
        {
            exit = null;
            if (cond.Instructions.Count == 0)
                return false;
            OPCodeValue first = cond.Instructions[0].getOpCode().OPCode;
            if (first != OPCodeValue.OP_for_of_next && first != OPCodeValue.OP_for_in_next)
                return false;
            // 找已处理的非回边前驱（for_of_start 所在块）
            IrBlock? setup = null;
            foreach (IrBlock p in cond.Predecessors)
            {
                if (!visiting.Contains(p.Index) && p.ExitStack.Count > 0)
                {
                    setup = p;
                    break;
                }
            }
            if (setup == null)
                return false;
            exit = new List<IrValue>(setup.ExitStack);
            var kind = first == OPCodeValue.OP_for_of_next
                ? IrIteratorPlaceholder.Kind.OfValue : IrIteratorPlaceholder.Kind.InValue;
            exit.Add(new IrIteratorPlaceholder(kind) { Id = func.AllocValueId() });
            return true;
        }

        /// <summary>
        /// 尝试把汇合点的两个不同栈值识别为短路逻辑或三元表达式。
        /// QuickJS 产物形状：
        ///   X && Y : X; dup; if_false M / drop; Y; M:     （X 为假时保留 X 跳到 M）
        ///   X || Y : X; dup; if_true  M / drop; Y; M:
        ///   X ?? Y : X; dup; is_undefined_or_null; if_false M / drop; Y; M:
        ///   c ? T : F : c; if_false E / T; goto M / E: F; M:
        /// </summary>
        private IrValue? TryResolveMerged(IrValue curVal, IrBlock? curOrigin, IrValue newVal, IrBlock newPred, IrBlock mergeBlock)
        {
            // A. 新前驱经条件跳转边进入汇合块（newVal 是“幸存值”，curVal 来自顺序路径）
            if (newPred.Terminator == BlockTerminator.CondJump && newPred.JumpTarget == mergeBlock
                && newPred.Condition != null)
            {
                IrValue? r = ResolveShortCircuit(newPred, newVal, curVal);
                if (r != null) return r;
            }
            // B. 当前值来源前驱经条件跳转边进入汇合块（curVal 是幸存值）
            if (curOrigin != null && curOrigin.Terminator == BlockTerminator.CondJump
                && curOrigin.JumpTarget == mergeBlock && curOrigin.Condition != null)
            {
                IrValue? r = ResolveShortCircuit(curOrigin, curVal, newVal);
                if (r != null) return r;
            }
            // C. 三元：两个前驱有共同的条件分裂块 C（C 的两个出口分别直达两前驱）
            IrBlock? c = FindCondSplit(curOrigin, newPred);
            if (c != null && c.Condition != null)
            {
                bool curIsFallthrough = c.NextBlock == curOrigin;
                // JumpOnFalse：fallthrough 出口 = 条件为真分支；JumpOnTrue 相反
                IrValue thenV = (c.JumpOnFalse == curIsFallthrough) ? curVal : newVal;
                IrValue elseV = (c.JumpOnFalse == curIsFallthrough) ? newVal : curVal;
                return new IrTernary(c.Condition, thenV, elseV) { Id = func.AllocValueId() };
            }
            return null;
        }

        /// <summary>短路逻辑识别：condBlock 的跳转幸存值 survivor，另一侧值 other</summary>
        private IrValue? ResolveShortCircuit(IrBlock condBlock, IrValue survivor, IrValue other)
        {
            IrValue cond = condBlock.Condition!;
            // ?? 模式：条件是 (v == null)（is_undefined_or_null），幸存值即 v 本身，
            // if_false 跳转 = v 非 null 时保留 v
            if (condBlock.JumpOnFalse && cond is IrBinaryOp bin && bin.Op == "=="
                && bin.Right is IrConstant rc && rc.Text == "null"
                && bin.Left.Emit() == survivor.Emit())
            {
                return new IrBinaryOp("??", survivor, other, 3) { Id = func.AllocValueId() };
            }
            // && / ||：幸存值就是条件本身（dup 的留存副本）
            if (cond.Emit() == survivor.Emit())
            {
                return condBlock.JumpOnFalse
                    ? new IrBinaryOp("&&", survivor, other, 4) { Id = func.AllocValueId() }
                    : new IrBinaryOp("||", survivor, other, 3) { Id = func.AllocValueId() };
            }
            return null;
        }

        /// <summary>查找共同条件分裂块：其两个出口分别直接是 a、b</summary>
        private static IrBlock? FindCondSplit(IrBlock? a, IrBlock? b)
        {
            if (a == null || b == null) return null;
            foreach (IrBlock cand in a.Predecessors.Concat(b.Predecessors))
            {
                if (cand.Terminator != BlockTerminator.CondJump || cand.Condition == null)
                    continue;
                if ((cand.NextBlock == a && cand.JumpTarget == b)
                    || (cand.NextBlock == b && cand.JumpTarget == a))
                    return cand;
            }
            return null;
        }

        // ================= 栈操作辅助 =================

        private IrValue Pop()
        {
            if (stack.Count == 0)
            {
                // 栈下溢（分析不精确或特殊标记），用占位值防止崩溃
                var v = new IrConstant("undefined /*stack underflow*/");
                v.Id = func.AllocValueId();
                return v;
            }
            IrValue top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return top;
        }

        private IrValue Peek() => stack.Count > 0 ? stack[stack.Count - 1] : new IrConstant("undefined /*empty*/");

        private void Push(IrValue v)
        {
            if (v.Id == 0 && v is not IrConstant) v.Id = func.AllocValueId();
            stack.Add(v);
        }

        /// <summary>复制值（dup 系列用）：副作用值必须先物化成临时变量再引用</summary>
        private IrValue DupValue(IrValue v)
        {
            if (v.HasSideEffect)
            {
                string tmp = "tmp" + (tmpCounter++).ToString();
                TmpVariables.Add(tmp);
                func.CompilerTemps.Add(tmp);
                // 声明统一由函数头输出（TmpVariables），这里生成普通赋值
                block.Statements.Add(new IrAssign(tmp, v, false) { Pc = CurrentPc });
                var varRef = new IrVariable(tmp) { Id = func.AllocValueId() };
                // 栈上所有同一引用槽位也替换为 tmp 变量：
                // 否则残留的副作用表达式会被后续指令再次内联（如 re.exec(str) 出现两次）
                for (int i = 0; i < stack.Count; i++)
                    if (ReferenceEquals(stack[i], v))
                        stack[i] = varRef;
                return varRef;
            }
            return v;
        }

        /// <summary>drop 一个值：有副作用或可能抛异常则物化为语句（深度检查，含被折叠进逻辑表达式的调用）</summary>
        private void DropValue(IrValue v, long pc)
        {
            // dup 模板里的 drop 只是栈杂耍：同一实例仍在栈上（另一副本会继续使用），
            // 或属于短路逻辑模板留存的副本，都不物化
            if (stack.Contains(v) || shortCircuitSurvivors.Contains(v))
                return;
            if (!v.NeedsPreserve)
                return;
            // 副作用已被其他语句承载（同一实例的残留副本）：重复物化是冗余读取
            if (AllEffectsConsumed(v))
                return;
            block.Statements.Add(new IrExprStatement(v) { Pc = pc });
            MarkConsumed(v);
        }

        /// <summary>上一条 drop 的是否是 for_of_done（连续 drop done+value = 解构空洞位）</summary>
        private bool lastDropWasForOfDone;

        /// <summary>
        /// 短路逻辑模板（X; dup; if_true/if_false; drop; Y）中被 dup 的条件实例集合。
        /// 条件在 if_* 处 pop 一份，栈上留存另一份流向汇合点；fallthrough 块里的 drop
        /// 只是丢弃模板的冗余副本，不应物化为噪音语句。
        /// </summary>
        private readonly HashSet<IrValue> shortCircuitSurvivors = new HashSet<IrValue>();

        /// <summary>
        /// return/throw 前冲刷栈上被遗弃的副作用值。
        /// QuickJS 表达式语句的结果可能直接遗弃在栈上（如 `fclosure; call0; return_undef`
        /// 的 IIFE 调用），不经 drop，必须在此物化为语句，否则整条语句丢失（输出截断）。
        /// </summary>
        private void FlushStackSideEffects()
        {
            foreach (IrValue v in stack)
            {
                if (v.NeedsPreserve && !AllEffectsConsumed(v))
                {
                    block.Statements.Add(new IrExprStatement(v) { Pc = CurrentPc });
                    MarkConsumed(v);
                }
            }
            stack.Clear();
        }

        private long CurrentPc;

        /// <summary>变量赋值语句（声明统一由函数头处理，这里一律不生成 let）</summary>
        private void AssignVar(string name, IrValue value, long pc)
        {
            // 函数序言的 this 存储（push_this; put_loc this）无用户语义，跳过
            if (name == "this")
                return;
            MarkConsumed(value);
            // post_inc/post_dec 形状：put 的值是 (旧值变量 ± 1)，且栈上残留同一旧值引用
            // （ReferenceEquals 精确匹配 dup 残留，误伤面为零）
            // → 残留替换为 x++/x-- 后缀表达式，不再生成 x = x + 1 赋值语句
            if (value is IrBinaryOp pb && (pb.Op == "+" || pb.Op == "-")
                && pb.Right is IrConstant one && one.Text == "1"
                && pb.Left is IrVariable lv && lv.Name == name)
            {
                bool replaced = false;
                for (int i = 0; i < stack.Count; i++)
                {
                    if (ReferenceEquals(stack[i], pb.Left))
                    {
                        stack[i] = new IrUnaryOp(pb.Op == "+" ? "++" : "--", lv)
                            { IsPrefix = false, IsSideEffect = true, Id = func.AllocValueId() };
                        replaced = true;
                    }
                }
                if (replaced)
                    return;
            }
            block.Statements.Add(new IrAssign(name, value, false) { Pc = pc });
            // dup + put 组合等价于 set 语义：写回后栈上残留的同一引用应替换为变量
            // 否则会出现 `x = x+1; return x+1;` 这类重复表达式
            if (stack.Count > 0 && ReferenceEquals(stack[stack.Count - 1], value))
                stack[stack.Count - 1] = Var(name);
        }

        /// <summary>
        /// 局部变量读取。编译器伪变量槽（this_func / home_object）不落普通变量引用：
        /// this_func → 当前函数自身标记（供 get_super 或自引用渲染）；
        /// home_object → home_object 标记（供 get_super 组合出 super 表达式）。
        /// </summary>
        private void GetLoc(int idx)
        {
            if (func.ThisFuncLocals.Contains(idx))
            {
                Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.ThisFunc, 0) { Id = func.AllocValueId() });
                return;
            }
            if (func.HomeObjectLocals.Contains(idx))
            {
                Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.HomeObject, 0) { Id = func.AllocValueId() });
                return;
            }
            PushVar(func.GetLocName(idx));
        }

        /// <summary>
        /// 局部变量写入。函数序言里 special_object → put_loc 的伪变量初始化
        /// （命名函数表达式自引用绑定 / home_object）无用户语义：只登记槽索引，不生成语句。
        /// </summary>
        private void PutLoc(int idx)
        {
            IrValue v = Pop();
            if (v is IrSpecialMarker m)
            {
                if (m.Kind == IrSpecialMarker.MarkerKind.ThisFunc)
                {
                    func.ThisFuncLocals.Add(idx);
                    return;
                }
                if (m.Kind == IrSpecialMarker.MarkerKind.HomeObject)
                {
                    func.HomeObjectLocals.Add(idx);
                    return;
                }
            }
            AssignVar(func.GetLocName(idx), v, CurrentPc);
        }

        // ================= 主分发 =================

        private void LiftInstruction(QuickJsInstruction ins)
        {
            CurrentPc = ins.getPC();
            OPCodeValue op = ins.getOpCode().OPCode;
            QuickJsOperand operand = ins.getOperand();

            // 非 drop 指令重置空洞位判定
            if (op != OPCodeValue.OP_drop)
                lastDropWasForOfDone = false;

            switch (op)
            {
                // ---------- push 常量 ----------
                case OPCodeValue.OP_push_minus1:
                case OPCodeValue.OP_push_0:
                case OPCodeValue.OP_push_1:
                case OPCodeValue.OP_push_2:
                case OPCodeValue.OP_push_3:
                case OPCodeValue.OP_push_4:
                case OPCodeValue.OP_push_5:
                case OPCodeValue.OP_push_6:
                case OPCodeValue.OP_push_7:
                    PushConst(((QuickJsOperandNoneInt)operand).Value.ToString());
                    break;
                case OPCodeValue.OP_push_i8:
                    PushConst(((QuickJsOperandI8)operand).Value.ToString());
                    break;
                case OPCodeValue.OP_push_i16:
                    PushConst(((QuickJsOperandI16)operand).Value.ToString());
                    break;
                case OPCodeValue.OP_push_i32:
                    PushConst(((QuickJsOperandI32)operand).Value.ToString());
                    break;
                case OPCodeValue.OP_push_const8:
                    PushConstPool(((QuickJsOperandConst8)operand).ConstIndex);
                    break;
                case OPCodeValue.OP_push_const:
                    PushConstPool(((QuickJsOperandConst)operand).ConstIndex);
                    break;
                case OPCodeValue.OP_push_atom_value:
                    PushConst(QuoteString(AtomName((QuickJsOperandAtom)operand)));
                    break;
                case OPCodeValue.OP_push_empty_string:
                    PushConst("\"\"");
                    break;
                case OPCodeValue.OP_push_this:
                    PushConst("this");
                    break;
                case OPCodeValue.OP_push_true:
                    PushConst("true");
                    break;
                case OPCodeValue.OP_push_false:
                    PushConst("false");
                    break;
                case OPCodeValue.OP_undefined:
                    PushConst("undefined");
                    break;
                case OPCodeValue.OP_null:
                    PushConst("null");
                    break;

                // ---------- 闭包 ----------
                case OPCodeValue.OP_fclosure8:
                    PushClosure(((QuickJsOperandConst8)operand).ConstValue as JsFunctionBytecode);
                    break;
                case OPCodeValue.OP_fclosure:
                    PushClosure(((QuickJsOperandConst)operand).ConstValue as JsFunctionBytecode);
                    break;

                // ---------- 变量读取 ----------
                case OPCodeValue.OP_get_arg:
                    PushVar(func.GetArgName(((QuickJsOperandArg)operand).ArgIndex));
                    break;
                case OPCodeValue.OP_get_arg0:
                case OPCodeValue.OP_get_arg1:
                case OPCodeValue.OP_get_arg2:
                case OPCodeValue.OP_get_arg3:
                    PushVar(func.GetArgName(((QuickJsOperandNoneArg)operand).ArgIndex));
                    break;
                case OPCodeValue.OP_get_loc:
                    GetLoc(((QuickJsOperandLoc)operand).LocIndex);
                    break;
                case OPCodeValue.OP_get_loc_check:
                    GetLoc(((QuickJsOperandLoc)operand).LocIndex);
                    break;
                case OPCodeValue.OP_get_loc0:
                case OPCodeValue.OP_get_loc1:
                case OPCodeValue.OP_get_loc2:
                case OPCodeValue.OP_get_loc3:
                    GetLoc(((QuickJsOperandNoneLoc)operand).LocIndex);
                    break;
                case OPCodeValue.OP_get_loc8:
                    GetLoc(((QuickJsOperandLoc8)operand).LocIndex);
                    break;
                case OPCodeValue.OP_get_var:
                case OPCodeValue.OP_get_var_undef:
                    PushVar(AtomName((QuickJsOperandAtom)operand));
                    break;
                case OPCodeValue.OP_get_var_ref:
                    PushVar(func.GetVarRefName(((QuickJsOperandVarRef)operand).RefIndex));
                    break;
                case OPCodeValue.OP_get_var_ref_check:
                    PushVar(func.GetVarRefName(((QuickJsOperandVarRef)operand).RefIndex));
                    break;
                case OPCodeValue.OP_get_var_ref0:
                case OPCodeValue.OP_get_var_ref1:
                case OPCodeValue.OP_get_var_ref2:
                case OPCodeValue.OP_get_var_ref3:
                    PushVar(func.GetVarRefName(((QuickJsOperandNoneVarRef)operand).RefIndex));
                    break;

                // ---------- 属性读取 ----------
                case OPCodeValue.OP_get_field:
                    {
                        IrValue obj = Pop();
                        Push(new IrGetProperty(obj, AtomName((QuickJsOperandAtom)operand)) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_get_field2:
                    {
                        // [obj] → [obj, val]：obj 保留（方法调用 this）
                        IrValue obj = Peek();
                        Push(new IrGetProperty(obj, AtomName((QuickJsOperandAtom)operand)) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_get_length:
                    {
                        IrValue obj = Pop();
                        Push(new IrGetProperty(obj, "length") { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_get_array_el:
                    {
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        Push(new IrGetProperty(obj, prop) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_get_array_el2:
                    {
                        // [obj, prop] → [obj, val]
                        IrValue prop = Pop();
                        IrValue obj = Peek();
                        Push(new IrGetProperty(obj, prop) { Id = func.AllocValueId() });
                        break;
                    }

                // ---------- 变量写入 ----------
                case OPCodeValue.OP_put_arg:
                    AssignVar(func.GetArgName(((QuickJsOperandArg)operand).ArgIndex), Pop(), CurrentPc);
                    break;
                case OPCodeValue.OP_put_arg0:
                case OPCodeValue.OP_put_arg1:
                case OPCodeValue.OP_put_arg2:
                case OPCodeValue.OP_put_arg3:
                    AssignVar(func.GetArgName(((QuickJsOperandNoneArg)operand).ArgIndex), Pop(), CurrentPc);
                    break;
                case OPCodeValue.OP_put_loc:
                case OPCodeValue.OP_put_loc_check:
                case OPCodeValue.OP_put_loc_check_init:
                    PutLoc(((QuickJsOperandLoc)operand).LocIndex);
                    break;
                case OPCodeValue.OP_put_loc0:
                case OPCodeValue.OP_put_loc1:
                case OPCodeValue.OP_put_loc2:
                case OPCodeValue.OP_put_loc3:
                    PutLoc(((QuickJsOperandNoneLoc)operand).LocIndex);
                    break;
                case OPCodeValue.OP_put_loc8:
                    PutLoc(((QuickJsOperandLoc8)operand).LocIndex);
                    break;
                case OPCodeValue.OP_put_var:
                case OPCodeValue.OP_put_var_init:
                    AssignVar(AtomName((QuickJsOperandAtom)operand), Pop(), CurrentPc);
                    break;
                case OPCodeValue.OP_put_var_ref:
                    AssignVar(func.GetVarRefName(((QuickJsOperandVarRef)operand).RefIndex), Pop(), CurrentPc);
                    break;
                case OPCodeValue.OP_put_var_ref_check:
                case OPCodeValue.OP_put_var_ref_check_init:
                    AssignVar(func.GetVarRefName(((QuickJsOperandVarRef)operand).RefIndex), Pop(), CurrentPc);
                    break;
                case OPCodeValue.OP_put_var_ref0:
                case OPCodeValue.OP_put_var_ref1:
                case OPCodeValue.OP_put_var_ref2:
                case OPCodeValue.OP_put_var_ref3:
                    AssignVar(func.GetVarRefName(((QuickJsOperandNoneVarRef)operand).RefIndex), Pop(), CurrentPc);
                    break;

                // ---------- set 系列：值留栈 ----------
                case OPCodeValue.OP_set_loc:
                    SetVarKeepStack(func.GetLocName(((QuickJsOperandLoc)operand).LocIndex));
                    break;
                case OPCodeValue.OP_set_loc8:
                    SetVarKeepStack(func.GetLocName(((QuickJsOperandLoc8)operand).LocIndex));
                    break;
                case OPCodeValue.OP_set_loc0:
                case OPCodeValue.OP_set_loc1:
                case OPCodeValue.OP_set_loc2:
                case OPCodeValue.OP_set_loc3:
                    SetVarKeepStack(func.GetLocName(((QuickJsOperandNoneLoc)operand).LocIndex));
                    break;
                case OPCodeValue.OP_set_arg:
                    SetVarKeepStack(func.GetArgName(((QuickJsOperandArg)operand).ArgIndex));
                    break;
                case OPCodeValue.OP_set_arg0:
                case OPCodeValue.OP_set_arg1:
                case OPCodeValue.OP_set_arg2:
                case OPCodeValue.OP_set_arg3:
                    SetVarKeepStack(func.GetArgName(((QuickJsOperandNoneArg)operand).ArgIndex));
                    break;
                case OPCodeValue.OP_set_var_ref:
                    SetVarKeepStack(func.GetVarRefName(((QuickJsOperandVarRef)operand).RefIndex));
                    break;
                case OPCodeValue.OP_set_var_ref0:
                case OPCodeValue.OP_set_var_ref1:
                case OPCodeValue.OP_set_var_ref2:
                case OPCodeValue.OP_set_var_ref3:
                    SetVarKeepStack(func.GetVarRefName(((QuickJsOperandNoneVarRef)operand).RefIndex));
                    break;
                case OPCodeValue.OP_set_loc_uninitialized:
                    // TDZ 初始化，无栈操作，忽略
                    break;
                case OPCodeValue.OP_check_define_var:
                    // 全局变量重定义检查，无栈操作，忽略
                    break;
                case OPCodeValue.OP_define_var:
                    // 全局 var 声明（atom_u8），无栈效应；登记名字供函数头输出
                    {
                        string gv = (((QuickJsOperandAtomU8)operand).AtomValue?.Value ?? "?").TrimEnd('\0');
                        if (!GlobalVars.Contains(gv))
                            GlobalVars.Add(gv);
                        break;
                    }
                case OPCodeValue.OP_define_func:
                    {
                        // [func] → []：全局函数声明
                        IrValue fn = Pop();
                        var atomOp = (QuickJsOperandAtomU8)operand;
                        string fnName = (atomOp.AtomValue?.Value ?? "anonymous").TrimEnd('\0');
                        block.Statements.Add(new IrFuncDecl
                        {
                            Name = fnName,
                            Closure = fn as IrClosureValue,
                            Pc = CurrentPc
                        });
                        break;
                    }
                case OPCodeValue.OP_set_name:
                case OPCodeValue.OP_set_name_computed:
                    // 只设置函数 .name，值留栈，忽略
                    break;

                // ---------- 栈杂耍（SSA 化后纯引用操作） ----------
                case OPCodeValue.OP_drop:
                    {
                        IrValue v = Pop();
                        // 数组解构空洞位：for_of_next 后连续 drop done + drop value（类型匹配）
                        if (v is IrIteratorPlaceholder hv && hv.K == IrIteratorPlaceholder.Kind.OfValue
                            && lastDropWasForOfDone)
                            block.Statements.Add(new IrAssign("", v, false) { Pc = CurrentPc });
                        else
                            DropValue(v, CurrentPc);
                        lastDropWasForOfDone = v is IrIteratorPlaceholder dv && dv.K == IrIteratorPlaceholder.Kind.OfDone;
                        break;
                    }
                case OPCodeValue.OP_nop:
                    break;
                case OPCodeValue.OP_dup:
                    Push(DupValue(Peek()));
                    break;
                case OPCodeValue.OP_dup1:
                    {
                        // a b → a a b
                        IrValue b = Pop();
                        IrValue a = Pop();
                        Push(a);
                        Push(DupValue(a));
                        Push(b);
                        break;
                    }
                case OPCodeValue.OP_dup2:
                    {
                        // a b → a b a b
                        IrValue b = Pop();
                        IrValue a = Pop();
                        Push(a); Push(b);
                        Push(DupValue(a)); Push(DupValue(b));
                        break;
                    }
                case OPCodeValue.OP_dup3:
                    {
                        // a b c → a b c a b c
                        IrValue c = Pop();
                        IrValue b = Pop();
                        IrValue a = Pop();
                        Push(a); Push(b); Push(c);
                        Push(DupValue(a)); Push(DupValue(b)); Push(DupValue(c));
                        break;
                    }
                case OPCodeValue.OP_nip:
                    {
                        // a b → b
                        IrValue b = Pop();
                        DropValue(Pop(), CurrentPc);
                        Push(b);
                        break;
                    }
                case OPCodeValue.OP_nip1:
                    {
                        // a b c → b c
                        IrValue c = Pop();
                        IrValue b = Pop();
                        DropValue(Pop(), CurrentPc);
                        Push(b); Push(c);
                        break;
                    }
                case OPCodeValue.OP_insert2:
                    {
                        // obj a → a obj a
                        IrValue a = Pop();
                        IrValue obj = Pop();
                        IrValue aR = DupValue(a); // 副作用值物化后两个副本都要引用 tmp
                        Push(aR);
                        Push(obj);
                        Push(aR);
                        break;
                    }
                case OPCodeValue.OP_insert3:
                    {
                        // obj prop a → a obj prop a
                        IrValue a = Pop();
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        IrValue aR = DupValue(a);
                        Push(aR);
                        Push(obj); Push(prop); Push(aR);
                        break;
                    }
                case OPCodeValue.OP_insert4:
                    {
                        // this obj prop a → a this obj prop a
                        IrValue a = Pop();
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        IrValue thisV = Pop();
                        IrValue aR = DupValue(a);
                        Push(aR);
                        Push(thisV); Push(obj); Push(prop); Push(aR);
                        break;
                    }
                case OPCodeValue.OP_perm3:
                    {
                        // obj a b → a obj b
                        IrValue b = Pop();
                        IrValue a = Pop();
                        IrValue obj = Pop();
                        Push(a); Push(obj); Push(b);
                        break;
                    }
                case OPCodeValue.OP_perm4:
                    {
                        // obj prop a b → a obj prop b
                        IrValue b = Pop();
                        IrValue a = Pop();
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        Push(a); Push(obj); Push(prop); Push(b);
                        break;
                    }
                case OPCodeValue.OP_perm5:
                    {
                        // this obj prop a b → a this obj prop b
                        IrValue b = Pop();
                        IrValue a = Pop();
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        IrValue thisV = Pop();
                        Push(a); Push(thisV); Push(obj); Push(prop); Push(b);
                        break;
                    }
                case OPCodeValue.OP_swap:
                    {
                        IrValue b = Pop();
                        IrValue a = Pop();
                        Push(b); Push(a);
                        break;
                    }
                case OPCodeValue.OP_swap2:
                    {
                        // a b c d → c d a b
                        IrValue d = Pop();
                        IrValue c = Pop();
                        IrValue b = Pop();
                        IrValue a = Pop();
                        Push(c); Push(d); Push(a); Push(b);
                        break;
                    }
                case OPCodeValue.OP_rot3l:
                    {
                        // x a b → a b x
                        IrValue b = Pop();
                        IrValue a = Pop();
                        IrValue x = Pop();
                        Push(a); Push(b); Push(x);
                        break;
                    }
                case OPCodeValue.OP_rot3r:
                    {
                        // a b x → x a b
                        IrValue x = Pop();
                        IrValue b = Pop();
                        IrValue a = Pop();
                        Push(x); Push(a); Push(b);
                        break;
                    }
                case OPCodeValue.OP_rot4l:
                    {
                        // x a b c → a b c x
                        IrValue c = Pop();
                        IrValue b = Pop();
                        IrValue a = Pop();
                        IrValue x = Pop();
                        Push(a); Push(b); Push(c); Push(x);
                        break;
                    }
                case OPCodeValue.OP_rot5l:
                    {
                        // x a b c d → a b c d x
                        IrValue d = Pop();
                        IrValue c = Pop();
                        IrValue b = Pop();
                        IrValue a = Pop();
                        IrValue x = Pop();
                        Push(a); Push(b); Push(c); Push(d); Push(x);
                        break;
                    }

                // ---------- 算术 ----------
                case OPCodeValue.OP_add: PushBinary("+", 11); break;
                case OPCodeValue.OP_sub: PushBinary("-", 11); break;
                case OPCodeValue.OP_mul: PushBinary("*", 12); break;
                case OPCodeValue.OP_div: PushBinary("/", 12); break;
                case OPCodeValue.OP_mod: PushBinary("%", 12); break;
                case OPCodeValue.OP_pow: PushBinary("**", 13); break;
                case OPCodeValue.OP_shl: PushBinary("<<", 10); break;
                case OPCodeValue.OP_sar: PushBinary(">>", 10); break;
                case OPCodeValue.OP_shr: PushBinary(">>>", 10); break;
                case OPCodeValue.OP_and: PushBinary("&", 7); break;
                case OPCodeValue.OP_xor: PushBinary("^", 6); break;
                case OPCodeValue.OP_or: PushBinary("|", 5); break;

                case OPCodeValue.OP_lt: PushBinary("<", 9); break;
                case OPCodeValue.OP_lte: PushBinary("<=", 9); break;
                case OPCodeValue.OP_gt: PushBinary(">", 9); break;
                case OPCodeValue.OP_gte: PushBinary(">=", 9); break;
                case OPCodeValue.OP_eq: PushBinary("==", 8); break;
                case OPCodeValue.OP_neq: PushBinary("!=", 8); break;
                case OPCodeValue.OP_strict_eq: PushBinary("===", 8); break;
                case OPCodeValue.OP_strict_neq: PushBinary("!==", 8); break;
                case OPCodeValue.OP_instanceof: PushBinary("instanceof", 9); break;
                case OPCodeValue.OP_in: PushBinary("in", 9); break;

                // ---------- 一元 ----------
                case OPCodeValue.OP_neg: PushUnary("-"); break;
                case OPCodeValue.OP_plus: PushUnary("+"); break;
                case OPCodeValue.OP_not: PushUnary("~"); break;   // OP_not 是按位取反 ~
                case OPCodeValue.OP_lnot: PushUnary("!"); break;  // OP_lnot 是逻辑非 !
                case OPCodeValue.OP_regexp:
                    {
                        // [pattern, flags] → [regexp]：pattern 是 lre 编译后的二进制字节码，
                        // 无法还原原始正则文本，输出占位
                        Pop(); Pop();
                        PushConst("/<regexp>/ /* regexp 字面量（lre 字节码无法还原） */");
                        break;
                    }
                case OPCodeValue.OP_typeof: PushUnary("typeof"); break;
                case OPCodeValue.OP_await: PushUnary("await"); break;
                case OPCodeValue.OP_is_undefined:
                    {
                        IrValue v = Pop();
                        Push(new IrBinaryOp("===", v, new IrConstant("undefined") { Id = func.AllocValueId() }, 8) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_is_null:
                    {
                        IrValue v = Pop();
                        Push(new IrBinaryOp("===", v, new IrConstant("null") { Id = func.AllocValueId() }, 8) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_typeof_is_function:
                    {
                        IrValue v = Pop();
                        Push(new IrBinaryOp("===",
                            new IrUnaryOp("typeof", v) { Id = func.AllocValueId() },
                            new IrConstant("\"function\"") { Id = func.AllocValueId() }, 8)
                        { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_typeof_is_undefined:
                    {
                        IrValue v = Pop();
                        Push(new IrBinaryOp("===",
                            new IrUnaryOp("typeof", v) { Id = func.AllocValueId() },
                            new IrConstant("\"undefined\"") { Id = func.AllocValueId() }, 8)
                        { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_is_undefined_or_null:
                    {
                        // ?? 运算符用：v == null（undefined 或 null）
                        IrValue v = Pop();
                        Push(new IrBinaryOp("==", v, new IrConstant("null") { Id = func.AllocValueId() }, 8)
                        { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_inc:
                    {
                        IrValue v = Pop();
                        Push(new IrBinaryOp("+", v, new IrConstant("1") { Id = func.AllocValueId() }, 11) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_dec:
                    {
                        IrValue v = Pop();
                        Push(new IrBinaryOp("-", v, new IrConstant("1") { Id = func.AllocValueId() }, 11) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_post_inc:
                case OPCodeValue.OP_post_dec:
                    {
                        // 栈效应 1/2：a → a, a±1（旧值在下，新值在顶）
                        // 权威：quickjs.c js_post_inc_slow（12902-12917）
                        IrValue v = Peek();
                        string deltaOp = (op == OPCodeValue.OP_post_inc) ? "+" : "-";
                        Push(new IrBinaryOp(deltaOp, v, new IrConstant("1") { Id = func.AllocValueId() }, 11) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_inc_loc:
                    AssignVar(func.GetLocName(((QuickJsOperandLoc8)operand).LocIndex),
                        new IrBinaryOp("+", Var(func.GetLocName(((QuickJsOperandLoc8)operand).LocIndex)), new IrConstant("1") { Id = func.AllocValueId() }, 11) { Id = func.AllocValueId() }, CurrentPc);
                    break;
                case OPCodeValue.OP_dec_loc:
                    AssignVar(func.GetLocName(((QuickJsOperandLoc8)operand).LocIndex),
                        new IrBinaryOp("-", Var(func.GetLocName(((QuickJsOperandLoc8)operand).LocIndex)), new IrConstant("1") { Id = func.AllocValueId() }, 11) { Id = func.AllocValueId() }, CurrentPc);
                    break;
                case OPCodeValue.OP_add_loc:
                    {
                        string name = func.GetLocName(((QuickJsOperandLoc8)operand).LocIndex);
                        IrValue v = Pop();
                        AssignVar(name, new IrBinaryOp("+", Var(name), v, 11) { Id = func.AllocValueId() }, CurrentPc);
                        break;
                    }

                // ---------- 调用 ----------
                case OPCodeValue.OP_call:
                    LiftCall(((QuickJsOperandNPop)operand).NPop, CallKind.Normal, false);
                    break;
                case OPCodeValue.OP_call0:
                case OPCodeValue.OP_call1:
                case OPCodeValue.OP_call2:
                case OPCodeValue.OP_call3:
                    LiftCall(op - OPCodeValue.OP_call0, CallKind.Normal, false);
                    break;
                case OPCodeValue.OP_tail_call:
                    LiftCall(((QuickJsOperandNPop)operand).NPop, CallKind.Normal, true);
                    break;
                case OPCodeValue.OP_call_method:
                    LiftCall(((QuickJsOperandNPop)operand).NPop, CallKind.Method, false);
                    break;
                case OPCodeValue.OP_tail_call_method:
                    LiftCall(((QuickJsOperandNPop)operand).NPop, CallKind.Method, true);
                    break;
                case OPCodeValue.OP_call_constructor:
                    LiftCall(((QuickJsOperandNPop)operand).NPop, CallKind.Constructor, false);
                    break;
                case OPCodeValue.OP_eval:
                    LiftCall(((QuickJsOperandNPopU16)operand).NPop, CallKind.Normal, false);
                    break;

                // ---------- 对象/数组构建 ----------
                case OPCodeValue.OP_object:
                    Push(new IrLiteralContainer { IsArray = false, Id = func.AllocValueId() });
                    break;
                case OPCodeValue.OP_define_class:
                case OPCodeValue.OP_define_class_computed:
                    {
                        // [parent, ctorFn] → [ctor, proto]（computed 的名字在更底下，保持不动）
                        // class_flags bit0 = JS_DEFINE_CLASS_HAS_HERITAGE
                        IrValue ctorFn = Pop();
                        IrValue parent = Pop();
                        string? className;
                        int cflags;
                        if (op == OPCodeValue.OP_define_class)
                        {
                            var atomOp = (QuickJsOperandAtomU8)operand;
                            className = AtomName(atomOp);
                            cflags = atomOp.U8;
                        }
                        else
                        {
                            className = null; // 计算名：名字值留在栈底
                            cflags = ((QuickJsOperandU8)operand).Value;
                        }
                        if (string.IsNullOrEmpty(className)) className = null;
                        var cls = new IrClassValue
                        {
                            Id = func.AllocValueId(),
                            Name = className,
                            Ctor = ctorFn,
                            Parent = parent,
                            HasHeritage = (cflags & 1) != 0
                        };
                        cls.Proto.Id = func.AllocValueId();
                        cls.StaticItems.Id = func.AllocValueId();
                        Push(cls);
                        Push(cls.Proto); // 后续 define_method/define_field 填充原型
                        break;
                    }
                case OPCodeValue.OP_array_from:
                    {
                        int argc = ((QuickJsOperandNPop)operand).NPop;
                        var arr = new IrLiteralContainer { IsArray = true, Id = func.AllocValueId() };
                        List<IrValue> items = PopArgs(argc);
                        foreach (var it in items)
                            arr.Items.Add(new IrContainerItem { Value = it });
                        Push(arr);
                        break;
                    }
                case OPCodeValue.OP_define_field:
                    {
                        // [obj, val] → [obj]
                        IrValue val = Pop();
                        IrValue obj = Peek();
                        string name = AtomName((QuickJsOperandAtom)operand);
                        if (obj is IrLiteralContainer container)
                        {
                            if (!container.IsArray)
                            {
                                container.Items.Add(new IrContainerItem { KeyName = name, Value = val });
                            }
                            else
                            {
                                // 稀疏数组字面量：[1,2,3,,5] 的 5 是 define_field "4"
                                if (int.TryParse(name, out int idx))
                                {
                                    while (container.Items.Count < idx)
                                        container.Items.Add(new IrContainerItem { Value = new IrConstant("/*hole*/") { Id = func.AllocValueId() } });
                                    container.Items.Add(new IrContainerItem { Value = val });
                                }
                            }
                        }
                        else if (obj is IrClassValue cls2)
                        {
                            // 静态字段：define_field 直接作用在 ctor 上
                            cls2.StaticItems.Items.Add(new IrContainerItem { KeyName = name, Value = val });
                        }
                        else
                        {
                            block.Statements.Add(new IrExprStatement(
                                new IrBinaryOp("=", new IrGetProperty(obj, name) { Id = func.AllocValueId() }, val, 2) { Id = func.AllocValueId() }) { Pc = CurrentPc });
                        }
                        break;
                    }
                case OPCodeValue.OP_put_field:
                    {
                        // [obj, val] → []
                        IrValue val = Pop();
                        IrValue obj = Pop();
                        string name = AtomName((QuickJsOperandAtom)operand);
                        MarkConsumed(val);
                        block.Statements.Add(new IrExprStatement(
                            new IrBinaryOp("=", new IrGetProperty(obj, name) { Id = func.AllocValueId() }, val, 2) { Id = func.AllocValueId() }) { Pc = CurrentPc });
                        break;
                    }
                case OPCodeValue.OP_delete:
                    {
                        // [obj, prop] → [bool]（none 格式，属性名在栈上）
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        Push(new IrUnaryOp("delete", new IrGetProperty(obj, prop) { Id = func.AllocValueId() }) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_delete_var:
                    {
                        // atom 格式：[ ] → [bool]
                        Push(new IrUnaryOp("delete", Var(AtomName((QuickJsOperandAtom)operand))) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_copy_data_properties:
                    {
                        // u8 mask：[target, source, excluded] → 栈不变（3/3）
                        // target = sp[-1-(mask&3)], source = sp[-1-((mask>>2)&7)]
                        int mask = ((QuickJsOperandU8)operand).Value;
                        int targetOff = mask & 3;
                        int sourceOff = (mask >> 2) & 7;
                        int targetIdx = stack.Count - 1 - targetOff;
                        int sourceIdx = stack.Count - 1 - sourceOff;
                        if (targetIdx >= 0 && sourceIdx >= 0
                            && stack[targetIdx] is IrLiteralContainer container)
                        {
                            // rest 解构的排除键是 define_field 压入的 null 占位，展开前清除
                            container.Items.RemoveAll(it => it.Value is IrConstant c && c.Text == "null");
                            container.Items.Add(new IrContainerItem { IsSpread = true, Value = stack[sourceIdx] });
                        }
                        break;
                    }
                case OPCodeValue.OP_make_var_ref:
                    {
                        // atom，0/2：push [env, name]（变量引用二元组）
                        string name = AtomName((QuickJsOperandAtom)operand);
                        Push(new IrConstant("undefined /*env*/") { Id = func.AllocValueId() });
                        Push(new IrConstant(QuoteString(name)) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_make_loc_ref:
                case OPCodeValue.OP_make_var_ref_ref:
                    {
                        // atom_u16：push [env, name]（局部变量/闭包变量引用，配合 put_ref_value 写回）
                        string name = AtomName((QuickJsOperandAtomU16)operand);
                        Push(new IrConstant("undefined /*env*/") { Id = func.AllocValueId() });
                        Push(new IrConstant(QuoteString(name)) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_put_ref_value:
                    {
                        // [env, name, value] → []：通过引用写变量
                        IrValue value = Pop();
                        IrValue nameV = Pop();
                        Pop(); // env
                        string name = nameV.Emit().Trim('"');
                        AssignVar(name, value, CurrentPc);
                        break;
                    }
                case OPCodeValue.OP_get_ref_value:
                    {
                        // [env, name] → [env, name, value]：通过引用读变量
                        IrValue nameV = Peek();
                        Push(Var(nameV.Emit().Trim('"')));
                        break;
                    }
                case OPCodeValue.OP_define_array_el:
                    {
                        // [arr, idx, val] → [arr, idx]
                        IrValue val = Pop();
                        IrValue arr = stack.Count >= 2 ? stack[stack.Count - 2] : Peek();
                        if (arr is IrLiteralContainer container)
                        {
                            if (container.IsArray)
                            {
                                container.Items.Add(new IrContainerItem { Value = val });
                            }
                            else
                            {
                                // 对象字面量的计算键 {[key]: val}：栈上 idx 即键表达式，
                                // 后续会有 drop 把它弹掉，这里保持栈不变
                                IrValue key = Peek();
                                container.Items.Add(new IrContainerItem { KeyExpr = key, Value = val });
                            }
                        }
                        break;
                    }
                case OPCodeValue.OP_put_array_el:
                    {
                        // [obj, prop, val] → []
                        IrValue val = Pop();
                        IrValue prop = Pop();
                        IrValue obj = Pop();
                        MarkConsumed(val);
                        block.Statements.Add(new IrExprStatement(
                            new IrBinaryOp("=", new IrGetProperty(obj, prop) { Id = func.AllocValueId() }, val, 2) { Id = func.AllocValueId() }) { Pc = CurrentPc });
                        break;
                    }
                case OPCodeValue.OP_define_method:
                case OPCodeValue.OP_define_method_computed:
                    {
                        // define_method:          [obj, func] → [obj]（atom 操作数带名字）
                        // define_method_computed: [obj, name, func] → [obj]（名字在栈上）
                        // u8 flags：0=method 1=getter 2=setter
                        IrValue fn = Pop();
                        IrValue? computedName = null;
                        string name = "?";
                        int kind = 0;
                        if (op == OPCodeValue.OP_define_method)
                        {
                            var atomOp = (QuickJsOperandAtomU8)operand;
                            name = AtomName(atomOp);
                            kind = atomOp.U8 & 3;
                        }
                        else
                        {
                            computedName = Pop();
                            kind = ((QuickJsOperandU8)operand).Value & 3;
                        }
                        if (kind == 1) name = "get " + name;
                        else if (kind == 2) name = "set " + name;
                        IrValue obj = Peek();
                        if (obj is IrLiteralContainer container && !container.IsArray)
                        {
                            if (computedName != null)
                                container.Items.Add(new IrContainerItem { KeyExpr = computedName, Value = fn });
                            else
                                container.Items.Add(new IrContainerItem { KeyName = name, Value = fn });
                        }
                        else if (obj is IrClassValue cls)
                        {
                            // 静态方法：define_method 直接作用在 ctor 上
                            if (computedName != null)
                                cls.StaticItems.Items.Add(new IrContainerItem { KeyExpr = computedName, Value = fn });
                            else
                                cls.StaticItems.Items.Add(new IrContainerItem { KeyName = name, Value = fn });
                        }
                        break;
                    }
                case OPCodeValue.OP_append:
                    {
                        // [array, pos, enumobj] → [array, pos]：展开 ...x
                        IrValue enumObj = Pop();
                        IrValue arr = stack.Count >= 2 ? stack[stack.Count - 2] : Peek();
                        if (arr is IrLiteralContainer container && container.IsArray)
                            container.Items.Add(new IrContainerItem { IsSpread = true, Value = SpreadOf(enumObj) });
                        break;
                    }

                // ---------- 控制流（块终结指令在此只处理栈效应） ----------
                case OPCodeValue.OP_if_true:
                case OPCodeValue.OP_if_false:
                case OPCodeValue.OP_if_true8:
                case OPCodeValue.OP_if_false8:
                    {
                        IrValue cond = Pop();
                        // 条件求值已发生（作为跳转条件被消费）：标记后其残留副本
                        // （dup 幸存者、汇合折叠产物）再次被 drop 时不再重复物化，
                        // 否则 getter/读取会执行两次（如 `tmp0 && cond && ...` 垃圾语句）
                        MarkConsumed(cond);
                        // 条件跳之后仍留在栈上的值会同时流向两个后继块（dup 短路模板、
                        // hole-check 模板等）：任一分支里 drop 这些实例都只是丢弃该路径的
                        // 副本（另一路径的汇合会保留它），不应物化为噪音语句
                        foreach (IrValue v in stack)
                            shortCircuitSurvivors.Add(v);
                        block.Condition = cond;
                        break;
                    }

                case OPCodeValue.OP_goto:
                case OPCodeValue.OP_goto8:
                case OPCodeValue.OP_goto16:
                    break; // 无栈效应

                case OPCodeValue.OP_return:
                    {
                        IrValue retVal = Pop();
                        FlushStackSideEffects();
                        MarkConsumed(retVal);
                        block.Statements.Add(new IrReturn { Value = retVal, Pc = CurrentPc });
                        break;
                    }
                case OPCodeValue.OP_return_undef:
                    FlushStackSideEffects();
                    block.Statements.Add(new IrReturn { Value = null, Pc = CurrentPc });
                    break;
                case OPCodeValue.OP_return_async:
                    {
                        IrValue retVal = Pop();
                        FlushStackSideEffects();
                        block.Statements.Add(new IrReturn { Value = retVal, Pc = CurrentPc });
                        break;
                    }
                case OPCodeValue.OP_throw:
                    {
                        IrValue throwVal = Pop();
                        FlushStackSideEffects();
                        MarkConsumed(throwVal);
                        block.Statements.Add(new IrThrow { Value = throwVal, Pc = CurrentPc });
                        break;
                    }
                case OPCodeValue.OP_throw_error:
                    block.Statements.Add(new IrThrow { Value = null, Pc = CurrentPc });
                    break;

                case OPCodeValue.OP_catch:
                    {
                        long target = BasicBlockPass.GetJumpTarget(ins) ?? 0;
                        Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.CatchOffset, target) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_gosub:
                    {
                        long retAddr = ins.getPC() + ins.getOpCode().Size;
                        Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.ReturnAddress, retAddr) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_ret:
                    Pop(); // 返回地址
                    block.Statements.Add(new IrRawLine("// ret (finally 块返回)") { Pc = CurrentPc });
                    break;

                // ---------- for-in / for-of ----------
                case OPCodeValue.OP_for_in_start:
                    {
                        IrValue obj = Pop();
                        MarkConsumed(obj);
                        // 记录迭代表达式供 StructurePass 重组 for-in 循环
                        block.ForOfIterable = obj;
                        block.ForOfIsForIn = true;
                        Push(Var("for_in_enum")); // 枚举对象占位
                        break;
                    }
                case OPCodeValue.OP_for_in_next:
                    {
                        // [enum] → [enum, value, done]
                        Push(new IrIteratorPlaceholder(IrIteratorPlaceholder.Kind.InValue) { Id = func.AllocValueId() });
                        Push(new IrIteratorPlaceholder(IrIteratorPlaceholder.Kind.InDone) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_for_of_start:
                    {
                        IrValue obj = Pop();
                        MarkConsumed(obj);
                        // 记录迭代表达式供 StructurePass 重组 for-of 循环；
                        // 直线型数组解构则靠这条标记语句重组 [a, b] = expr
                        block.ForOfIterable = obj;
                        block.ForOfIsForIn = false;
                        block.Statements.Add(new IrIteratorStart(obj) { Pc = CurrentPc });
                        Push(Var("for_of_iter"));
                        Push(Var("for_of_next_fn"));
                        Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.IteratorGuard, 0) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_for_of_next:
                    {
                        Push(new IrIteratorPlaceholder(IrIteratorPlaceholder.Kind.OfValue) { Id = func.AllocValueId() });
                        Push(new IrIteratorPlaceholder(IrIteratorPlaceholder.Kind.OfDone) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_iterator_close:
                    Pop(); Pop(); Pop();
                    break;
                case OPCodeValue.OP_iterator_get_value_done:
                    {
                        IrValue res = Pop();
                        Push(new IrGetProperty(res, "value") { Id = func.AllocValueId() });
                        Push(new IrGetProperty(res, "done") { Id = func.AllocValueId() });
                        break;
                    }

                // ---------- 序言/无害检查 ----------
                case OPCodeValue.OP_check_ctor:
                case OPCodeValue.OP_to_propkey:
                case OPCodeValue.OP_to_propkey2:
                case OPCodeValue.OP_to_object:
                case OPCodeValue.OP_close_loc:
                    break;
                case OPCodeValue.OP_check_ctor_return:
                    {
                        // [val] → [val, this?]：简化保留 val
                        break;
                    }
                case OPCodeValue.OP_special_object:
                    {
                        // u8：0/1=arguments 2=当前函数 3=new.target 4=home object 5=var object 6=import.meta
                        int kind = ((QuickJsOperandU8)operand).Value;
                        switch (kind)
                        {
                            case 2:
                                // this_func 伪变量：类型标记（不用名字匹配），put_loc 时登记槽位
                                Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.ThisFunc, 0) { Id = func.AllocValueId() });
                                break;
                            case 4:
                                Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.HomeObject, 0) { Id = func.AllocValueId() });
                                break;
                            default:
                                PushConst(kind switch
                                {
                                    0 or 1 => "arguments",
                                    3 => "new.target",
                                    5 => "/*var_object*/ undefined",
                                    6 => "import.meta",
                                    _ => "undefined /*special_object*/"
                                });
                                break;
                        }
                        break;
                    }
                case OPCodeValue.OP_get_super:
                    {
                        // [obj] → [proto]：obj 是 home_object（super.x）或 this_func（super(...)）伪变量
                        IrValue obj = Pop();
                        if (obj is IrSpecialMarker sm && sm.Kind == IrSpecialMarker.MarkerKind.HomeObject)
                            Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.SuperProto, 0) { Id = func.AllocValueId() });
                        else if (obj is IrSpecialMarker sm2 && sm2.Kind == IrSpecialMarker.MarkerKind.ThisFunc)
                            Push(new IrSpecialMarker(IrSpecialMarker.MarkerKind.SuperCtor, 0) { Id = func.AllocValueId() });
                        else
                            Push(new IrGetProperty(obj, "__proto__") { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_get_super_value:
                    {
                        // [this, proto, key] → [value]：super.key 读取（receiver=this 已隐含在 super 语义中）
                        IrValue key = Pop();
                        IrValue proto = Pop();
                        Pop(); // this
                        Push(new IrGetProperty(proto, key) { Id = func.AllocValueId() });
                        break;
                    }
                case OPCodeValue.OP_put_super_value:
                    {
                        // [this, proto, key, val] → []：super.key = val
                        IrValue val = Pop();
                        IrValue key = Pop();
                        IrValue proto = Pop();
                        Pop(); // this
                        MarkConsumed(val);
                        block.Statements.Add(new IrExprStatement(
                            new IrBinaryOp("=", new IrGetProperty(proto, key) { Id = func.AllocValueId() }, val, 2) { Id = func.AllocValueId() }) { Pc = CurrentPc });
                        break;
                    }
                case OPCodeValue.OP_rest:
                    PushConst("[...rest]");
                    break;

                // ---------- 兜底 ----------
                default:
                    Fallback(ins);
                    break;
            }
        }

        // ================= 辅助构造 =================

        enum CallKind { Normal, Method, Constructor }

        private void LiftCall(int argc, CallKind kind, bool isTailCall)
        {
            // 栈布局（底→顶）：
            //   Normal:      [func, arg0..argN-1]
            //   Method:      [this, func, arg0..argN-1]   ← this 在 func 之下（权威）
            //   Constructor: [ctor, new.target, args...]
            List<IrValue> args = PopArgs(argc);
            IrValue? thisArg = null;
            IrValue funcVal;

            if (kind == CallKind.Method)
            {
                funcVal = Pop();
                thisArg = Pop();
            }
            else if (kind == CallKind.Constructor)
            {
                Pop(); // new.target
                funcVal = Pop();
            }
            else
            {
                funcVal = Pop();
            }

            var call = new IrCall(funcVal, thisArg, kind == CallKind.Constructor) { Id = func.AllocValueId() };
            call.Args.AddRange(args);

            if (isTailCall)
            {
                block.Statements.Add(new IrReturn { Value = call, Pc = CurrentPc });
            }
            else
            {
                Push(call);
            }
        }

        /// <summary>弹出 argc 个参数并按正序返回（栈顶是最后一个参数）</summary>
        private List<IrValue> PopArgs(int argc)
        {
            List<IrValue> args = new List<IrValue>(argc);
            for (int i = 0; i < argc; i++)
                args.Add(Pop());
            args.Reverse();
            return args;
        }


        /// <summary>atom 名提取：tagged int atom（属性名是数字）直接显示数值</summary>
        private static string AtomName(QuickJsOperandAtom op)
        {
            if (op.AtomIndex.IsTaggedInt) return op.AtomIndex.Value.ToString();
            return (op.AtomValue?.Value ?? "?").TrimEnd('\0');
        }

        private static string AtomName(QuickJsOperandAtomU8 op)
        {
            if (op.AtomIndex.IsTaggedInt) return op.AtomIndex.Value.ToString();
            return (op.AtomValue?.Value ?? "?").TrimEnd('\0');
        }

        private static string AtomName(QuickJsOperandAtomU16 op)
        {
            if (op.AtomIndex.IsTaggedInt) return op.AtomIndex.Value.ToString();
            return (op.AtomValue?.Value ?? "?").TrimEnd('\0');
        }

        private void PushConst(string text) => Push(new IrConstant(text) { Id = func.AllocValueId() });

        private IrVariable Var(string name) => new IrVariable(name) { Id = func.AllocValueId() };

        private void PushVar(string name) => Push(Var(name));

        private void PushClosure(JsFunctionBytecode? fn)
        {
            if (fn != null)
                Push(new IrClosureValue(fn) { Id = func.AllocValueId() });
            else
                PushConst("null /*closure*/");
        }

        private void PushConstPool(uint index)
        {
            if (index < func.Bytecode.CPool.Count)
            {
                JsObject.Types.Objects.JsObject c = func.Bytecode.CPool[(int)index];
                switch (c)
                {
                    case JsString s:
                        PushConst(QuoteString(s.Value));
                        return;
                    case JsInt32 i32:
                        PushConst(i32.Value.ToString());
                        return;
                    case JsFloat64 f64:
                        PushConst(FormatDouble(f64.Value));
                        return;
                    case JsBigInt bi:
                        PushConst(bi.ToString());
                        return;
                    case JsFunctionBytecode fb:
                        PushClosure(fb);
                        return;
                    default:
                        PushConst(c.ToString());
                        return;
                }
            }
            PushConst("undefined /*cpool?*/");
        }

        private static string QuoteString(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        /// <summary>double → JS 字面量（.NET 的 ∞/NaN 要转为 JS 合法写法）</summary>
        private static string FormatDouble(double d)
        {
            if (double.IsNaN(d)) return "NaN";
            if (double.IsPositiveInfinity(d)) return "Infinity";
            if (double.IsNegativeInfinity(d)) return "-Infinity";
            return d.ToString("R");
        }

        private static IrValue SpreadOf(IrValue v)
        {
            // 用一元伪运算表达展开，打印为 ...x 由 container 的 key 标记
            return v;
        }

        private void PushBinary(string symbol, int precedence)
        {
            IrValue right = Pop();
            IrValue left = Pop();
            Push(new IrBinaryOp(symbol, left, right, precedence) { Id = func.AllocValueId() });
        }

        private void PushUnary(string symbol)
        {
            IrValue v = Pop();
            Push(new IrUnaryOp(symbol, v) { Id = func.AllocValueId() });
        }

        private void SetVarKeepStack(string name)
        {
            IrValue v = Peek();
            AssignVar(name, v, CurrentPc);
            // 栈顶替换为变量引用，避免副作用值被后续重复内联
            stack[stack.Count - 1] = Var(name);
        }

        /// <summary>未知指令兜底：按 opcode 表的 pop/push 数保持栈平衡</summary>
        private void Fallback(QuickJsInstruction ins)
        {
            QuickJsOPCode code = ins.getOpCode();
            List<IrValue> args = PopArgs(code.PopCount);

            block.Statements.Add(new IrRawLine("// unsupported: " + ins.ToString()) { Pc = CurrentPc });

            for (int i = 0; i < code.PushCount; i++)
            {
                string text = "OP_" + code.Name + "(" + string.Join(", ", args.Select(a => a.Emit())) + ")[" + i + "]";
                Push(new IrConstant(text) { Id = func.AllocValueId() });
            }
        }
    }
}
