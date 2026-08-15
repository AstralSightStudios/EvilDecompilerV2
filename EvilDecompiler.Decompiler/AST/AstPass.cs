namespace EvilDecompiler.Decompiler.AST
{
    /// <summary>
    /// AST Pass 基类：提供递归遍历框架，子类重写钩子做变换。
    /// 与 IR 层的 PassManager 类似，但作用于 AST。
    /// </summary>
    public abstract class AstPass
    {
        public abstract string Name { get; }

        public void Run(AstBlock root) => VisitBlock(root);

        // ---------- 钩子（子类重写） ----------

        /// <summary>语句钩子：可原地修改，或返回 true 表示删除该语句</summary>
        protected virtual bool VisitStmt(AstStmt stmt, AstBlock parent) => false;

        /// <summary>表达式钩子：返回替换后的表达式</summary>
        protected virtual AstExpr VisitExpr(AstExpr expr) => expr;

        /// <summary>语句列表级钩子（可合并/删除多条语句），在遍历完子节点后调用</summary>
        protected virtual void PostVisitBlock(AstBlock block) { }

        // ---------- 遍历框架 ----------

        protected void VisitBlock(AstBlock block)
        {
            EnterBlock();
            for (int i = block.Statements.Count - 1; i >= 0; i--)
            {
                AstStmt stmt = block.Statements[i];
                VisitStmtChildren(stmt);
                if (VisitStmt(stmt, block))
                    block.Statements.RemoveAt(i);
            }
            PostVisitBlock(block);
            ExitBlock();
        }

        /// <summary>进入/离开块（嵌套深度跟踪等用途）</summary>
        protected virtual void EnterBlock() { }
        protected virtual void ExitBlock() { }

        private void VisitStmtChildren(AstStmt stmt)
        {
            switch (stmt)
            {
                case AstExprStmt s:
                    s.Expr = VisitExprTree(s.Expr);
                    break;
                case AstVarDecl decl:
                    for (int i = 0; i < decl.Declarations.Count; i++)
                    {
                        var d = decl.Declarations[i];
                        if (d.Init != null)
                            decl.Declarations[i] = (d.Name, VisitExprTree(d.Init));
                    }
                    break;
                case AstReturn r:
                    if (r.Value != null) r.Value = VisitExprTree(r.Value);
                    break;
                case AstThrow t:
                    if (t.Value != null) t.Value = VisitExprTree(t.Value);
                    break;
                case AstIf ifStmt:
                    ifStmt.Condition = VisitExprTree(ifStmt.Condition);
                    VisitBlock(ifStmt.Then);
                    if (ifStmt.Else != null) VisitBlock(ifStmt.Else);
                    break;
                case AstWhile w:
                    w.Condition = VisitExprTree(w.Condition);
                    VisitBlock(w.Body);
                    break;
                case AstDoWhile dw:
                    VisitBlock(dw.Body);
                    dw.Condition = VisitExprTree(dw.Condition);
                    break;
                case AstForOf fo:
                    fo.Iterable = VisitExprTree(fo.Iterable);
                    VisitBlock(fo.Body);
                    break;
                case AstSwitch sw:
                    sw.Discriminant = VisitExprTree(sw.Discriminant);
                    foreach (var c in sw.Cases)
                    {
                        for (int i = 0; i < c.Labels.Count; i++)
                            c.Labels[i] = VisitExprTree(c.Labels[i]);
                        VisitBlock(c.Body);
                    }
                    break;
                case AstTryCatch tc:
                    VisitBlock(tc.TryBody);
                    VisitBlock(tc.CatchBody);
                    if (tc.FinallyBody != null) VisitBlock(tc.FinallyBody);
                    break;
                case AstIteratorStart its:
                    its.Iterable = VisitExprTree(its.Iterable);
                    break;
                case AstFunctionDecl fn:
                    VisitBlock(fn.Body);
                    break;
            }
        }

        protected AstExpr VisitExprTree(AstExpr expr)
        {
            // 先递归子表达式，再应用自身钩子（自底向上）
            switch (expr)
            {
                case AstBinary bin:
                    bin.Left = VisitExprTree(bin.Left);
                    bin.Right = VisitExprTree(bin.Right);
                    break;
                case AstUnary un:
                    un.Operand = VisitExprTree(un.Operand);
                    break;
                case AstCall call:
                    call.Callee = VisitExprTree(call.Callee);
                    for (int i = 0; i < call.Args.Count; i++)
                        call.Args[i] = VisitExprTree(call.Args[i]);
                    break;
                case AstNew nw:
                    nw.Callee = VisitExprTree(nw.Callee);
                    for (int i = 0; i < nw.Args.Count; i++)
                        nw.Args[i] = VisitExprTree(nw.Args[i]);
                    break;
                case AstMember member:
                    member.Object = VisitExprTree(member.Object);
                    if (member.KeyExpr != null)
                        member.KeyExpr = VisitExprTree(member.KeyExpr);
                    break;
                case AstAssignExpr assign:
                    assign.Target = VisitExprTree(assign.Target);
                    assign.Value = VisitExprTree(assign.Value);
                    break;
                case AstConditional cond:
                    cond.Condition = VisitExprTree(cond.Condition);
                    cond.ThenExpr = VisitExprTree(cond.ThenExpr);
                    cond.ElseExpr = VisitExprTree(cond.ElseExpr);
                    break;
                case AstObjectLiteral obj:
                    foreach (var item in obj.Items)
                    {
                        if (item.KeyExpr != null)
                            item.KeyExpr = VisitExprTree(item.KeyExpr);
                        item.Value = VisitExprTree(item.Value);
                    }
                    break;
                case AstClassExpr ce:
                    if (ce.SuperClass != null)
                        ce.SuperClass = VisitExprTree(ce.SuperClass);
                    foreach (var m in ce.Members)
                    {
                        if (m.KeyExpr != null)
                            m.KeyExpr = VisitExprTree(m.KeyExpr);
                        m.Value = VisitExprTree(m.Value);
                    }
                    break;
                case AstArrayLiteral arr:
                    foreach (var item in arr.Items)
                        item.Value = VisitExprTree(item.Value);
                    break;
            }
            return VisitExpr(expr);
        }
    }

    /// <summary>AST Pass 管线管理器</summary>
    public class AstPassManager
    {
        private readonly List<AstPass> passes = new List<AstPass>();

        public AstPassManager Add(AstPass pass)
        {
            passes.Add(pass);
            return this;
        }

        public void Run(AstBlock root)
        {
            foreach (var pass in passes)
                pass.Run(root);
        }
    }
}
