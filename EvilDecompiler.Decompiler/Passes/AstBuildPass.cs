using EvilDecompiler.Decompiler.AST;
using EvilDecompiler.Decompiler.IR;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.Passes
{
    /// <summary>
    /// Pass 5: IR 语句/表达式 → AST。闭包函数递归走完整条反编译管线。
    /// </summary>
    public class AstBuildPass
    {
        private IrFunction func = null!;
        private AtomSet atoms = null!;
        /// <summary>编译器临时变量名集合（标记 AstIdentifier.IsCompilerTemp 用）</summary>
        private HashSet<string> compilerTemps = new HashSet<string>();

        // 复用的临时函数表达式（BuildClosure/FuncDecl 递归填充）
        private AstFunctionExpr NewFnExpr() => new AstFunctionExpr();

        /// <summary>
        /// 构建函数 AST：局部变量头部声明 + 结构化语句转换。
        /// </summary>
        public AstBlock Build(IrFunction function, List<IrStatement> structured, List<string> tmpVariables, List<string> globalVars)
        {
            func = function;
            atoms = function.Atoms;
            compilerTemps = new HashSet<string>(tmpVariables);

            AstBlock root = new AstBlock();

            // 顶层全局 var 声明（define_var 收集）
            if (globalVars.Count > 0)
            {
                var varDecl = new AstVarDecl { Kind = "var" };
                foreach (string name in globalVars)
                    varDecl.Declarations.Add((name, null));
                root.Statements.Add(varDecl);
            }

            // 函数头统一声明局部变量（真名）和 tmp 变量
            // 注意排除与参数同名的（带默认参数/rest 时 QuickJS 会生成同名局部副本）
            List<string> argNames = function.GetArgNames();
            List<string> declNames = new List<string>();
            List<string> localNames = function.GetLocalNames();
            for (int i = 0; i < localNames.Count; i++)
            {
                // 编译器伪变量槽（this_func/home_object）不生成 let 声明：
                // this_func 由命名函数表达式名绑定替代，home_object 只服务 super 访问
                if (function.IsSpecialLocal(i))
                    continue;
                string name = localNames[i];
                if (name.Length > 0 && name != "this" && !declNames.Contains(name) && !argNames.Contains(name))
                    declNames.Add(name);
            }
            foreach (string tmp in tmpVariables)
            {
                if (!declNames.Contains(tmp))
                    declNames.Add(tmp);
            }

            if (declNames.Count > 0)
            {
                var decl = new AstVarDecl { Kind = "let", CompilerTemps = compilerTemps };
                foreach (string name in declNames)
                    decl.Declarations.Add((name, null));
                root.Statements.Add(decl);
            }

            foreach (IrStatement stmt in structured)
                root.Statements.Add(ConvertStmt(stmt));

            return root;
        }

        // ==================== 语句转换 ====================

        private AstStmt ConvertStmt(IrStatement stmt)
        {
            AstStmt result;
            switch (stmt)
            {
                case IrExprStatement s:
                    result = new AstExprStmt(ConvertExpr(s.Expr));
                    break;
                case IrAssign s:
                    if (s.Declare)
                    {
                        var decl = new AstVarDecl { Kind = "let" };
                        decl.Declarations.Add((s.Target, ConvertExpr(s.Value)));
                        result = decl;
                    }
                    else
                    {
                        result = new AstExprStmt(new AstAssignExpr(
                            new AstIdentifier(s.Target) { IsCompilerTemp = compilerTemps.Contains(s.Target) },
                            ConvertExpr(s.Value)));
                    }
                    break;
                case IrReturn s:
                    result = new AST.AstReturn { Value = s.Value != null ? ConvertExpr(s.Value) : null };
                    break;
                case IrThrow s:
                    result = new AST.AstThrow { Value = s.Value != null ? ConvertExpr(s.Value) : null };
                    break;
                case IrIf s:
                    {
                        var astIf = new AstIf { Condition = ConvertCondition(s.Condition, s.NegateCondition) };
                        foreach (var t in s.ThenBody) astIf.Then.Statements.Add(ConvertStmt(t));
                        if (s.ElseBody != null)
                        {
                            astIf.Else = new AstBlock();
                            foreach (var t in s.ElseBody) astIf.Else.Statements.Add(ConvertStmt(t));
                        }
                        result = astIf;
                        break;
                    }
                case IrWhile s:
                    {
                        var astWhile = new AST.AstWhile { Condition = ConvertCondition(s.Condition, s.NegateCondition) };
                        foreach (var t in s.Body) astWhile.Body.Statements.Add(ConvertStmt(t));
                        result = astWhile;
                        break;
                    }
                case IrDoWhile s:
                    {
                        var astDo = new AST.AstDoWhile { Condition = ConvertCondition(s.Condition, s.NegateCondition) };
                        foreach (var t in s.Body) astDo.Body.Statements.Add(ConvertStmt(t));
                        result = astDo;
                        break;
                    }
                case IrForOf s:
                    {
                        var astForOf = new AST.AstForOf { VarName = s.VarName, IsForIn = s.IsForIn, Iterable = ConvertExpr(s.Iterable) };
                        foreach (var t in s.Body) astForOf.Body.Statements.Add(ConvertStmt(t));
                        result = astForOf;
                        break;
                    }
                case IrSwitch s:
                    {
                        var astSwitch = new AST.AstSwitch { Discriminant = ConvertExpr(s.Discriminant) };
                        foreach (var c in s.Cases)
                        {
                            var ac = new AST.AstSwitchCase();
                            foreach (var l in c.Labels) ac.Labels.Add(ConvertExpr(l));
                            foreach (var t in c.Body) ac.Body.Statements.Add(ConvertStmt(t));
                            astSwitch.Cases.Add(ac);
                        }
                        result = astSwitch;
                        break;
                    }
                case IrTryCatch s:
                    {
                        var astTry = new AST.AstTryCatch { CatchVar = s.CatchVar };
                        foreach (var t in s.TryBody) astTry.TryBody.Statements.Add(ConvertStmt(t));
                        foreach (var t in s.CatchBody) astTry.CatchBody.Statements.Add(ConvertStmt(t));
                        if (s.FinallyBody != null)
                        {
                            astTry.FinallyBody = new AstBlock();
                            foreach (var t in s.FinallyBody) astTry.FinallyBody.Statements.Add(ConvertStmt(t));
                        }
                        result = astTry;
                        break;
                    }
                case IrBreak:
                    result = new AST.AstBreak();
                    break;
                case IrFuncDecl s:
                    {
                        var fnDecl = new AstFunctionDecl { Name = s.Name };
                        if (s.Closure != null)
                        {
                            try
                            {
                                var tmp = NewFnExpr();
                                QuickJsDecompilerV2.DecompileInto(s.Closure.Function, atoms, tmp);
                                fnDecl.Args = tmp.Args;
                                fnDecl.Body = tmp.Body;
                                fnDecl.IsAsync = tmp.IsAsync;
                            }
                            catch (Exception e)
                            {
                                fnDecl.Body.Statements.Add(new AstRaw("/* decompile failed: " + e.Message + " */"));
                            }
                        }
                        result = fnDecl;
                        break;
                    }
                case IrContinue:
                    result = new AST.AstContinue();
                    break;
                case IrLabel s:
                    result = new AstRaw("// label L" + s.TargetPcValue + ":");
                    break;
                case IrGoto s:
                    result = new AstRaw("// goto L" + s.TargetPcValue + ";");
                    break;
                case IrRawLine s:
                    result = new AstRaw(s.Text);
                    break;
                case IrIteratorStart s:
                    result = new AST.AstIteratorStart { Iterable = ConvertExpr(s.Iterable) };
                    break;
                default:
                    result = new AstRaw("// unknown IR statement: " + stmt.GetType().Name);
                    break;
            }
            result.Pc = stmt.Pc;
            return result;
        }

        private AstExpr ConvertCondition(IrValue cond, bool negate)
        {
            AstExpr expr = ConvertExpr(cond);
            // 取反时优先翻转比较运算符（!(a === b) → a !== b），减少否定嵌套
            if (negate)
                return AstPasses.IfNormalizePass.Negate(expr);
            return expr;
        }

        // ==================== 表达式转换 ====================

        private AstExpr ConvertExpr(IrValue v)
        {
            AstExpr result;
            switch (v)
            {
                case IrConstant c:
                    result = new AstLiteral(c.Text);
                    break;
                case IrVariable var:
                    result = new AstIdentifier(var.Name) { IsCompilerTemp = compilerTemps.Contains(var.Name) };
                    break;
                case IrIteratorPlaceholder itp:
                    result = new AST.AstIteratorValue
                    {
                        IsDone = itp.K == IrIteratorPlaceholder.Kind.OfDone || itp.K == IrIteratorPlaceholder.Kind.InDone,
                        IsForIn = itp.K == IrIteratorPlaceholder.Kind.InValue || itp.K == IrIteratorPlaceholder.Kind.InDone
                    };
                    break;
                case IrPhi phi:
                    {
                        // 可折叠的 phi（?? 默认值模式等）转为真实表达式节点，保留优先级信息
                        IrValue? folded = phi.Fold();
                        result = folded != null ? ConvertExpr(folded) : new AstLiteral(phi.Emit());
                        break;
                    }
                case IrUnaryOp un:
                    result = new AstUnary(un.Op, ConvertExpr(un.Operand)) { IsPrefix = un.IsPrefix };
                    break;
                case IrTernary tern:
                    result = new AstConditional(ConvertExpr(tern.Condition), ConvertExpr(tern.Then), ConvertExpr(tern.Else));
                    break;
                case IrBinaryOp bin:
                    result = new AstBinary(bin.Op, ConvertExpr(bin.Left), ConvertExpr(bin.Right), bin.OpPrecedence);
                    break;
                case IrCall call:
                    {
                        // call_method 的 func 通常已是 obj.method 形态（get_field2 保留 this），
                        // 直接用 func 表达式作 callee
                        AstExpr callee = ConvertExpr(call.Func);
                        // super(...) 是构造调用形态但语法上不带 new
                        bool isSuperCtor = call.Func is IrSpecialMarker scm
                            && scm.Kind == IrSpecialMarker.MarkerKind.SuperCtor;
                        if (call.IsConstructor && !isSuperCtor)
                        {
                            var nw = new AstNew(callee);
                            foreach (var a in call.Args) nw.Args.Add(ConvertExpr(a));
                            result = nw;
                        }
                        else
                        {
                            var c = new AstCall(callee);
                            foreach (var a in call.Args) c.Args.Add(ConvertExpr(a));
                            result = c;
                        }
                        break;
                    }
                case IrGetProperty prop:
                    result = prop.KeyExpr != null
                        ? new AstMember(ConvertExpr(prop.Object), ConvertExpr(prop.KeyExpr))
                        : new AstMember(ConvertExpr(prop.Object), prop.KeyName);
                    break;
                case IrLiteralContainer container:
                    if (container.IsArray)
                    {
                        var arr = new AstArrayLiteral();
                        foreach (var it in container.Items)
                            arr.Items.Add(new AstObjectItem { Key = null, Value = ConvertExpr(it.Value), IsSpread = it.IsSpread });
                        result = arr;
                    }
                    else
                    {
                        var obj = new AstObjectLiteral();
                        foreach (var it in container.Items)
                            obj.Items.Add(new AstObjectItem
                            {
                                Key = it.KeyName,
                                KeyExpr = it.KeyExpr != null ? ConvertExpr(it.KeyExpr) : null,
                                Value = ConvertExpr(it.Value),
                                IsSpread = it.IsSpread
                            });
                        result = obj;
                    }
                    break;
                case IrClosureValue closure:
                    result = BuildClosure(closure);
                    break;
                case IrClassValue cls:
                    result = BuildClass(cls);
                    break;
                case IrSpecialMarker marker:
                    // this_func 伪变量 → 当前函数名（命名函数表达式的自引用绑定）；
                    // 函数无名时退回占位文本（字节码缺符号的边缘情况）
                    if (marker.Kind == IrSpecialMarker.MarkerKind.ThisFunc && func.GetOwnFunctionName() != null)
                        result = new AstIdentifier(func.GetOwnFunctionName()!);
                    else
                        result = new AstLiteral(marker.Emit());
                    break;
                default:
                    result = new AstLiteral("/*ir?:" + v.GetType().Name + "*/");
                    break;
            }
            return result;
        }

        /// <summary>闭包递归：子函数走完整条管线</summary>
        private AstExpr BuildClosure(IrClosureValue closure)
        {
            var fn = new AstFunctionExpr();
            try
            {
                QuickJsDecompilerV2.DecompileInto(closure.Function, atoms, fn);
            }
            catch (Exception e)
            {
                fn.Name = null;
                fn.Body.Statements.Add(new AstRaw("/* closure decompile failed: " + e.Message + " */"));
            }
            return fn;
        }

        /// <summary>类值 → class 表达式（构造函数 + 原型成员 + 静态成员）</summary>
        private AstExpr BuildClass(IrClassValue cls)
        {
            var ce = new AstClassExpr { Name = cls.Name };
            if (cls.HasHeritage && cls.Parent != null)
                ce.SuperClass = ConvertExpr(cls.Parent);

            // 构造函数
            if (cls.Ctor is IrClosureValue ctorClosure)
            {
                AstExpr ctorExpr = BuildClosure(ctorClosure);
                if (ctorExpr is AstFunctionExpr ctorFn)
                {
                    ctorFn.Name = null;
                    ce.Members.Add(new AstObjectItem { Key = "constructor", Value = ctorFn });
                }
            }

            AppendClassMembers(ce, cls.Proto, false);
            AppendClassMembers(ce, cls.StaticItems, true);
            return ce;
        }

        private void AppendClassMembers(AstClassExpr ce, IrLiteralContainer container, bool isStatic)
        {
            foreach (var it in container.Items)
            {
                ce.Members.Add(new AstObjectItem
                {
                    Key = it.KeyName,
                    KeyExpr = it.KeyExpr != null ? ConvertExpr(it.KeyExpr) : null,
                    Value = ConvertExpr(it.Value),
                    IsSpread = it.IsSpread,
                    IsStatic = isStatic
                });
            }
        }
    }
}
