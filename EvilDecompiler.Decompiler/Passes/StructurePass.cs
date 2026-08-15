using EvilDecompiler.Decompiler.IR;
using static EvilDecompiler.ByteCode.Type.QuickJsOPCode;

namespace EvilDecompiler.Decompiler.Passes
{
    /// <summary>
    /// Pass 4: 控制流结构化。把扁平的基本块列表（CFG）识别为嵌套的
    /// if / if-else / while / do-while 结构，无法识别的跳转兜底为标签注释。
    ///
    /// QuickJS 编译器产物的典型形状（块按 pc 连续分布）：
    ///   if:        B: if_false T  →  then=[B+1, T-1]，T 是汇合点
    ///   if-else:   B: if_false E  →  then=[B+1, L]，L: goto X；else=[E, X-1]
    ///   while:     H: if_false X  →  body=[H+1, K]，K: goto H，X=K+1 是出口
    ///   do-while:  末块条件跳回区域首块（if_true→H 或 if_false→H，for-of 就是后者）
    ///   break:     循环体内 goto 循环出口
    ///   continue:  循环体内 goto 循环头
    /// </summary>
    public class StructurePass : IFunctionPass
    {
        public string Name => "Structure";

        private IrFunction func = null!;
        public List<IrStatement> Result = new List<IrStatement>();

        private struct LoopCtx
        {
            public int HeaderIndex;    // 循环头块（continue 目标之一）
            public int ExitIndex;      // 循环出口块（break 目标）
            public int ContinueIndex;  // continue 实际跳转目标（for 的增量块/回边块）
            public int NaturalBackEdge; // 自然回边块（循环体最后的 goto→头，不输出 continue）
        }

        public void Run(IrFunctionContext ctx)
        {
            func = ctx.Function;
            absorbedBlocks.Clear();
            Result = StructureRegion(0, func.Blocks.Count - 1, new List<LoopCtx>());
        }

        /// <summary>结构化 [startIdx, endIdx] 块区间；regionExit = 区域外允许的汇合块（if-else 内嵌循环的出口可超出区域）</summary>
        private List<IrStatement> StructureRegion(int startIdx, int endIdx, List<LoopCtx> loops, int regionExit = -1)
        {
            List<IrStatement> output = new List<IrStatement>();
            List<IrBlock> blocks = func.Blocks;

            if (startIdx > endIdx || startIdx < 0 || endIdx >= blocks.Count)
                return output;

            // 块 index → 其语句在 output 中的起始下标（do-while 回溯收编用）
            Dictionary<int, int> blockOutputStart = new Dictionary<int, int>();

            int i = startIdx;
            while (i <= endIdx)
            {
                IrBlock b = blocks[i];
                if (absorbedBlocks.Contains(i))
                {
                    i++;
                    continue;
                }
                blockOutputStart[i] = output.Count;

                // ---- try/catch/finally 识别（catch 块有异常边 CatchTarget） ----
                if (b.CatchTarget != null && b.CatchTarget.Index > i)
                {
                    if (TryStructureTryCatch(i, endIdx, loops, output, out int tcNext))
                    {
                        i = tcNext;
                        continue;
                    }
                }

                // ---- for-of / for-in 循环识别 ----
                // 形状：A: ...; for_of_start; goto C   B(=A+1..C-1): 循环体（首条语句 v = for_of_value）
                //       C: for_of_next; if_false B      C+1: 出口（drop; iterator_close）
                if (b.Terminator == BlockTerminator.Jump && b.JumpTarget != null
                    && b.ForOfIterable != null)
                {
                    int c = b.JumpTarget.Index;
                    if (c > i + 1 && c + 1 <= endIdx + 1)
                    {
                        IrBlock condBlock = blocks[c];
                        OPCodeValue nextOp = condBlock.Instructions.Count > 0
                            ? condBlock.Instructions[0].getOpCode().OPCode : 0;
                        bool wantOp = b.ForOfIsForIn ? nextOp == OPCodeValue.OP_for_in_next
                            : nextOp == OPCodeValue.OP_for_of_next;
                        if (wantOp
                            && condBlock.Terminator == BlockTerminator.CondJump
                            && condBlock.JumpTarget != null
                            && condBlock.JumpTarget.Index > i && condBlock.JumpTarget.Index < c)
                        {
                            IrBlock bodyFirst = blocks[condBlock.JumpTarget.Index];
                            if (TryExtractForOfVar(bodyFirst, b.ForOfIsForIn, out string? varName))
                            {
                                // 摘除 for_of_start 的标记语句（循环已重组，不再需要）
                                b.Statements.RemoveAll(s => s is IrIteratorStart);
                                output.AddRange(b.Statements);
                                var forOf = new IrForOf
                                {
                                    Pc = b.EndPc,
                                    VarName = varName!,
                                    Iterable = b.ForOfIterable,
                                    IsForIn = b.ForOfIsForIn
                                };
                                // continue → 条件块 C（取下一个值）；break → 出口块 C+1
                                var newLoops = new List<LoopCtx>(loops) { new LoopCtx { HeaderIndex = c, ExitIndex = c + 1, ContinueIndex = c } };
                                forOf.Body = StructureRegion(condBlock.JumpTarget.Index, c - 1, newLoops);
                                output.Add(forOf);
                                i = c + 1;
                                continue;
                            }
                        }
                    }
                }

                if (b.Terminator == BlockTerminator.CondJump && b.Condition != null
                    && b.JumpTarget != null)
                {
                    // ---- switch 识别：dup 判别式 + === 常量比较链 ----
                    if (TryStructureSwitch(i, endIdx, loops, output, out int switchNext))
                    {
                        i = switchNext;
                        continue;
                    }

                    int T = b.JumpTarget.Index;

                    // ---- do-while 检测：条件后向跳回区域内某块（含自回边 T==i） ----
                    if (T >= startIdx && T <= i)
                    {
                        // 校验：回边目标块必须在当前区域且已输出
                        if (blockOutputStart.ContainsKey(T))
                        {
                            int cut = blockOutputStart[T];
                            var doWhile = new IrDoWhile { Pc = b.EndPc };

                            // 收编 [T 块 .. 前一块] 的语句为循环体
                            List<IrStatement> body = output.GetRange(cut, output.Count - cut);
                            output.RemoveRange(cut, output.Count - cut);
                            doWhile.Body = body;
                            // 条件计算语句（本块的）属于循环体末尾
                            doWhile.Body.AddRange(b.Statements);
                            doWhile.Condition = b.Condition;
                            doWhile.NegateCondition = b.JumpOnFalse; // if_false 跳回 = 条件为假继续循环
                            output.Add(doWhile);
                            i++;
                            continue;
                        }
                    }

                    // ---- while 检测：前向条件跳（出循环）+ 区域内末块无条件回边 ----
                    // 注意必须先于 break/continue 检测：嵌套循环的内层出口块
                    // 可能与外层增量块是同一块，会被 break/continue 检测误吃
                    // JumpOnFalse：条件为假退出 → while(C)；JumpOnTrue：条件为真退出 → while(!C)
                    // 复合条件头（&& / || 链）：条件由多个连续条件块组成，共享同一出口
                    {
                        IrValue wCond = b.Condition;
                        bool wNegate = !b.JumpOnFalse;
                        int wBodyStart = i + 1;
                        int wExit = T;
                        if (ScanConditionChain(i, endIdx, out var wTerms, out bool wIsOr,
                                out bool wNeg, out int wThenStart, out int wE)
                            && wE <= endIdx + 1)
                        {
                            wCond = FoldConditionTerms(wTerms, wIsOr);
                            wNegate = wNeg;
                            wBodyStart = wThenStart;
                            wExit = wE;
                        }

                        if (wExit > i && (wExit <= endIdx + 1 || wExit == regionExit))
                        {
                            // 自然回边块：区域内最后一个无条件跳回本块的块。
                            // 注意回边块不一定是体末块（循环内 try 的异常尾 throw 块在回边之后），
                            // 所以体区域要覆盖整个 [wBodyStart, 出口前]，不能只切到回边块
                            int bodyEnd = Math.Min(wExit - 1, endIdx);
                            int K = -1;
                            for (int x = bodyEnd; x >= wBodyStart; x--)
                            {
                                if (blocks[x].Terminator == BlockTerminator.Jump
                                    && blocks[x].JumpTarget != null
                                    && blocks[x].JumpTarget.Index == i)
                                { K = x; break; }
                            }
                            if (K >= wBodyStart)
                            {
                                var whileStmt = new IrWhile { Pc = b.EndPc };
                                var newLoops = new List<LoopCtx>(loops) { new LoopCtx { HeaderIndex = i, ExitIndex = wExit, ContinueIndex = K, NaturalBackEdge = K } };
                                whileStmt.Body = StructureRegion(wBodyStart, bodyEnd, newLoops);

                                // 条件块语句折叠：while ((m = re.exec(s)) !== null) 这类形状中，
                                // 赋值发生在条件块里、每次迭代都执行。逆序把赋值并入条件表达式
                                // （tmp 临时变量直接替换；用户变量变成赋值表达式 m = ...），
                                // 并不完的语句用 while (true) { ...; if (退出条件) break; } 保底
                                IrValue cond = wCond;
                                List<IrStatement> leftover = new List<IrStatement>();
                                for (int si = b.Statements.Count - 1; si >= 0; si--)
                                {
                                    if (b.Statements[si] is IrAssign a && MentionsVar(cond, a.Target))
                                    {
                                        IrValue repl = func.CompilerTemps.Contains(a.Target)
                                            ? a.Value
                                            : new IrBinaryOp("=", new IrVariable(a.Target) { Id = func.AllocValueId() }, a.Value, 2) { Id = func.AllocValueId() };
                                        cond = SubstituteVar(cond, a.Target, repl);
                                    }
                                    else
                                    {
                                        leftover.Insert(0, b.Statements[si]);
                                    }
                                }

                                if (leftover.Count == 0)
                                {
                                    whileStmt.Condition = cond;
                                    whileStmt.NegateCondition = wNegate;
                                }
                                else
                                {
                                    whileStmt.Condition = new IrConstant("true") { Id = func.AllocValueId() };
                                    var guard = new IrIf { Pc = b.EndPc, Condition = cond };
                                    // 退出条件 = 循环条件的否定：if_false 出循环 → if (!C) break
                                    guard.NegateCondition = !wNegate;
                                    guard.ThenBody.Add(new IrBreak { Pc = b.EndPc });
                                    whileStmt.Body.InsertRange(0, leftover);
                                    whileStmt.Body.Insert(leftover.Count, guard);
                                }
                                // 空体通常是 for-of 收集循环（体是对栈上容器的填充，无语句产物）
                                if (whileStmt.Body.Count == 0)
                                    whileStmt.Body.Add(new IrRawLine("// (for-of 收集循环体，结果见容器赋值)") { Pc = b.EndPc });
                                output.Add(whileStmt);
                                i = wExit;
                                continue;
                            }
                        }
                    }

                    // ---- 循环 break/continue 检测（优先于 if 识别）----
                    // continue 可能跳到增量块（ContinueIndex）或循环头；break 跳到出口
                    {
                        bool loopJump = false;
                        for (int l = loops.Count - 1; l >= 0 && !loopJump; l--)
                        {
                            IrStatement? jumpStmt = null;
                            if (T == loops[l].ExitIndex)
                                jumpStmt = new IrBreak { Pc = b.EndPc };
                            else if (T == loops[l].HeaderIndex || T == loops[l].ContinueIndex)
                                jumpStmt = new IrContinue { Pc = b.EndPc };

                            if (jumpStmt != null)
                            {
                                output.AddRange(b.Statements);
                                var condIf = new IrIf { Pc = b.EndPc };
                                condIf.Condition = b.Condition;
                                // 注意与 if 识别相反：这里跳转 = 执行 then
                                // if_true 跳 → 条件为真执行（不取反）；if_false 跳 → 条件为假执行（取反）
                                condIf.NegateCondition = b.JumpOnFalse;
                                condIf.ThenBody.Add(jumpStmt);
                                output.Add(condIf);
                                loopJump = true;
                            }
                        }
                        if (loopJump)
                        {
                            i++;
                            continue;
                        }
                    }

                    // ---- if / if-else 检测：前向条件跳 ----
                    // 先试复合条件折叠（纯 && / || 链共享同一 else/出口）
                    IrValue ifCond = b.Condition;
                    bool ifNegate = !b.JumpOnFalse;
                    int bodyStart = i + 1;
                    int T2 = T;
                    if (ScanConditionChain(i, endIdx, out var iTerms, out bool iIsOr,
                            out bool iNeg, out int iThenStart, out int iE))
                    {
                        ifCond = FoldConditionTerms(iTerms, iIsOr);
                        ifNegate = iNeg;
                        bodyStart = iThenStart;
                        T2 = iE;
                    }

                    if (T2 > i && T2 <= endIdx + 1)
                    {
                        output.AddRange(b.Statements);
                        int thenEnd = T2 - 1;
                        IrBlock? thenLast = (thenEnd >= bodyStart) ? blocks[thenEnd] : null;

                        // if-else：then 末块无条件跳到 T2 之后
                        if (thenLast != null
                            && thenLast.Terminator == BlockTerminator.Jump
                            && thenLast.JumpTarget != null
                            && thenLast.JumpTarget.Index > T2
                            && thenLast.JumpTarget.Index <= endIdx + 1)
                        {
                            int E = thenLast.JumpTarget.Index;

                            // ---- hole-check 模板折叠 ----
                            // QuickJS 数组解构的形状：if (源===undefined) 走 else 求值真实源
                            // 再跳回 then 起点。特征：else 区末块无条件跳回 then 区起点。
                            // 语义上等于无条件顺序执行，丢弃 if。
                            IrBlock elseLast = blocks[E - 1];
                            if (elseLast.Terminator == BlockTerminator.Jump
                                && elseLast.JumpTarget != null
                                && elseLast.JumpTarget.Index == bodyStart)
                            {
                                // else 区中间块语句（源表达式求值）
                                for (int x = T2; x < E - 1; x++)
                                    output.AddRange(blocks[x].Statements);
                                output.AddRange(elseLast.Statements);
                                // then 区（真正的逻辑）
                                output.AddRange(StructureRegion(bodyStart, thenEnd - 1, loops));
                                output.AddRange(thenLast.Statements);
                                i = E;
                                continue;
                            }

                            var ifStmt = new IrIf { Pc = b.EndPc };
                            ifStmt.Condition = ifCond;
                            ifStmt.NegateCondition = ifNegate;
                            ifStmt.ThenBody = StructureRegion(bodyStart, thenEnd - 1, loops);
                            ifStmt.ThenBody.AddRange(thenLast.Statements);
                            ifStmt.ElseBody = StructureRegion(T2, E - 1, loops);
                            // 表达式级三元/短路（then/else 末块出口栈带值流向汇合点被消费）
                            // 不是语句级 if-else：双体皆空且有值流出 → 不生成语句
                            // （SSA 层已把汇合值折叠为三元表达式，由汇合块消费）
                            if (ifStmt.ThenBody.Count == 0 && ifStmt.ElseBody.Count == 0
                                && (HasRealValue(thenLast.ExitStack) || HasRealValue(blocks[E - 1].ExitStack)))
                            {
                                i = E;
                                continue;
                            }
                            output.Add(ifStmt);
                            i = E;
                            continue;
                        }

                        // ---- if-else 扩展：then 末块非前向跳（then 以循环收尾，
                        // 循环出口 = 整个 if-else 的汇合点，位于 else 之后）----
                        // 扫描 then 区内部前向边，取超出 T2 的最大目标作为汇合点
                        {
                            int mergeE = -1;
                            for (int x = bodyStart; x <= thenEnd && x <= endIdx; x++)
                            {
                                IrBlock bx = blocks[x];
                                if (bx.JumpTarget == null) continue;
                                int tt = bx.JumpTarget.Index;
                                if (tt <= T2) continue;
                                // 排除 break/continue（目标是外层循环出口/头的边）
                                bool isLoopJump = false;
                                foreach (var l in loops)
                                    if (tt == l.ExitIndex || tt == l.HeaderIndex || tt == l.ContinueIndex)
                                    { isLoopJump = true; break; }
                                if (!isLoopJump && tt > mergeE)
                                    mergeE = tt;
                            }
                            // 验证：else 区 [T2, mergeE-1] 内不能有跳出 mergeE 之外的边
                            if (mergeE > T2 && mergeE <= endIdx + 1)
                            {
                                bool elseEscapes = false;
                                for (int x = T2; x < mergeE && x <= endIdx; x++)
                                {
                                    IrBlock bx = blocks[x];
                                    if (bx.JumpTarget != null && bx.JumpTarget.Index > mergeE)
                                    { elseEscapes = true; break; }
                                }
                                if (!elseEscapes)
                                {
                                    var ifStmt2 = new IrIf { Pc = b.EndPc };
                                    ifStmt2.Condition = ifCond;
                                    ifStmt2.NegateCondition = ifNegate;
                                    // 区域内的循环允许把出口指到汇合点（regionExit = mergeE）
                                    ifStmt2.ThenBody = StructureRegion(bodyStart, thenEnd, loops, mergeE);
                                    ifStmt2.ElseBody = StructureRegion(T2, mergeE - 1, loops, mergeE);
                                    output.Add(ifStmt2);
                                    i = mergeE;
                                    continue;
                                }
                            }
                        }

                        // 纯 if
                        {
                            var ifStmt = new IrIf { Pc = b.EndPc };
                            ifStmt.Condition = ifCond;
                            ifStmt.NegateCondition = ifNegate;
                            ifStmt.ThenBody = StructureRegion(bodyStart, thenEnd, loops);
                            // 表达式级短路（dup; if_x 的幸存值流到汇合点被消费）不是语句级 if：
                            // 条件块/then 末块出口栈有值流出且 then 体为空 → 不生成语句
                            // （SSA 层已把汇合值折叠为 && / || 表达式，由汇合点消费）
                            if (ifStmt.ThenBody.Count == 0
                                && (HasRealValue(b.ExitStack) || HasRealValue(blocks[thenEnd].ExitStack)))
                            {
                                i = T2;
                                continue;
                            }
                            output.Add(ifStmt);
                            i = T2;
                            continue;
                        }
                    }

                    // ---- guard 子句：出口超出当前区域（跳到外层汇合点）----
                    // 形状：if (c) goto L; [then 体直到区域末]; 区域末块 goto L
                    // 仅当区域末块以 goto→T2 收尾时按纯 if 收编（then = [bodyStart, endIdx]）
                    if (T2 > endIdx + 1)
                    {
                        IrBlock trailer = blocks[endIdx];
                        IrBlock? consumeBlock = null;
                        bool trailerOk = trailer.Terminator == BlockTerminator.Jump
                            && trailer.JumpTarget != null && trailer.JumpTarget.Index == T2;
                        if (!trailerOk && trailer.Terminator == BlockTerminator.CondJump
                            && bodyStart > endIdx && endIdx + 1 < blocks.Count)
                        {
                            // 区域末块是条件链的一部分（外层 if-else 把 then 末块剥离到区域外）：
                            // 真正的 trailer 是区域外下一块；其语句在此收编并清空，避免外层重复输出
                            IrBlock nb = blocks[endIdx + 1];
                            if (nb.Terminator == BlockTerminator.Jump
                                && nb.JumpTarget != null && nb.JumpTarget.Index == T2)
                            {
                                trailerOk = true;
                                consumeBlock = nb;
                            }
                        }
                        if (trailerOk)
                        {
                            var ifStmt = new IrIf { Pc = b.EndPc };
                            ifStmt.Condition = ifCond;
                            ifStmt.NegateCondition = ifNegate;
                            // 末块的 goto 本身无语句产物，直接收编其语句即可
                            if (consumeBlock != null)
                            {
                                ifStmt.ThenBody = StructureRegion(bodyStart, endIdx, loops);
                                ifStmt.ThenBody.AddRange(consumeBlock.Statements);
                            }
                            else
                            {
                                ifStmt.ThenBody = StructureRegion(bodyStart, endIdx - 1, loops);
                                ifStmt.ThenBody.AddRange(trailer.Statements);
                            }
                            // 表达式级投影：then 体为空且 trailer 出口栈有值流向汇合点
                            // → 不生成语句（值已由 SSA 折叠进汇合点表达式）
                            IrBlock tailBlock = consumeBlock ?? trailer;
                            if (ifStmt.ThenBody.Count == 0 && HasRealValue(tailBlock.ExitStack))
                            {
                                i = T2;
                                continue;
                            }
                            output.AddRange(b.Statements);
                            if (consumeBlock != null)
                                consumeBlock.Statements = new List<IrStatement>(); // 已被收编，防止外层重复
                            output.Add(ifStmt);
                            i = T2; // 超出 endIdx → 本区域处理结束
                            continue;
                        }
                    }

                    // ---- 无法结构化的条件跳：兜底注释 ----
                    output.AddRange(b.Statements);
                    output.Add(new IrRawLine("// if (" + (b.JumpOnFalse ? "!(" + b.Condition.Emit() + ")" : b.Condition.Emit())
                        + ") goto L" + b.JumpTarget.StartPc + ";") { Pc = b.EndPc });
                    i++;
                    continue;
                }

                output.AddRange(b.Statements);

                if (b.Terminator == BlockTerminator.Jump && b.JumpTarget != null)
                {
                    int target = b.JumpTarget.Index;
                    bool handled = false;

                    // ---- 绕行线程化：goto F（前向单块）且 F 跳回本块下一块、
                    // 且 F 只被本块跳入 → 把 F 内联进来（QuickJS 解构源求值的绕行产物）
                    // 注意：b.Statements 已在外层 AddRange 过，这里只追加 F 的语句
                    if (target > i + 1 && target <= endIdx)
                    {
                        IrBlock f = blocks[target];
                        if (f.Terminator == BlockTerminator.Jump && f.JumpTarget != null
                            && f.JumpTarget.Index == i + 1
                            && f.Predecessors.Count == 1 && f.Predecessors[0] == b)
                        {
                            output.AddRange(f.Statements);
                            absorbedBlocks.Add(target);
                            i++;
                            continue;
                        }
                    }

                    // break / continue 识别（从内层循环往外匹配）
                    for (int l = loops.Count - 1; l >= 0; l--)
                    {
                        if (target == loops[l].ExitIndex)
                        {
                            output.Add(new IrBreak { Pc = b.EndPc });
                            handled = true;
                            break;
                        }
                        if (target == loops[l].HeaderIndex || target == loops[l].ContinueIndex)
                        {
                            // 自然回边（循环体最后一块的 goto→循环头）不输出 continue
                            if (i == loops[l].NaturalBackEdge && target == loops[l].HeaderIndex)
                            {
                                handled = true;
                                break;
                            }
                            output.Add(new IrContinue { Pc = b.EndPc });
                            handled = true;
                            break;
                        }
                    }

                    if (!handled)
                    {
                        // 跳到下一块等于 fall-through，不输出；其他情况 goto 兜底注释
                        // （已被线程化吸收的块视为不存在，顺延计算逻辑下一块）
                        int logicalNext = i + 1;
                        while (absorbedBlocks.Contains(logicalNext)) logicalNext++;
                        if (target != logicalNext)
                            output.Add(new IrGoto(b.JumpTarget.StartPc) { Pc = b.EndPc });
                    }
                    i++;
                    continue;
                }

                i++;
            }

            return output;
        }

        /// <summary>已被绕行线程化内联的块（遍历时跳过，不再输出）</summary>
        private readonly HashSet<int> absorbedBlocks = new HashSet<int>();

        // ==================== 复合条件（&& / || 链） ====================

        /// <summary>出口栈是否有真实值残留（非 undefined 哨兵）</summary>
        private static bool HasRealValue(List<IrValue> exitStack)
        {
            foreach (IrValue v in exitStack)
                if (v is not IrConstant c || c.Text != "undefined")
                    return true;
            return false;
        }

        /// <summary>
        /// 扫描从块 start 开始的条件链（语句级短路逻辑编译产物）。
        /// && 前缀：连续 if_false→同一出口 E 的纯条件块，其后内容一律视为 then 体
        ///   （"if (c) goto E" 嵌套在 then 首部与 && c 等价，后续块不属于链也没关系）；
        /// || 链：若干 if_true→then 起点 + 至少一个 if_false→E 收尾；
        /// 全 || 否定链：每项 if_true→M（跳过 then），等价 if (!(a || b))（negateChain=true）。
        /// 仅折叠无语句的纯条件块（副作用语句不能被短路跳过）。
        /// </summary>
        private bool ScanConditionChain(int start, int endIdx,
            out List<IrValue> terms, out bool isOrChain, out bool negateChain, out int thenStart, out int E)
        {
            terms = new List<IrValue>();
            isOrChain = false;
            negateChain = false;
            E = -1;
            List<IrBlock> blocks = func.Blocks;

            IrBlock first = blocks[start];
            if (first.JumpOnFalse)
            {
                // && 前缀：连续 if_false→同一出口 E；在首个不匹配块停止（其后再折是 then 体的事）
                E = first.JumpTarget.Index;
                terms.Add(first.Condition);
                int j = start + 1;
                while (j <= endIdx)
                {
                    IrBlock bj = blocks[j];
                    if (bj.Terminator != BlockTerminator.CondJump || bj.Condition == null || bj.JumpTarget == null)
                        break;
                    if (bj.Statements.Count > 0)
                        break;
                    if (!bj.JumpOnFalse || bj.JumpTarget.Index != E)
                        break;
                    terms.Add(bj.Condition);
                    j++;
                }
                thenStart = j;
                return terms.Count >= 2;
            }

            // || 前缀：连续 if_true（目标待验证），随后至少一个 if_false→E
            int j2 = start;
            var orTargets = new List<int>();
            while (j2 <= endIdx)
            {
                IrBlock bj = blocks[j2];
                if (bj.Terminator != BlockTerminator.CondJump || bj.Condition == null || bj.JumpTarget == null)
                    break;
                if (j2 > start && bj.Statements.Count > 0)
                    break;
                if (bj.JumpOnFalse)
                {
                    if (E >= 0 && bj.JumpTarget.Index != E)
                        break;
                    E = bj.JumpTarget.Index;
                    terms.Add(bj.Condition);
                    j2++;
                    continue; // || 链尾部可以有多个 && 项（a || b && c）
                }
                orTargets.Add(bj.JumpTarget.Index);
                terms.Add(bj.Condition);
                j2++;
            }
            thenStart = j2;

            if (orTargets.Count == 0)
                return false;

            // 全 || 否定链：无 && 收尾项，每项 if_true→同一目标 M（跳过 then 体）
            if (E < 0)
            {
                if (orTargets.Count < 2) return false;
                int m = orTargets[0];
                if (orTargets.Any(t => t != m)) return false;
                if (m <= thenStart) return false;
                isOrChain = true;
                negateChain = true;
                E = m;
                return true;
            }

            // || 项必须都跳到 then 起点
            if (terms.Count < 2)
                return false;
            foreach (int t in orTargets)
                if (t != thenStart)
                    return false;
            if (E <= thenStart)
                return false;
            isOrChain = true;
            return true;
        }

        /// <summary>把条件项按单一运算符折叠为一个表达式</summary>
        private IrValue FoldConditionTerms(List<IrValue> terms, bool isOrChain)
        {
            IrValue acc = terms[0];
            for (int k = 1; k < terms.Count; k++)
            {
                acc = isOrChain
                    ? new IrBinaryOp("||", acc, terms[k], 3) { Id = func.AllocValueId() }
                    : new IrBinaryOp("&&", acc, terms[k], 4) { Id = func.AllocValueId() };
            }
            return acc;
        }

        /// <summary>
        /// 提取 for-of/for-in 的循环变量（从体块首部摘除对应语句）：
        ///   简单形态：v = for_of_value
        ///   解构形态：IrIteratorStart(for_of_value) + a = for_of_value [?? 默认] + 空洞 → [a, , b]
        /// </summary>
        private static bool TryExtractForOfVar(IrBlock bodyFirst, bool isForIn, out string? varName)
        {
            varName = null;
            if (bodyFirst.Statements.Count == 0)
                return false;

            IrIteratorPlaceholder.Kind valueKind = isForIn
                ? IrIteratorPlaceholder.Kind.InValue : IrIteratorPlaceholder.Kind.OfValue;

            // 简单形态
            if (bodyFirst.Statements[0] is IrAssign firstAssign
                && firstAssign.Value is IrIteratorPlaceholder fv && fv.K == valueKind)
            {
                varName = firstAssign.Target;
                bodyFirst.Statements.RemoveAt(0);
                return true;
            }

            // 解构形态（仅 for-of）：for (let [i, v] of ...)
            if (!isForIn && bodyFirst.Statements[0] is IrIteratorStart its
                && its.Iterable is IrIteratorPlaceholder iv && iv.K == IrIteratorPlaceholder.Kind.OfValue)
            {
                var parts = new List<string>();
                int k = 1;
                while (k < bodyFirst.Statements.Count
                    && bodyFirst.Statements[k] is IrAssign da)
                {
                    if (da.Value is IrIteratorPlaceholder dv && dv.K == IrIteratorPlaceholder.Kind.OfValue)
                        parts.Add(da.Target); // Target 为空串 = 空洞位
                    else if (da.Value is IrBinaryOp db && db.Op == "??"
                        && db.Left is IrIteratorPlaceholder dlv && dlv.K == IrIteratorPlaceholder.Kind.OfValue)
                        parts.Add(da.Target + " = " + db.Right.Emit());
                    else
                        break;
                    k++;
                }
                if (parts.Count >= 2)
                {
                    bodyFirst.Statements.RemoveRange(0, k);
                    varName = "[" + string.Join(", ", parts) + "]";
                    return true;
                }
            }
            return false;
        }

        // ==================== try/catch/finally 识别 ====================

        /// <summary>
        /// 识别 QuickJS 的 try/catch/finally 编译产物：
        ///   B: ...; catch H          （catch 标记压栈，异常边 B→H）
        ///   try 体 [B+1, H-1]，末尾 drop; [undefined; gosub F; drop;] goto X
        ///   H: put_loc e; [catch H2]  （handler 首条语句是 e = CatchOffset 标记）
        ///   catch 体 [H, H2/X-1]，末尾同样的 gosub/goto 尾迹
        ///   [H2: gosub F; throw]      （catch 体抛异常时的 finally 尾迹，丢弃）
        ///   [F: finally 体; ret]
        /// </summary>
        private bool TryStructureTryCatch(int start, int endIdx, List<LoopCtx> loops,
            List<IrStatement> output, out int nextIndex)
        {
            nextIndex = start;
            List<IrBlock> blocks = func.Blocks;
            IrBlock b = blocks[start];
            IrBlock handler = b.CatchTarget!;
            int H = handler.Index;
            // 允许 handler 紧贴区域外一块（try 嵌在循环体/if 分支末尾）：
            // catch 体在区域外收编后加入 absorbedBlocks，外层遍历会跳过
            if (H <= start + 1 || H > endIdx + 1)
            return false;

            // try 体最后一块通常以 goto X 结尾（跳过 handler）；也可能直接 return/throw
            // 注意：X 可能超出当前区域（try 嵌在 if-then 里、出口在区域外），仍然允许，
            // 只是尾部 goto 不能安全剥离（保留兜底注释，提示人工检查）
            IrBlock tryLast = blocks[H - 1];
            int X;
            if (tryLast.Terminator == BlockTerminator.Jump && tryLast.JumpTarget != null)
            {
                X = tryLast.JumpTarget.Index;
            }
            else if (tryLast.Terminator == BlockTerminator.Return
                || tryLast.Terminator == BlockTerminator.Throw
                || tryLast.Terminator == BlockTerminator.TailCall)
            {
                X = -1; // try 体以 return/throw 结束：出口稍后从 catch 尾迹的 goto 取
            }
            else
            return false;

            // handler 里的嵌套 catch（finally 保护；可能超出当前区域，由 catchEnd 截断处理）
            int h2 = handler.CatchTarget != null ? handler.CatchTarget.Index : -1;
            if (h2 >= 0 && h2 <= H)
                return false;

            if (X == -1)
            {
                // 从 catch 体尾迹（h2 前一块）的 goto 取出口
                if (h2 < 0 || h2 - 1 <= H) return false;
                IrBlock catchLast = blocks[h2 - 1];
                if (catchLast.Terminator != BlockTerminator.Jump || catchLast.JumpTarget == null
                    || catchLast.JumpTarget.Index <= H)
                    return false;
                X = catchLast.JumpTarget.Index;
            }

            // 出口后向跳转（循环内 try：trailer 是 continue）：
            // 无法从 try trailer 得到前向出口，用异常尾的 throw 定位区域末尾
            int tailEnd = -1;
            if (X <= H)
            {
                if (h2 < 0) return false;
                // 异常尾可能超出当前区域（循环体边界把它切开了），扫到函数末
                for (int k = h2; k < blocks.Count; k++)
                {
                    if (blocks[k].Terminator == BlockTerminator.Throw) { tailEnd = k; break; }
                    if (k > h2 + 8) break; // 异常尾紧邻 h2，超出范围说明形态不符
                }
                if (tailEnd < 0) return false;
            }

            // catch 变量：handler 首条语句必须是 e = <CatchOffset 标记>
            string? catchVar = null;
            if (handler.Statements.Count > 0 && handler.Statements[0] is IrAssign ca
                && ca.Value is IrSpecialMarker mk && mk.Kind == IrSpecialMarker.MarkerKind.CatchOffset)
                catchVar = ca.Target;
            else
                return false; // 无绑定的形态不识别，保守兜底

            // finally：扫描 try/catch 体内的 gosub（finally 子程序调用）
            int scanEnd = X > H ? Math.Min(X, endIdx + 1) : h2;
            int F = -1;
            for (int k = start + 1; k < scanEnd; k++)
            {
                foreach (var ins in blocks[k].Instructions)
                {
                    if (ins.getOpCode().OPCode == OPCodeValue.OP_gosub)
                    {
                        long? tgt = BasicBlockPass.GetJumpTarget(ins);
                        if (!tgt.HasValue) return false;
                        int fIdx = FindBlockByStartPc(blocks, tgt.Value);
                        if (fIdx < 0) return false;
                        if (F != -1 && F != fIdx) return false; // 多个 finally 目标，放弃
                        F = fIdx;
                    }
                }
            }

            int retIdx = -1;
            if (F >= 0)
            {
                // finally 体 [F, retIdx]：以 ret（Indirect 终结）结束
                for (int k = F; k <= endIdx && (X <= H || k < X); k++)
                {
                    if (blocks[k].Terminator == BlockTerminator.Indirect) { retIdx = k; break; }
                }
                if (retIdx < 0) return false;
            }

            // catch 体范围：到嵌套 catch / 出口为止。后向出口（循环内 try）时
            // catch 体可超出当前区域（循环体边界会把它切开），收编后标记吸收
            int catchEnd = (X <= H && h2 >= 0)
                ? h2 - 1
                : Math.Min(h2 >= 0 ? h2 : X, endIdx + 1) - 1;
            if (catchEnd < H) return false;

            // ---- 校验完成，开始变更 ----
            handler.Statements.RemoveAt(0); // e = CatchOffset 标记

            var tc = new IrTryCatch { Pc = b.EndPc, CatchVar = catchVar };

            output.AddRange(b.Statements);

            // 前向出口 X 就是 try/catch 之后的顺序代码，尾部 goto X 可以安全剥离
            // （后向出口是循环 continue，不能剥）
            long xStartPc = X > H ? blocks[X].StartPc : -1;

            tc.TryBody = StructureRegion(start + 1, H - 1, loops);
            StripTrailingGoto(tc.TryBody, xStartPc);

            tc.CatchBody = StructureRegion(H, catchEnd, loops);
            StripTrailingGoto(tc.CatchBody, xStartPc);

            if (F >= 0)
            {
                tc.FinallyBody = StructureRegion(F, retIdx, loops);
                // 去掉 finally 末尾的 ret 占位行
                if (tc.FinallyBody.Count > 0 && tc.FinallyBody[tc.FinallyBody.Count - 1] is IrRawLine rl
                    && rl.Text.StartsWith("// ret"))
                    tc.FinallyBody.RemoveAt(tc.FinallyBody.Count - 1);
            }

            output.Add(tc);
            nextIndex = X > H ? X : tailEnd + 1;

            // catch 体/异常尾/finally 可能超出当前区域：标记吸收，防止外层重复输出
            if (H > endIdx || catchEnd > endIdx)
            {
                for (int k = H; k <= catchEnd; k++) absorbedBlocks.Add(k);
                if (h2 >= 0)
                {
                    int tailStop = tailEnd >= 0 ? tailEnd : (F >= 0 ? F - 1 : (X > H ? X - 1 : h2));
                    for (int k = h2; k <= tailStop; k++) absorbedBlocks.Add(k);
                }
                if (F >= 0)
                    for (int k = F; k <= retIdx; k++) absorbedBlocks.Add(k);
            }
            return true;
        }

        private static int FindBlockByStartPc(List<IrBlock> blocks, long pc)
        {
            for (int k = 0; k < blocks.Count; k++)
                if (blocks[k].StartPc == pc) return k;
            return -1;
        }

        /// <summary>去掉区域末尾“跳到 try 之后”的兜底 goto 注释</summary>
        private static void StripTrailingGoto(List<IrStatement> stmts, long targetStartPc)
        {
            if (targetStartPc >= 0 && stmts.Count > 0
                && stmts[stmts.Count - 1] is IrGoto g && g.TargetPcValue == targetStartPc)
                stmts.RemoveAt(stmts.Count - 1);
        }

        // ==================== switch 识别 ====================

        /// <summary>
        /// 识别 QuickJS 的 switch 编译产物：判别式压栈一次，之后每个 case 是
        /// `dup; push C; strict_eq; if_false/if_true` 的比较链。
        ///   if_false → 下一测试/default：case 体 = 顺序块 [j+1, T-1]
        ///   if_true  → 共享体（fallthrough case，体在后面的 case 处）
        /// 体块以 return 结束，或以 goto 公共出口结束（break）。
        /// </summary>
        private bool TryStructureSwitch(int start, int endIdx, List<LoopCtx> loops,
            List<IrStatement> output, out int nextIndex)
        {
            nextIndex = start;
            List<IrBlock> blocks = func.Blocks;

            string? discText = null;
            IrValue? disc = null;
            // (label, bodyStart, bodyEnd)；bodyEnd = -1 表示共享体（bodyStart 是目标块号）
            var entries = new List<(IrValue label, int bodyStart, int bodyEnd)>();

            int j = start;
            while (j <= endIdx)
            {
                IrBlock bj = blocks[j];
                if (!MatchSwitchTest(bj, j == start, ref discText, ref disc, out IrValue? label) || label == null)
                    break;
                int T = bj.JumpTarget!.Index;
                if (T <= j || T > endIdx + 1)
                    break; // 只接受前向跳
                if (bj.JumpOnFalse)
                {
                    entries.Add((label, j + 1, T - 1));
                    j = T;
                }
                else
                {
                    entries.Add((label, T, -1));
                    j = j + 1;
                }
            }

            // 至少两个 case 才认定为 switch（避免把 || 短路链/else-if 链误判）
            if (entries.Count < 2)
                return false;

            // switch 覆盖的最大块号
            int maxIdx = j - 1;
            foreach (var e in entries)
                if (e.bodyEnd >= 0 && e.bodyEnd > maxIdx)
                    maxIdx = e.bodyEnd;

            // 出口：体块末尾 goto 的共同目标（break）；全部 return 则无出口（区域末尾）
            int exit = -1;
            foreach (var e in entries)
            {
                if (e.bodyEnd < e.bodyStart) continue;
                IrBlock last = blocks[e.bodyEnd];
                if (last.Terminator == BlockTerminator.Jump && last.JumpTarget != null
                    && last.JumpTarget.Index > maxIdx)
                {
                    int cand = last.JumpTarget.Index;
                    if (exit == -1) exit = cand;
                    else if (exit != cand) { exit = -2; break; } // 多个候选，放弃出口推断
                }
            }
            if (exit == -1 || exit == -2) exit = endIdx + 1;
            if (exit > endIdx + 1) return false;

            // default：链终止块 j 到出口之间（j == exit 表示无 default）
            int defaultStart = j;

            // 体共享分组（fallthrough：多个 label 映射到同一体起点）
            var groupMap = new Dictionary<int, IrSwitchCase>();
            var groupOrder = new List<int>();
            foreach (var e in entries)
            {
                if (!groupMap.TryGetValue(e.bodyStart, out IrSwitchCase? g))
                {
                    g = new IrSwitchCase();
                    groupMap[e.bodyStart] = g;
                    groupOrder.Add(e.bodyStart);
                }
                g.Labels.Add(e.label);
            }
            // 校验：每个组都要有带体范围的条目（体在链外则放弃）
            // 注意必须先完成全部校验再变更 output，否则失败路径会重复输出语句
            foreach (int bs in groupOrder)
                if (!entries.Any(x => x.bodyStart == bs && x.bodyEnd >= 0))
                    return false;

            var sw = new IrSwitch { Discriminant = disc!, Pc = blocks[start].EndPc };
            var innerLoops = new List<LoopCtx>(loops) { new LoopCtx { HeaderIndex = -1, ExitIndex = exit, ContinueIndex = -1 } };

            output.AddRange(blocks[start].Statements);

            foreach (int bs in groupOrder)
            {
                IrSwitchCase g = groupMap[bs];
                int bodyEnd = entries.First(x => x.bodyStart == bs && x.bodyEnd >= 0).bodyEnd;
                g.Body = StructureRegion(bs, bodyEnd, innerLoops);
                sw.Cases.Add(g);
            }

            if (defaultStart < exit && defaultStart <= endIdx)
            {
                var dc = new IrSwitchCase();
                dc.Body = StructureRegion(defaultStart, exit - 1, innerLoops);
                sw.Cases.Add(dc);
            }

            output.Add(sw);
            nextIndex = exit;
            return true;
        }

        /// <summary>匹配一个 switch 测试块：条件是 X === 常量；非首块必须以 dup 开头</summary>
        private static bool MatchSwitchTest(IrBlock b, bool isFirst,
            ref string? discText, ref IrValue? disc, out IrValue? label)
        {
            label = null;
            if (b.Terminator != BlockTerminator.CondJump || b.JumpTarget == null)
                return false;
            if (b.Condition is not IrBinaryOp bin || bin.Op != "===")
                return false;
            if (!isFirst)
            {
                // switch 比较链的后续测试块以 dup（判别式留存副本）开头
                if (b.Instructions.Count == 0
                    || b.Instructions[0].getOpCode().OPCode != OPCodeValue.OP_dup)
                    return false;
            }
            IrValue x = bin.Left;
            IrValue? c = bin.Right as IrConstant;
            if (c == null)
            {
                c = bin.Left as IrConstant;
                x = bin.Right;
            }
            if (c == null)
                return false;
            if (discText == null)
            {
                discText = x.Emit();
                disc = x;
            }
            else if (discText != x.Emit())
            {
                return false;
            }
            label = c;
            return true;
        }

        // ==================== IR 值树工具 ====================

        /// <summary>值树中是否引用了指定变量</summary>
        private static bool MentionsVar(IrValue v, string name)
        {
            switch (v)
            {
                case IrVariable var: return var.Name == name;
                case IrUnaryOp un: return MentionsVar(un.Operand, name);
                case IrBinaryOp bin: return MentionsVar(bin.Left, name) || MentionsVar(bin.Right, name);
                case IrTernary tern:
                    return MentionsVar(tern.Condition, name) || MentionsVar(tern.Then, name) || MentionsVar(tern.Else, name);
                case IrCall call:
                    if (MentionsVar(call.Func, name)) return true;
                    if (call.ThisArg != null && MentionsVar(call.ThisArg, name)) return true;
                    return call.Args.Any(a => MentionsVar(a, name));
                case IrGetProperty gp:
                    return MentionsVar(gp.Object, name) || (gp.KeyExpr != null && MentionsVar(gp.KeyExpr, name));
                case IrPhi phi:
                    return phi.Sources.Any(s => MentionsVar(s, name));
                default: return false;
            }
        }

        /// <summary>把值树中所有 name 变量引用替换为 replacement（原地修改并返回新根）</summary>
        private static IrValue SubstituteVar(IrValue v, string name, IrValue replacement)
        {
            switch (v)
            {
                case IrVariable var when var.Name == name:
                    return replacement;
                case IrUnaryOp un:
                    un.Operand = SubstituteVar(un.Operand, name, replacement);
                    return un;
                case IrBinaryOp bin:
                    bin.Left = SubstituteVar(bin.Left, name, replacement);
                    bin.Right = SubstituteVar(bin.Right, name, replacement);
                    return bin;
                case IrTernary tern:
                    tern.Condition = SubstituteVar(tern.Condition, name, replacement);
                    tern.Then = SubstituteVar(tern.Then, name, replacement);
                    tern.Else = SubstituteVar(tern.Else, name, replacement);
                    return tern;
                case IrCall call:
                    call.Func = SubstituteVar(call.Func, name, replacement);
                    if (call.ThisArg != null) call.ThisArg = SubstituteVar(call.ThisArg, name, replacement);
                    for (int ci = 0; ci < call.Args.Count; ci++)
                        call.Args[ci] = SubstituteVar(call.Args[ci], name, replacement);
                    return call;
                case IrGetProperty gp:
                    gp.Object = SubstituteVar(gp.Object, name, replacement);
                    if (gp.KeyExpr != null) gp.KeyExpr = SubstituteVar(gp.KeyExpr, name, replacement);
                    return gp;
                default:
                    return v;
            }
        }
    }
}
