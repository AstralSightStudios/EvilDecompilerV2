namespace EvilDecompiler.Decompiler.AST
{
    /// <summary>AST → JS 文本打印器（含优先级括号处理）</summary>
    public class AstPrinter
    {
        private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();
        private int indent;

        public static string Print(AstNode node)
        {
            var p = new AstPrinter();
            p.PrintNode(node);
            return p.sb.ToString();
        }

        private void Line(string text)
        {
            sb.Append(new string(' ', indent * 4));
            sb.Append(text);
            sb.Append('\n');
        }

        public void PrintNode(AstNode node)
        {
            switch (node)
            {
                case AstBlock block: PrintBlock(block); break;
                case AstExprStmt s:
                    {
                        // 以 { 开头的表达式语句必须加括号（否则被解析为语句块）
                        string exprText = PrintExpr(s.Expr);
                        if (exprText.StartsWith("{"))
                            exprText = "(" + exprText + ")";
                        Line(exprText + ";");
                        break;
                    }
                case AstVarDecl decl: PrintVarDecl(decl); break;
                case AstReturn r: Line(r.Value != null ? "return " + PrintExpr(r.Value) + ";" : "return;"); break;
                case AstThrow t: Line(t.Value != null ? "throw " + PrintExpr(t.Value) + ";" : "throw;"); break;
                case AstIf ifStmt: PrintIf(ifStmt); break;
                case AstWhile w: PrintWhile(w); break;
                case AstDoWhile dw: PrintDoWhile(dw); break;
                case AstForOf fo: PrintForOf(fo); break;
                case AstSwitch sw: PrintSwitch(sw); break;
                case AstTryCatch tc: PrintTryCatch(tc); break;
                case AstBreak: Line("break;"); break;
                case AstContinue: Line("continue;"); break;
                case AstFunctionDecl fn: PrintFunctionDecl(fn); break;
                case AstRaw raw: Line(raw.Text); break;
                case AstIteratorStart its: Line("// iterator over " + PrintExpr(its.Iterable)); break;
                default: Line("/* unknown ast node: " + node.GetType().Name + " */"); break;
            }
        }

        private void PrintBlock(AstBlock block)
        {
            foreach (var s in block.Statements)
                PrintNode(s);
        }

        /// <summary>供 AstFunctionExpr 打印用的块序列化（保持当前缩进）</summary>
        public string PrintBodyToString(AstBlock block)
        {
            PrintBlock(block);
            return sb.ToString();
        }

        private void PrintVarDecl(AstVarDecl decl)
        {
            List<string> parts = new List<string>();
            foreach (var (name, init) in decl.Declarations)
            {
                parts.Add(init != null ? name + " = " + PrintExpr(init) : name);
            }
            Line(decl.Kind + " " + string.Join(", ", parts) + ";");
        }

        private void PrintIf(AstIf ifStmt)
        {
            Line("if (" + PrintExpr(ifStmt.Condition) + ") {");
            indent++;
            PrintBlock(ifStmt.Then);
            indent--;
            if (ifStmt.Else != null)
            {
                Line("} else {");
                indent++;
                PrintBlock(ifStmt.Else);
                indent--;
            }
            Line("}");
        }

        private void PrintWhile(AstWhile w)
        {
            Line("while (" + PrintExpr(w.Condition) + ") {");
            indent++;
            PrintBlock(w.Body);
            indent--;
            Line("}");
        }

        private void PrintDoWhile(AstDoWhile dw)
        {
            Line("do {");
            indent++;
            PrintBlock(dw.Body);
            indent--;
            Line("} while (" + PrintExpr(dw.Condition) + ");");
        }

        private void PrintForOf(AstForOf fo)
        {
            Line("for (let " + fo.VarName + (fo.IsForIn ? " in " : " of ") + PrintExpr(fo.Iterable) + ") {");
            indent++;
            PrintBlock(fo.Body);
            indent--;
            Line("}");
        }

        private void PrintSwitch(AstSwitch sw)
        {
            Line("switch (" + PrintExpr(sw.Discriminant) + ") {");
            indent++;
            foreach (var c in sw.Cases)
            {
                foreach (var l in c.Labels)
                    Line("case " + PrintExpr(l) + ":");
                if (c.Labels.Count == 0)
                    Line("default:");
                indent++;
                PrintBlock(c.Body);
                indent--;
            }
            indent--;
            Line("}");
        }

        private void PrintTryCatch(AstTryCatch tc)
        {
            Line("try {");
            indent++;
            PrintBlock(tc.TryBody);
            indent--;
            Line("} catch (" + (tc.CatchVar ?? "") + ") {");
            indent++;
            PrintBlock(tc.CatchBody);
            indent--;
            if (tc.FinallyBody != null)
            {
                Line("} finally {");
                indent++;
                PrintBlock(tc.FinallyBody);
                indent--;
            }
            Line("}");
        }

        private void PrintFunctionDecl(AstFunctionDecl fn)
        {
            Line((fn.IsAsync ? "async " : "") + "function " + fn.Name + "(" + string.Join(", ", fn.Args) + ") {");
            indent++;
            PrintBlock(fn.Body);
            indent--;
            Line("}");
        }

        // ==================== 表达式打印（优先级括号） ====================

        public string PrintExpr(AstExpr expr, int parentPrecedence = 0, bool isRightChild = false)
        {
            string text = PrintExprRaw(expr);
            // 右子节点同级也要加括号（减/除/比较不满足结合律）
            bool needParens = expr.Precedence < parentPrecedence
                || (isRightChild && expr.Precedence == parentPrecedence);
            return needParens ? "(" + text + ")" : text;
        }

        private string PrintExprRaw(AstExpr expr)
        {
            switch (expr)
            {
                case AstLiteral lit: return lit.Text;
                case AstIdentifier id: return id.Name;
                case AstIteratorValue itv:
                    return itv.IsForIn ? (itv.IsDone ? "for_in_done" : "for_in_value")
                        : (itv.IsDone ? "for_of_done" : "for_of_value");
                case AstBinary bin:
                    {
                        string l = PrintExpr(bin.Left, bin.Precedence);
                        string r = PrintExpr(bin.Right, bin.Precedence, true);
                        // ?? 与 && / || 混用时 JS 语法强制要求括号
                        if (bin.Op == "??" || bin.Op == "&&" || bin.Op == "||")
                        {
                            if (bin.Left is AstBinary lb && lb.Op != bin.Op
                                && (lb.Op == "??" || lb.Op == "&&" || lb.Op == "||"))
                                l = "(" + l + ")";
                            if (bin.Right is AstBinary rb && rb.Op != bin.Op
                                && (rb.Op == "??" || rb.Op == "&&" || rb.Op == "||"))
                                r = "(" + r + ")";
                        }
                        return l + " " + bin.Op + " " + r;
                    }
                case AstUnary un:
                    {
                        string v = PrintExpr(un.Operand, un.Precedence);
                        string sep = un.Op.Length > 1 ? " " : "";
                        return un.IsPrefix ? un.Op + sep + v : v + un.Op;
                    }
                case AstCall call:
                    {
                        // 函数表达式作 callee 必须加括号：(function(){...})()
                        string calleeText = PrintExpr(call.Callee, 18);
                        if (call.Callee is AstFunctionExpr)
                            calleeText = "(" + calleeText + ")";
                        return calleeText + "(" + string.Join(", ", call.Args.Select(a => PrintExpr(a))) + ")";
                    }
                case AstNew nw:
                    {
                        // 函数/类表达式作 new 的目标必须加括号
                        string newCallee = PrintExpr(nw.Callee, 18);
                        if (nw.Callee is AstFunctionExpr || nw.Callee is AstClassExpr)
                            newCallee = "(" + newCallee + ")";
                        return "new " + newCallee + "(" + string.Join(", ", nw.Args.Select(a => PrintExpr(a))) + ")";
                    }
                case AstMember member:
                    {
                        string o = PrintExpr(member.Object, 18);
                        if (member.KeyExpr != null)
                            return o + "[" + PrintExpr(member.KeyExpr) + "]";
                        // 非法标识符属性名走 ["..."] 形式
                        if (!IsValidIdentifier(member.KeyName!))
                            return o + "[\"" + member.KeyName + "\"]";
                        return o + "." + member.KeyName;
                    }
                case AstAssignExpr assign:
                    return PrintExpr(assign.Target, 2) + " = " + PrintExpr(assign.Value, 2, true);
                case AstConditional cond:
                    return PrintExpr(cond.Condition, 3) + " ? " + PrintExpr(cond.ThenExpr, 3) + " : " + PrintExpr(cond.ElseExpr, 3, true);
                case AstObjectLiteral obj:
                    return "{" + string.Join(", ", obj.Items.Select(PrintObjectItem)) + "}";
                case AstArrayLiteral arr:
                    return "[" + string.Join(", ", arr.Items.Select(PrintObjectItem)) + "]";
                case AstFunctionExpr fn:
                    {
                        // 函数体需要多行打印：用子打印器生成后拼接
                        var sub = new AstPrinter();
                        sub.indent = indent + 1;
                        string header = (fn.IsAsync ? "async " : "") + "function " + (fn.Name ?? "") + "(" + string.Join(", ", fn.Args) + ") {";
                        string body = sub.PrintBodyToString(fn.Body);
                        string closing = new string(' ', indent * 4) + "}";
                        // 嵌入表达式上下文：整体作为一个多行字符串
                        return header + "\n" + body + closing;
                    }
                case AstClassExpr ce:
                    {
                        string header = "class " + (ce.Name ?? "")
                            + (ce.SuperClass != null ? " extends " + PrintExpr(ce.SuperClass) : "") + " {";
                        var sb2 = new System.Text.StringBuilder();
                        sb2.Append(header).Append('\n');
                        indent++;
                        string memberIndent = new string(' ', indent * 4);
                        foreach (var m in ce.Members)
                            sb2.Append(memberIndent).Append(PrintClassMember(m)).Append('\n');
                        indent--;
                        sb2.Append(new string(' ', indent * 4)).Append('}');
                        return sb2.ToString();
                    }
                default:
                    return "/*expr?:" + expr.GetType().Name + "*/";
            }
        }

        private string PrintObjectItem(AstObjectItem item)
        {
            if (item.IsSpread)
                return "..." + PrintExpr(item.Value);
            // 稀疏数组空洞
            if (item.Value is AstLiteral hole && hole.Text == "/*hole*/")
                return "";
            // 计算键：[expr]: v 或 [expr]() { ... }
            if (item.KeyExpr != null)
            {
                string ck = "[" + PrintExpr(item.KeyExpr) + "]";
                if (item.Value is AstFunctionExpr cFn)
                    return (cFn.IsAsync ? "async " : "") + ck + PrintFunctionTail(cFn);
                return ck + ": " + PrintExpr(item.Value);
            }
            if (item.Key != null)
            {
                // get/set 前缀：get double() { ... }
                if ((item.Key.StartsWith("get ") || item.Key.StartsWith("set "))
                    && item.Value is AstFunctionExpr gsFn)
                    return item.Key + PrintFunctionTail(gsFn);
                // 方法简写：name(args) { ... }（仅合法标识符键；数字/引号键用 "key": function 形式）
                if (item.Value is AstFunctionExpr mFn)
                {
                    if (IsValidIdentifier(item.Key))
                        return (mFn.IsAsync ? "async " : "") + FormatKey(item.Key) + PrintFunctionTail(mFn);
                    return FormatKey(item.Key) + ": " + PrintExpr(item.Value);
                }
                return FormatKey(item.Key) + ": " + PrintExpr(item.Value);
            }
            return PrintExpr(item.Value);
        }

        /// <summary>函数表达式的 "(args) { body }" 部分（多行）</summary>
        private string PrintFunctionTail(AstFunctionExpr fn)
        {
            var sub = new AstPrinter();
            sub.indent = indent + 1;
            string body = sub.PrintBodyToString(fn.Body);
            string closing = new string(' ', indent * 4) + "}";
            return "(" + string.Join(", ", fn.Args) + ") {\n" + body + closing;
        }

        /// <summary>class 成员：constructor/method/get/set/static/字段</summary>
        private string PrintClassMember(AstObjectItem item)
        {
            string prefix = item.IsStatic ? "static " : "";
            if (item.KeyExpr != null)
            {
                string ck = "[" + PrintExpr(item.KeyExpr) + "]";
                if (item.Value is AstFunctionExpr cFn)
                    return prefix + (cFn.IsAsync ? "async " : "") + ck + PrintFunctionTail(cFn);
                return prefix + ck + " = " + PrintExpr(item.Value) + ";";
            }
            string key = item.Key ?? "?";
            if ((key.StartsWith("get ") || key.StartsWith("set ")) && item.Value is AstFunctionExpr gsFn)
                return prefix + key + PrintFunctionTail(gsFn);
            if (item.Value is AstFunctionExpr mFn)
                return prefix + (mFn.IsAsync ? "async " : "") + FormatKey(key) + PrintFunctionTail(mFn);
            // 字段（含静态字段）：class 字段语法
            return prefix + FormatKey(key) + " = " + PrintExpr(item.Value) + ";";
        }

        /// <summary>合法标识符原样输出，否则加引号</summary>
        private static string FormatKey(string key)
        {
            return IsValidIdentifier(key) ? key : "\"" + key + "\"";
        }

        private static bool IsValidIdentifier(string name)
        {
            if (name.Length == 0) return false;
            if (!(char.IsLetter(name[0]) || name[0] == '_' || name[0] == '$')) return false;
            foreach (char c in name)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$')) return false;
            return true;
        }
    }
}
