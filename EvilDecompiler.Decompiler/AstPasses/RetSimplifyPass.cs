using EvilDecompiler.Decompiler.AST;

namespace EvilDecompiler.Decompiler.AstPasses
{
    /// <summary>消除自赋值噪音：x = x;（默认参数写回等产物）</summary>
    public class SelfAssignElimPass : AstPass
    {
        public override string Name => "SelfAssignElim";

        protected override bool VisitStmt(AstStmt stmt, AstBlock parent)
        {
            return stmt is AstExprStmt exprStmt
                && exprStmt.Expr is AstAssignExpr assign
                && assign.Target is AstIdentifier t
                && assign.Value is AstIdentifier v
                && t.Name == v.Name;
        }
    }

    /// <summary>
    /// eval 顶层 &lt;ret&gt; 变量噪音消除：
    /// QuickJS 把每个顶层表达式语句编译为 `ret = expr;`，最后 `return ret;`。
    /// 本 pass 把 ret 赋值还原为裸表达式语句，并删除 ret 声明和 return ret。
    /// </summary>
    public class EvalRetSimplifyPass : AstPass
    {
        public override string Name => "EvalRetSimplify";

        protected override bool VisitStmt(AstStmt stmt, AstBlock parent)
        {
            switch (stmt)
            {
                // ret = expr; → 副作用表达式转裸语句，纯表达式删除
                case AstExprStmt exprStmt
                    when exprStmt.Expr is AstAssignExpr assign
                    && assign.Target is AstIdentifier id && id.Name == "ret":
                    {
                        if (IsPure(assign.Value))
                            return true; // 纯值（含 undefined/常量/变量/纯运算）直接删除
                        exprStmt.Expr = assign.Value;
                        return false;
                    }

                // return ret; → 删除
                case AstReturn ret
                    when ret.Value is AstIdentifier id && id.Name == "ret":
                    return true;

                // let ..., ret, ... 声明里去掉 ret
                case AstVarDecl decl:
                    decl.Declarations.RemoveAll(d => d.Name == "ret" && d.Init == null);
                    return decl.Declarations.Count == 0;

                default:
                    return false;
            }
        }
        /// <summary>表达式是否无副作用（可安全删除）</summary>
        public static bool IsPure(AstExpr expr)
        {
            switch (expr)
            {
                case AstLiteral:
                case AstIdentifier:
                    return true;
                case AstBinary bin:
                    return IsPure(bin.Left) && IsPure(bin.Right);
                case AstUnary un:
                    if (un.Op == "delete" || un.Op == "++" || un.Op == "--") return false; // 有副作用
                    return IsPure(un.Operand);
                case AstMember member:
                    // 属性访问在 null/undefined 上抛 TypeError，不能视为纯表达式删除
                    return false;
                case AstArrayLiteral arr:
                    return arr.Items.All(it => IsPure(it.Value));
                case AstObjectLiteral obj:
                    return obj.Items.All(it => IsPure(it.Value));
                case AstFunctionExpr:
                    return true; // 函数表达式本身无副作用
                default:
                    return false; // Call/New/Assign/Conditional 等保守视为有副作用
            }
        }
    }
}
