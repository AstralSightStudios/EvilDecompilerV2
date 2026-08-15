using EvilDecompiler.Decompiler.AST;

namespace EvilDecompiler.Decompiler.AstPasses
{
    /// <summary>let x; 紧跟 x = expr; → let x = expr;</summary>
    public class DeclInitMergePass : AstPass
    {
        public override string Name => "DeclInitMerge";

        protected override void PostVisitBlock(AstBlock block)
        {
            for (int i = 0; i + 1 < block.Statements.Count; i++)
            {
                if (block.Statements[i] is AstVarDecl decl
                    && decl.Declarations.Count == 1
                    && decl.Declarations[0].Init == null
                    && block.Statements[i + 1] is AstExprStmt exprStmt
                    && exprStmt.Expr is AstAssignExpr assign
                    && assign.Target is AstIdentifier id
                    && id.Name == decl.Declarations[0].Name)
                {
                    decl.Declarations[0] = (id.Name, assign.Value);
                    block.Statements.RemoveAt(i + 1);
                    i--; // 合并后再看能否继续合并下一条
                }
            }
        }
    }

    /// <summary>return/throw 之后的同块死代码删除</summary>
    public class DeadCodePass : AstPass
    {
        public override string Name => "DeadCode";

        /// <summary>块嵌套深度（1 = 函数体直层）</summary>
        private int depth;

        protected override void EnterBlock() => depth++;
        protected override void ExitBlock() => depth--;

        protected override void PostVisitBlock(AstBlock block)
        {
            for (int i = 0; i < block.Statements.Count; i++)
            {
                if (block.Statements[i] is AstReturn || block.Statements[i] is AstThrow
                    || block.Statements[i] is AstBreak || block.Statements[i] is AstContinue)
                {
                    if (i + 1 < block.Statements.Count)
                        block.Statements.RemoveRange(i + 1, block.Statements.Count - i - 1);
                    break;
                }
            }

            // 只有函数体直层末尾的无值 return 才能删：
            // if 分支里的 `return;` 是控制流（阻止后续 else 路径执行），删了会让
            // 后续语句无条件执行（如 `if (c) { x=1; return; } throw e;` 变成 throw 必达）
            if (depth == 1
                && block.Statements.Count > 0
                && block.Statements[block.Statements.Count - 1] is AstReturn lastRet
                && lastRet.Value == null)
            {
                block.Statements.RemoveAt(block.Statements.Count - 1);
            }
        }
    }

    /// <summary>
    /// dup 物化临时变量内联：tmpN = expr; 且 tmpN 在同块中仅被引用一次、
    /// 且引用点就在下一条语句的顶层表达式里（求值顺序不变）→ 内联并删除赋值。
    /// 同时清理不再使用的 let tmpN; 头部声明条目。
    /// </summary>
    public class TmpInlinePass : AstPass
    {
        public override string Name => "TmpInline";

        protected override void PostVisitBlock(AstBlock block)
        {
            var stmts = block.Statements;
            for (int i = 0; i + 1 < stmts.Count; i++)
            {
                if (stmts[i] is AstExprStmt es && es.Expr is AstAssignExpr assign
                    && assign.Target is AstIdentifier id && id.IsCompilerTemp)
                {
                    int useCount = 0;
                    for (int j = 0; j < stmts.Count; j++)
                        if (j != i) useCount += CountUses(stmts[j], id.Name);
                    if (useCount != 1) continue;
                    // 唯一引用必须位于下一条语句（保证副作用求值顺序不变）
                    if (CountUses(stmts[i + 1], id.Name) != 1) continue;
                    if (ReplaceUse(stmts[i + 1], id.Name, assign.Value))
                    {
                        stmts.RemoveAt(i);
                        i--;
                    }
                }
            }
            // 清理不再使用的 tmp 声明条目
            for (int i = 0; i < stmts.Count; i++)
            {
                if (stmts[i] is AstVarDecl decl)
                {
                    decl.Declarations.RemoveAll(d => d.Init == null
                        && decl.CompilerTemps != null && decl.CompilerTemps.Contains(d.Name)
                        && stmts.Where((s, j) => j != i).Sum(s => CountUses(s, d.Name)) == 0);
                    if (decl.Declarations.Count == 0)
                    {
                        stmts.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        // ---------- 引用计数 / 替换（递归遍历） ----------

        private static int CountUses(AstNode node, string name)
        {
            switch (node)
            {
                case AstIdentifier id: return id.Name == name ? 1 : 0;
                case AstLiteral: return 0;
                case AstBinary b: return CountUses(b.Left, name) + CountUses(b.Right, name);
                case AstUnary u: return CountUses(u.Operand, name);
                case AstCall c:
                    return CountUses(c.Callee, name) + c.Args.Sum(a => CountUses(a, name));
                case AstNew n:
                    return CountUses(n.Callee, name) + n.Args.Sum(a => CountUses(a, name));
                case AstMember m:
                    return CountUses(m.Object, name) + (m.KeyExpr != null ? CountUses(m.KeyExpr, name) : 0);
                case AstAssignExpr a: return CountUses(a.Target, name) + CountUses(a.Value, name);
                case AstConditional c:
                    return CountUses(c.Condition, name) + CountUses(c.ThenExpr, name) + CountUses(c.ElseExpr, name);
                case AstObjectLiteral o:
                    return o.Items.Sum(it => (it.KeyExpr != null ? CountUses(it.KeyExpr, name) : 0) + CountUses(it.Value, name));
                case AstClassExpr ce:
                    return (ce.SuperClass != null ? CountUses(ce.SuperClass, name) : 0)
                        + ce.Members.Sum(m => (m.KeyExpr != null ? CountUses(m.KeyExpr, name) : 0) + CountUses(m.Value, name));
                case AstArrayLiteral a:
                    return a.Items.Sum(it => CountUses(it.Value, name));
                case AstFunctionExpr f: return CountUses(f.Body, name);
                case AstBlock b: return b.Statements.Sum(s => CountUses(s, name));
                case AstExprStmt s: return CountUses(s.Expr, name);
                case AstVarDecl d: return d.Declarations.Sum(x => x.Init != null ? CountUses(x.Init, name) : 0);
                case AstReturn r: return r.Value != null ? CountUses(r.Value, name) : 0;
                case AstThrow t: return t.Value != null ? CountUses(t.Value, name) : 0;
                case AstIf f:
                    return CountUses(f.Condition, name) + CountUses(f.Then, name)
                        + (f.Else != null ? CountUses(f.Else, name) : 0);
                case AstWhile w: return CountUses(w.Condition, name) + CountUses(w.Body, name);
                case AstDoWhile dw: return CountUses(dw.Condition, name) + CountUses(dw.Body, name);
                case AstForOf fo: return CountUses(fo.Iterable, name) + CountUses(fo.Body, name);
                case AstSwitch sw:
                    return CountUses(sw.Discriminant, name)
                        + sw.Cases.Sum(c => c.Labels.Sum(l => CountUses(l, name)) + CountUses(c.Body, name));
                case AstTryCatch tc:
                    return CountUses(tc.TryBody, name) + CountUses(tc.CatchBody, name)
                        + (tc.FinallyBody != null ? CountUses(tc.FinallyBody, name) : 0);
                case AstIteratorStart its: return CountUses(its.Iterable, name);
                case AstFunctionDecl f: return CountUses(f.Body, name);
                default: return 0;
            }
        }

        /// <summary>替换语句顶层表达式中的唯一引用（嵌套块内的引用不处理，返回 false）</summary>
        private static bool ReplaceUse(AstStmt stmt, string name, AstExpr repl)
        {
            switch (stmt)
            {
                case AstExprStmt s:
                    s.Expr = ReplaceInExpr(s.Expr, name, repl);
                    return true;
                case AstVarDecl d:
                    for (int i = 0; i < d.Declarations.Count; i++)
                        if (d.Declarations[i].Init != null && CountUses(d.Declarations[i].Init!, name) > 0)
                            d.Declarations[i] = (d.Declarations[i].Name, ReplaceInExpr(d.Declarations[i].Init!, name, repl));
                    return true;
                case AstReturn r:
                    if (r.Value != null) r.Value = ReplaceInExpr(r.Value, name, repl);
                    return true;
                case AstThrow t:
                    if (t.Value != null) t.Value = ReplaceInExpr(t.Value, name, repl);
                    return true;
                case AstIf f:
                    if (CountUses(f.Condition, name) == 1) { f.Condition = ReplaceInExpr(f.Condition, name, repl); return true; }
                    return false;
                case AstWhile w:
                    if (CountUses(w.Condition, name) == 1) { w.Condition = ReplaceInExpr(w.Condition, name, repl); return true; }
                    return false;
                case AstDoWhile dw:
                    if (CountUses(dw.Condition, name) == 1) { dw.Condition = ReplaceInExpr(dw.Condition, name, repl); return true; }
                    return false;
                case AstForOf fo:
                    if (CountUses(fo.Iterable, name) == 1) { fo.Iterable = ReplaceInExpr(fo.Iterable, name, repl); return true; }
                    return false;
                case AstIteratorStart its:
                    if (CountUses(its.Iterable, name) == 1) { its.Iterable = ReplaceInExpr(its.Iterable, name, repl); return true; }
                    return false;
                default:
                    return false;
            }
        }

        private static AstExpr ReplaceInExpr(AstExpr expr, string name, AstExpr repl)
        {
            switch (expr)
            {
                case AstIdentifier id when id.Name == name:
                    return repl;
                case AstBinary b:
                    b.Left = ReplaceInExpr(b.Left, name, repl);
                    b.Right = ReplaceInExpr(b.Right, name, repl);
                    return b;
                case AstUnary u:
                    u.Operand = ReplaceInExpr(u.Operand, name, repl);
                    return u;
                case AstCall c:
                    c.Callee = ReplaceInExpr(c.Callee, name, repl);
                    for (int i = 0; i < c.Args.Count; i++) c.Args[i] = ReplaceInExpr(c.Args[i], name, repl);
                    return c;
                case AstNew n:
                    n.Callee = ReplaceInExpr(n.Callee, name, repl);
                    for (int i = 0; i < n.Args.Count; i++) n.Args[i] = ReplaceInExpr(n.Args[i], name, repl);
                    return n;
                case AstMember m:
                    m.Object = ReplaceInExpr(m.Object, name, repl);
                    if (m.KeyExpr != null) m.KeyExpr = ReplaceInExpr(m.KeyExpr, name, repl);
                    return m;
                case AstAssignExpr a:
                    a.Target = ReplaceInExpr(a.Target, name, repl);
                    a.Value = ReplaceInExpr(a.Value, name, repl);
                    return a;
                case AstConditional c:
                    c.Condition = ReplaceInExpr(c.Condition, name, repl);
                    c.ThenExpr = ReplaceInExpr(c.ThenExpr, name, repl);
                    c.ElseExpr = ReplaceInExpr(c.ElseExpr, name, repl);
                    return c;
                case AstObjectLiteral o:
                    foreach (var it in o.Items)
                    {
                        if (it.KeyExpr != null) it.KeyExpr = ReplaceInExpr(it.KeyExpr, name, repl);
                        it.Value = ReplaceInExpr(it.Value, name, repl);
                    }
                    return o;
                case AstArrayLiteral a:
                    foreach (var it in a.Items)
                        it.Value = ReplaceInExpr(it.Value, name, repl);
                    return a;
                default:
                    return expr;
            }
        }
    }

    /// <summary>
    /// 数组解构重组：for_of_start 标记 + 连续的 for_of_value 赋值 → [a, b, ...rest] = iterable
    ///   // iterator over arr          （AstIteratorStart 标记）
    ///   first = for_of_value;
    ///   x = for_of_value ?? 1;        （带默认值）
    ///    = for_of_value;             （空洞位，目标名为空串）
    ///   while (!for_of_done) {} rest = [];  （rest 收集）
    /// </summary>
    public class DestructureArrayPass : AstPass
    {
        public override string Name => "DestructureArray";

        protected override void PostVisitBlock(AstBlock block)
        {
            var stmts = block.Statements;
            for (int i = 0; i < stmts.Count; i++)
            {
                if (stmts[i] is not AstIteratorStart its) continue;

                var parts = new List<string>();
                int j = i + 1;
                while (j < stmts.Count && TryMatchElement(stmts[j], out string part))
                {
                    parts.Add(part);
                    j++;
                }
                int end = j;
                // rest 收集循环 + 空数组赋值（中间可能隔着残留的空洞标记）
                if (j < stmts.Count && IsCollectWhile(stmts[j]))
                {
                    int k = j + 1;
                    while (k < stmts.Count && IsHoleMarker(stmts[k])) k++;
                    if (k < stmts.Count && IsEmptyArrayAssign(stmts[k], out string? restName))
                    {
                        parts.Add("..." + restName);
                        end = k + 1;
                    }
                }
                if (parts.Count < 2) continue; // 单元素不重组，保守

                string pattern = "[" + string.Join(", ", parts) + "]";
                stmts[i] = new AstExprStmt(new AstAssignExpr(new AstIdentifier(pattern), its.Iterable)) { Pc = its.Pc };
                stmts.RemoveRange(i + 1, end - i - 1);
            }
            // 清理未被消费的空洞标记（rest 收集循环退出时的 drop 残留等）
            stmts.RemoveAll(IsHoleMarker);
        }

        /// <summary>空洞标记语句：`` = for_of_value;``（目标名为空串的赋值）</summary>
        private static bool IsHoleMarker(AstStmt stmt)
        {
            return stmt is AstExprStmt es && es.Expr is AstAssignExpr assign
                && assign.Target is AstIdentifier id && id.Name == "";
        }

        private static bool TryMatchElement(AstStmt stmt, out string part)
        {
            part = "";
            if (stmt is not AstExprStmt es)
                return false;
            // 赋值两种形态：AstAssignExpr（变量赋值）/ AstBinary("=")（成员写入 put_array_el 等）
            AstExpr target, value;
            if (es.Expr is AstAssignExpr ae) { target = ae.Target; value = ae.Value; }
            else if (es.Expr is AstBinary bb && bb.Op == "=") { target = bb.Left; value = bb.Right; }
            else return false;
            // 解构目标：标识符（空洞位时 Name 为空串）或成员访问（[a[0], a[1]] = ...）
            string? targetText = target switch
            {
                AstIdentifier id => id.Name,
                AstMember => new AstPrinter().PrintExpr(target),
                _ => null
            };
            if (targetText == null)
                return false;
            // 类型匹配：for_of_next 的 value 占位（不依赖名字）
            if (value is AstIteratorValue v && !v.IsDone && !v.IsForIn)
            {
                part = targetText;
                return true;
            }
            if (value is AstBinary bin && bin.Op == "??"
                && bin.Left is AstIteratorValue lv && !lv.IsDone && !lv.IsForIn)
            {
                part = targetText + " = " + new AstPrinter().PrintExpr(bin.Right);
                return true;
            }
            return false;
        }

        private static bool IsCollectWhile(AstStmt stmt)
        {
            return stmt is AstWhile w
                && w.Condition is AstUnary un && un.Op == "!"
                && un.Operand is AstIteratorValue id && id.IsDone && !id.IsForIn
                && w.Body.Statements.All(s => s is AstRaw);
        }

        private static bool IsEmptyArrayAssign(AstStmt stmt, out string? name)
        {
            name = null;
            if (stmt is AstExprStmt es && es.Expr is AstAssignExpr assign
                && assign.Target is AstIdentifier id
                && assign.Value is AstArrayLiteral arr
                // 空数组，或仅含 for_of 占位元素的数组（rest 收集循环里 define_array_el
                // 每次迭代追加的占位，不应算作初始内容）
                && arr.Items.All(it => it.Value is AstIteratorValue))
            {
                name = id.Name;
                return true;
            }
            return false;
        }
    }

    /// <summary>常量折叠：纯数字字面量的 + - * 运算</summary>
    public class ConstantFoldPass : AstPass
    {
        public override string Name => "ConstantFold";

        protected override AstExpr VisitExpr(AstExpr expr)
        {
            if (expr is AstBinary bin
                && bin.Left is AstLiteral l && bin.Right is AstLiteral r
                && long.TryParse(l.Text, out long lv) && long.TryParse(r.Text, out long rv))
            {
                long? result = bin.Op switch
                {
                    "+" => lv + rv,
                    "-" => lv - rv,
                    "*" => lv * rv,
                    _ => null
                };
                if (result.HasValue)
                    return new AstLiteral(result.Value.ToString()) { Pc = expr.Pc };
            }
            return expr;
        }
    }

    /// <summary>if (!(c)) A else B → if (c) B else A（减少否定条件嵌套）</summary>
    public class IfNormalizePass : AstPass
    {
        public override string Name => "IfNormalize";

        protected override bool VisitStmt(AstStmt stmt, AstBlock parent)
        {
            if (stmt is AstIf ifStmt)
            {
                // if (!(c)) A else B → if (c) B else A
                if (ifStmt.Else != null
                    && ifStmt.Condition is AstUnary un
                    && un.Op == "!" && un.IsPrefix)
                {
                    ifStmt.Condition = un.Operand;
                    (ifStmt.Then, ifStmt.Else) = (ifStmt.Else, ifStmt.Then);
                }
                // if (c) {} else B → if (!c) B（空 then 交换）
                else if (ifStmt.Else != null && ifStmt.Then.Statements.Count == 0)
                {
                    ifStmt.Condition = Negate(ifStmt.Condition);
                    ifStmt.Then = ifStmt.Else;
                    ifStmt.Else = null;
                }
                // 注意：if (c) {}（无 else）的删除挪到 PostVisitBlock 处理
                // （条件有副作用时不能删，要降级为表达式语句）
            }
            return false;
        }

        protected override void PostVisitBlock(AstBlock block)
        {
            for (int i = 0; i < block.Statements.Count; i++)
            {
                if (block.Statements[i] is AstIf ifStmt
                    && ifStmt.Else == null && ifStmt.Then.Statements.Count == 0)
                {
                    // 条件纯（无副作用）才可整个删除；否则降级为表达式语句保留副作用
                    if (EvalRetSimplifyPass.IsPure(ifStmt.Condition))
                        block.Statements.RemoveAt(i);
                    else
                        block.Statements[i] = new AstExprStmt(ifStmt.Condition);
                }
            }
        }

        /// <summary>条件取反：能翻转二元运算符就翻转，否则包 !</summary>
        public static AstExpr Negate(AstExpr cond)
        {
            if (cond is AstUnary un && un.Op == "!" && un.IsPrefix)
                return un.Operand;
            if (cond is AstBinary bin)
            {
                string? flipped = bin.Op switch
                {
                    "==" => "!=",
                    "!=" => "==",
                    "===" => "!==",
                    "!==" => "===",
                    "<" => ">=",
                    ">=" => "<",
                    ">" => "<=",
                    "<=" => ">",
                    _ => null
                };
                if (flipped != null)
                    return new AstBinary(flipped, bin.Left, bin.Right, bin.OpPrecedence) { Pc = bin.Pc };
            }
            return new AstUnary("!", cond) { Pc = cond.Pc };
        }
    }
}
