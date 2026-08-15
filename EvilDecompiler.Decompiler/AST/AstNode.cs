namespace EvilDecompiler.Decompiler.AST
{
    /// <summary>AST 节点基类</summary>
    public abstract class AstNode
    {
        /// <summary>来源 pc（调试追踪用，-1 表示无）</summary>
        public long Pc = -1;
    }

    // ==================== 表达式 ====================

    public abstract class AstExpr : AstNode
    {
        /// <summary>运算符优先级（用于打印时加括号，越大绑定越紧）</summary>
        public abstract int Precedence { get; }
    }

    /// <summary>字面量：数字/字符串/true/false/null/undefined</summary>
    public class AstLiteral : AstExpr
    {
        public string Text;
        public AstLiteral(string text) { Text = text; }
        public override int Precedence => 18;
    }

    /// <summary>标识符</summary>
    public class AstIdentifier : AstExpr
    {
        public string Name;
        /// <summary>编译器临时变量（dup 物化 tmpN）：类型标记，不依赖名字匹配</summary>
        public bool IsCompilerTemp;
        public AstIdentifier(string name) { Name = name; }
        public override int Precedence => 18;
    }

    /// <summary>迭代器占位值（for_of_next 的 value/done），类型匹配而非名字</summary>
    public class AstIteratorValue : AstExpr
    {
        public bool IsDone;
        public bool IsForIn;
        public override int Precedence => 18;
    }

    /// <summary>二元运算</summary>
    public class AstBinary : AstExpr
    {
        public string Op;
        public AstExpr Left;
        public AstExpr Right;
        public int OpPrecedence;
        public AstBinary(string op, AstExpr left, AstExpr right, int precedence)
        {
            Op = op; Left = left; Right = right; OpPrecedence = precedence;
        }
        public override int Precedence => OpPrecedence;
    }

    /// <summary>一元运算（typeof/!/-/~ 等）</summary>
    public class AstUnary : AstExpr
    {
        public string Op;
        public AstExpr Operand;
        public bool IsPrefix = true;
        public AstUnary(string op, AstExpr operand) { Op = op; Operand = operand; }
        public override int Precedence => 15;
    }

    /// <summary>函数调用</summary>
    public class AstCall : AstExpr
    {
        public AstExpr Callee;
        public List<AstExpr> Args = new List<AstExpr>();
        public AstCall(AstExpr callee) { Callee = callee; }
        public override int Precedence => 18;
    }

    /// <summary>new 表达式</summary>
    public class AstNew : AstExpr
    {
        public AstExpr Callee;
        public List<AstExpr> Args = new List<AstExpr>();
        public AstNew(AstExpr callee) { Callee = callee; }
        public override int Precedence => 18;
    }

    /// <summary>成员访问 obj.x 或 obj[expr]</summary>
    public class AstMember : AstExpr
    {
        public AstExpr Object;
        public string? KeyName;     // obj.name
        public AstExpr? KeyExpr;    // obj[expr]
        public AstMember(AstExpr obj, string keyName) { Object = obj; KeyName = keyName; }
        public AstMember(AstExpr obj, AstExpr keyExpr) { Object = obj; KeyExpr = keyExpr; }
        public override int Precedence => 18;
    }

    /// <summary>赋值表达式（a = b）</summary>
    public class AstAssignExpr : AstExpr
    {
        public AstExpr Target;
        public AstExpr Value;
        public AstAssignExpr(AstExpr target, AstExpr value) { Target = target; Value = value; }
        public override int Precedence => 2;
    }

    /// <summary>三元表达式</summary>
    public class AstConditional : AstExpr
    {
        public AstExpr Condition;
        public AstExpr ThenExpr;
        public AstExpr ElseExpr;
        public AstConditional(AstExpr c, AstExpr t, AstExpr e) { Condition = c; ThenExpr = t; ElseExpr = e; }
        public override int Precedence => 3;
    }

    /// <summary>对象字面量</summary>
    public class AstObjectLiteral : AstExpr
    {
        public List<AstObjectItem> Items = new List<AstObjectItem>();
        public override int Precedence => 18;
    }

    public class AstObjectItem
    {
        public string? Key;        // null = 数组元素/展开
        public AstExpr? KeyExpr;   // 计算键 [expr]
        public AstExpr Value = null!;
        public bool IsSpread;
        public bool IsMethod;      // 方法简写
        public bool IsStatic;      // class 静态成员
    }

    /// <summary>数组字面量</summary>
    public class AstArrayLiteral : AstExpr
    {
        public List<AstObjectItem> Items = new List<AstObjectItem>();
        public override int Precedence => 18;
    }

    /// <summary>函数表达式（闭包）</summary>
    public class AstFunctionExpr : AstExpr
    {
        public string? Name;
        public List<string> Args = new List<string>();
        public AstBlock Body = new AstBlock();
        public bool IsAsync;
        public override int Precedence => 18;
    }

    /// <summary>类表达式/声明</summary>
    public class AstClassExpr : AstExpr
    {
        public string? Name;
        public AstExpr? SuperClass;
        public List<AstObjectItem> Members = new List<AstObjectItem>();
        public override int Precedence => 18;
    }

    // ==================== 语句 ====================

    public abstract class AstStmt : AstNode { }

    /// <summary>语句块 { ... }</summary>
    public class AstBlock : AstStmt
    {
        public List<AstStmt> Statements = new List<AstStmt>();
    }

    /// <summary>表达式语句</summary>
    public class AstExprStmt : AstStmt
    {
        public AstExpr Expr;
        public AstExprStmt(AstExpr expr) { Expr = expr; }
    }

    /// <summary>变量声明 let/const/var</summary>
    public class AstVarDecl : AstStmt
    {
        public string Kind = "let";
        public List<(string Name, AstExpr? Init)> Declarations = new List<(string, AstExpr?)>();
        /// <summary>编译器临时变量名集合（函数头声明里的 tmpN 条目；TmpInlinePass 清理用）</summary>
        public HashSet<string>? CompilerTemps;
    }

    public class AstReturn : AstStmt
    {
        public AstExpr? Value;
    }

    public class AstThrow : AstStmt
    {
        public AstExpr? Value;
    }

    public class AstIf : AstStmt
    {
        public AstExpr Condition = null!;
        public AstBlock Then = new AstBlock();
        public AstBlock? Else;
    }

    public class AstWhile : AstStmt
    {
        public AstExpr Condition = null!;
        public AstBlock Body = new AstBlock();
    }

    public class AstDoWhile : AstStmt
    {
        public AstBlock Body = new AstBlock();
        public AstExpr Condition = null!;
    }

    public class AstBreak : AstStmt { }

    public class AstContinue : AstStmt { }

    /// <summary>for-of / for-in 循环</summary>
    public class AstForOf : AstStmt
    {
        public string VarName = "";
        public bool IsForIn;
        public AstExpr Iterable = null!;
        public AstBlock Body = new AstBlock();
    }

    /// <summary>switch 的一个 case（Labels 空 = default）</summary>
    public class AstSwitchCase
    {
        public List<AstExpr> Labels = new List<AstExpr>();
        public AstBlock Body = new AstBlock();
    }

    /// <summary>switch 语句</summary>
    public class AstSwitch : AstStmt
    {
        public AstExpr Discriminant = null!;
        public List<AstSwitchCase> Cases = new List<AstSwitchCase>();
    }

    /// <summary>try/catch/finally</summary>
    public class AstTryCatch : AstStmt
    {
        public AstBlock TryBody = new AstBlock();
        public string? CatchVar;
        public AstBlock CatchBody = new AstBlock();
        public AstBlock? FinallyBody;
    }

    /// <summary>函数声明</summary>
    public class AstFunctionDecl : AstStmt
    {
        public string Name = "";
        public List<string> Args = new List<string>();
        public AstBlock Body = new AstBlock();
        public bool IsAsync;
    }

    /// <summary>原始文本行（注释/兜底输出）</summary>
    public class AstRaw : AstStmt
    {
        public string Text;
        public AstRaw(string text) { Text = text; }
    }

    /// <summary>for_of_start 标记（数组解构重组用，未被消费时打印为注释）</summary>
    public class AstIteratorStart : AstStmt
    {
        public AstExpr Iterable = null!;
    }
}
