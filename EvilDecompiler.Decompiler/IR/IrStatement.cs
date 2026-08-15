namespace EvilDecompiler.Decompiler.IR
{
    /// <summary>语句基类（SSA 提升后块内顺序执行的语句，或结构化后的控制流语句）</summary>
    public abstract class IrStatement
    {
        /// <summary>来源 pc（调试用）</summary>
        public long Pc = -1;

        public abstract void EmitTo(CodeWriter w);
    }

    /// <summary>带缩进的文本输出器</summary>
    public class CodeWriter
    {
        private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();
        public int Indent;

        public void Line(string text)
        {
            sb.Append(new string(' ', Indent * 4));
            sb.Append(text);
            sb.Append('\n');
        }

        public override string ToString() => sb.ToString();
    }

    /// <summary>裸表达式语句（副作用物化：f(); x++; 等）</summary>
    public class IrExprStatement : IrStatement
    {
        public IrValue Expr;
        public IrExprStatement(IrValue expr) { Expr = expr; }
        public override void EmitTo(CodeWriter w) => w.Line(Expr.Emit() + ";");
    }

    /// <summary>赋值/声明语句：let x = v / x = v</summary>
    public class IrAssign : IrStatement
    {
        public string Target;
        public IrValue Value;
        public bool Declare;   // true = let 声明
        public IrAssign(string target, IrValue value, bool declare = false)
        {
            Target = target; Value = value; Declare = declare;
        }
        public override void EmitTo(CodeWriter w)
        {
            w.Line((Declare ? "let " : "") + Target + " = " + Value.Emit() + ";");
        }
    }

    public class IrReturn : IrStatement
    {
        public IrValue? Value;
        public override void EmitTo(CodeWriter w) => w.Line(Value != null ? "return " + Value.Emit() + ";" : "return;");
    }

    public class IrThrow : IrStatement
    {
        public IrValue? Value;
        public override void EmitTo(CodeWriter w) => w.Line(Value != null ? "throw " + Value.Emit() + ";" : "throw;");
    }

    /// <summary>if / if-else（结构化产物）</summary>
    public class IrIf : IrStatement
    {
        public IrValue Condition = null!;
        public List<IrStatement> ThenBody = new List<IrStatement>();
        public List<IrStatement>? ElseBody;   // null = 无 else
        public bool NegateCondition;          // if_false 跳转语义：条件为假才跳

        public override void EmitTo(CodeWriter w)
        {
            string cond = Condition.Emit();
            if (NegateCondition)
                cond = "!(" + cond + ")";
            w.Line("if (" + cond + ") {");
            w.Indent++;
            foreach (var s in ThenBody) s.EmitTo(w);
            w.Indent--;
            if (ElseBody != null)
            {
                w.Line("} else {");
                w.Indent++;
                foreach (var s in ElseBody) s.EmitTo(w);
                w.Indent--;
            }
            w.Line("}");
        }
    }

    /// <summary>while 循环（结构化产物）</summary>
    public class IrWhile : IrStatement
    {
        public IrValue Condition = null!;
        public List<IrStatement> Body = new List<IrStatement>();
        public bool NegateCondition;

        public override void EmitTo(CodeWriter w)
        {
            string cond = Condition.Emit();
            if (NegateCondition)
                cond = "!(" + cond + ")";
            w.Line("while (" + cond + ") {");
            w.Indent++;
            foreach (var s in Body) s.EmitTo(w);
            w.Indent--;
            w.Line("}");
        }
    }

    /// <summary>do-while 循环（结构化产物）</summary>
    public class IrDoWhile : IrStatement
    {
        public List<IrStatement> Body = new List<IrStatement>();
        public IrValue Condition = null!;
        public bool NegateCondition;

        public override void EmitTo(CodeWriter w)
        {
            w.Line("do {");
            w.Indent++;
            foreach (var s in Body) s.EmitTo(w);
            w.Indent--;
            string cond = Condition.Emit();
            if (NegateCondition)
                cond = "!(" + cond + ")";
            w.Line("} while (" + cond + ");");
        }
    }

    /// <summary>for-of / for-in 循环（结构化产物）</summary>
    public class IrForOf : IrStatement
    {
        public string VarName = "";
        public bool IsForIn;
        public IrValue Iterable = null!;
        public List<IrStatement> Body = new List<IrStatement>();

        public override void EmitTo(CodeWriter w)
        {
            w.Line("for (let " + VarName + (IsForIn ? " in " : " of ") + Iterable.Emit() + ") {");
            w.Indent++;
            foreach (var s in Body) s.EmitTo(w);
            w.Indent--;
            w.Line("}");
        }
    }

    /// <summary>switch 的一个 case 分支（Labels 空 = default）</summary>
    public class IrSwitchCase
    {
        public List<IrValue> Labels = new List<IrValue>();
        public List<IrStatement> Body = new List<IrStatement>();
    }

    /// <summary>switch 语句（结构化产物：QuickJS 编译为 dup 判别式 + === 比较链）</summary>
    public class IrSwitch : IrStatement
    {
        public IrValue Discriminant = null!;
        public List<IrSwitchCase> Cases = new List<IrSwitchCase>();

        public override void EmitTo(CodeWriter w)
        {
            w.Line("switch (" + Discriminant.Emit() + ") {");
            w.Indent++;
            foreach (var c in Cases)
            {
                foreach (var l in c.Labels)
                    w.Line("case " + l.Emit() + ":");
                if (c.Labels.Count == 0)
                    w.Line("default:");
                w.Indent++;
                foreach (var s in c.Body) s.EmitTo(w);
                w.Indent--;
            }
            w.Indent--;
            w.Line("}");
        }
    }

    /// <summary>for_of_start 标记：记录被迭代表达式（数组解构重组用；for-of 循环识别时会摘除）</summary>
    public class IrIteratorStart : IrStatement
    {
        public IrValue Iterable;
        public IrIteratorStart(IrValue iterable) { Iterable = iterable; }
        public override void EmitTo(CodeWriter w) => w.Line("// iterator over " + Iterable.Emit());
    }

    /// <summary>try/catch/finally（结构化产物）</summary>
    public class IrTryCatch : IrStatement
    {
        public List<IrStatement> TryBody = new List<IrStatement>();
        public string? CatchVar;          // null = catch 无参数绑定
        public List<IrStatement> CatchBody = new List<IrStatement>();
        public List<IrStatement>? FinallyBody;   // null = 无 finally

        public override void EmitTo(CodeWriter w)
        {
            w.Line("try {");
            w.Indent++;
            foreach (var s in TryBody) s.EmitTo(w);
            w.Indent--;
            w.Line("} catch (" + (CatchVar ?? "") + ") {");
            w.Indent++;
            foreach (var s in CatchBody) s.EmitTo(w);
            w.Indent--;
            if (FinallyBody != null)
            {
                w.Line("} finally {");
                w.Indent++;
                foreach (var s in FinallyBody) s.EmitTo(w);
                w.Indent--;
            }
            w.Line("}");
        }
    }

    /// <summary>无法结构化时的兜底：标签</summary>
    public class IrLabel : IrStatement
    {
        public long TargetPcValue;
        public IrLabel(long pc) { TargetPcValue = pc; }
        public override void EmitTo(CodeWriter w) => w.Line("// label L" + TargetPcValue + ":");
    }

    /// <summary>无法结构化时的兜底：跳转</summary>
    public class IrGoto : IrStatement
    {
        public long TargetPcValue;
        public IrGoto(long pc) { TargetPcValue = pc; }
        public override void EmitTo(CodeWriter w) => w.Line("// goto L" + TargetPcValue + ";");
    }

    /// <summary>原始文本行（调试/注释）</summary>
    public class IrRawLine : IrStatement
    {
        public string Text;
        public IrRawLine(string text) { Text = text; }
        public override void EmitTo(CodeWriter w) => w.Line(Text);
    }

    /// <summary>break（结构化循环内跳转识别产物）</summary>
    public class IrBreak : IrStatement
    {
        public override void EmitTo(CodeWriter w) => w.Line("break;");
    }

    /// <summary>continue（结构化循环内跳转识别产物）</summary>
    public class IrContinue : IrStatement
    {
        public override void EmitTo(CodeWriter w) => w.Line("continue;");
    }

    /// <summary>函数声明（define_func：栈顶闭包值定义为全局函数）</summary>
    public class IrFuncDecl : IrStatement
    {
        public string Name = "";
        public IrClosureValue? Closure;
        public override void EmitTo(CodeWriter w) => w.Line("// function " + Name + " (decl)");
    }
}
