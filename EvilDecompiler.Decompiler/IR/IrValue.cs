using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.IR
{
    /// <summary>
    /// SSA 值节点基类。每个节点代表一个"值的生产者"，通过 def-use 引用形成表达式树。
    /// </summary>
    public abstract class IrValue
    {
        /// <summary>SSA 编号（函数内唯一）</summary>
        public int Id;

        /// <summary>是否有副作用（被 drop 时需要物化为独立语句）</summary>
        public virtual bool HasSideEffect => false;

        /// <summary>求值可能抛异常（如属性访问 null.x），drop 时也应保留</summary>
        public virtual bool MayThrow => false;

        /// <summary>
        /// 深度检查：表达式树中是否含有副作用/可能抛异常的子节点。
        /// 用于 drop 物化——逻辑表达式折叠可能把调用包进 && / || 里，
        /// 顶层 HasSideEffect 为 false 也不能丢。
        /// </summary>
        public virtual bool NeedsPreserve => HasSideEffect || MayThrow;

        /// <summary>运算符优先级（JS 标准，越大越紧；语句级为 0）</summary>
        public virtual int Precedence => 17; // 默认视为原子（字面量/变量）

        public abstract string Emit();
    }

    /// <summary>字面量常量（数字/字符串/true/false/null/undefined/this）</summary>
    public class IrConstant : IrValue
    {
        public string Text;
        public IrConstant(string text) { Text = text; }
        public override string Emit() => Text;
    }

    /// <summary>变量引用（局部变量/参数/全局/闭包变量）。名字来自 VarDefs/ClosureVarDefs 真名。</summary>
    public class IrVariable : IrValue
    {
        public string Name;
        public IrVariable(string name) { Name = name; }
        public override string Emit() => Name;
    }

        /// <summary>φ 节点：控制流汇合处的栈值合并</summary>
    public class IrPhi : IrValue
    {
        public List<IrValue> Sources = new List<IrValue>();
        public IrBlock? Block;

        public override bool NeedsPreserve => Sources.Any(s => s.NeedsPreserve);

        // ?? 输出的优先级
        public override int Precedence => 3;

        /// <summary>
        /// 尝试把 phi 折叠成普通值/表达式（全部同源、undefined 哨兵、?? 默认值模式）。
        /// 返回 null 表示无法折叠（仍是 phi_N 占位）。
        /// </summary>
        public IrValue? Fold()
        {
            if (Sources.Count == 0) return null;

            // 全部来源相同则折叠
            string first = Sources[0].Emit();
            if (Sources.All(s => s.Emit() == first)) return Sources[0];

            // 过滤 undefined 哨兵后只剩一个 → 直接用（可选链/洞检查场景）
            var nonUndef = Sources.Where(s => s.Emit() != "undefined").ToList();
            if (nonUndef.Count == 1) return nonUndef[0];

            // 两来源：一个是字面量（常量/容器）一个是表达式 → 默认值模式 E ?? C
            // （解构默认值 v === undefined ? C : v 的汇合）
            if (Sources.Count == 2)
            {
                bool IsLiteralLike(IrValue v) => v is IrConstant || v is IrLiteralContainer;
                // 第二个来源优先视为默认值（QuickJS 的 ?? 模板：替代值来自较短的后置路径）
                int constIdx = IsLiteralLike(Sources[1]) ? 1 : (IsLiteralLike(Sources[0]) ? 0 : -1);
                if (constIdx >= 0)
                {
                    IrValue expr = Sources[1 - constIdx];
                    return new IrBinaryOp("??", expr, Sources[constIdx], 3) { Id = Id };
                }
            }

            return null;
        }

        public override string Emit()
        {
            if (Sources.Count == 0) return "undefined /*phi*/";
            IrValue? folded = Fold();
            return folded != null ? folded.Emit() : "phi_" + Id.ToString();
        }
    }

    /// <summary>一元运算（typeof/-/~/! 等）</summary>
    public class IrUnaryOp : IrValue
    {
        public string Op;
        public IrValue Operand;
        public bool IsPrefix = true;
        /// <summary>++/-- 等副作用运算（drop 时需物化为语句）</summary>
        public bool IsSideEffect;
        public IrUnaryOp(string op, IrValue operand) { Op = op; Operand = operand; }
        public override bool HasSideEffect => IsSideEffect;
        public override int Precedence => 15;
        public override bool NeedsPreserve => IsSideEffect || Operand.NeedsPreserve;
        public override string Emit()
        {
            string v = Operand.Precedence < Precedence ? "(" + Operand.Emit() + ")" : Operand.Emit();
            string sep = Op.Length > 1 ? " " : ""; // typeof 等需要空格
            return IsPrefix ? Op + sep + v : v + Op;
        }
    }

    /// <summary>二元运算</summary>
    public class IrBinaryOp : IrValue
    {
        public string Op;
        public IrValue Left;
        public IrValue Right;
        public int OpPrecedence;
        public IrBinaryOp(string op, IrValue left, IrValue right, int precedence)
        {
            Op = op; Left = left; Right = right; OpPrecedence = precedence;
        }
        public override int Precedence => OpPrecedence;
        public override bool NeedsPreserve => Left.NeedsPreserve || Right.NeedsPreserve;
        public override string Emit()
        {
            string l = Left.Precedence < OpPrecedence ? "(" + Left.Emit() + ")" : Left.Emit();
            // 右操作数同级也要加括号（减/除不满足结合律）
            string r = Right.Precedence <= OpPrecedence ? "(" + Right.Emit() + ")" : Right.Emit();
            return l + " " + Op + " " + r;
        }
    }

    /// <summary>函数调用 / 方法调用 / new</summary>
    public class IrCall : IrValue
    {
        public IrValue Func;
        public IrValue? ThisArg;      // null = 普通调用（this=undefined）
        public List<IrValue> Args = new List<IrValue>();
        public bool IsConstructor;

        public IrCall(IrValue func, IrValue? thisArg, bool isConstructor)
        {
            Func = func; ThisArg = thisArg; IsConstructor = isConstructor;
        }

        public override bool HasSideEffect => true;
        public override int Precedence => 17;
        public override string Emit()
        {
            string args = string.Join(", ", Args.Select(a => a.Emit()));
            string callee;
            // get_field2/get_array_el2 产生的方法调用：Func 已是 this.method 形态，
            // 直接拼接会出现 obj.obj.method() 重复
            if (ThisArg != null && Func is IrGetProperty gp && gp.Object.Emit() == ThisArg.Emit())
            {
                callee = Func.Emit();
            }
            else if (ThisArg != null)
            {
                string t = ThisArg.Precedence < 17 ? "(" + ThisArg.Emit() + ")" : ThisArg.Emit();
                callee = t + "." + Func.Emit();
            }
            else
            {
                callee = Func.Precedence < 17 ? "(" + Func.Emit() + ")" : Func.Emit();
            }
            string call = callee + "(" + args + ")";
            return IsConstructor ? "new " + call : call;
        }
    }

    /// <summary>三元表达式 cond ? then : else（phi 汇合点识别产物）</summary>
    public class IrTernary : IrValue
    {
        public IrValue Condition;
        public IrValue Then;
        public IrValue Else;
        public IrTernary(IrValue condition, IrValue thenV, IrValue elseV)
        {
            Condition = condition; Then = thenV; Else = elseV;
        }
        public override int Precedence => 3;
        public override bool NeedsPreserve =>
            Condition.NeedsPreserve || Then.NeedsPreserve || Else.NeedsPreserve;
        public override string Emit()
        {
            string c = Condition.Precedence < 3 ? "(" + Condition.Emit() + ")" : Condition.Emit();
            string t = Then.Precedence < 3 ? "(" + Then.Emit() + ")" : Then.Emit();
            string e = Else.Precedence <= 3 ? "(" + Else.Emit() + ")" : Else.Emit();
            return c + " ? " + t + " : " + e;
        }
    }

    /// <summary>属性访问 obj.prop / obj[expr]</summary>
    public class IrGetProperty : IrValue
    {
        public IrValue Object;
        public IrValue? KeyExpr;   // 非 null = obj[expr]
        public string? KeyName;    // obj.name
        public IrGetProperty(IrValue obj, string keyName) { Object = obj; KeyName = keyName; }
        public IrGetProperty(IrValue obj, IrValue keyExpr) { Object = obj; KeyExpr = keyExpr; }
        // 属性访问可能抛 TypeError/触发 getter，被 drop 且栈上无副本时应保留为语句
        // （dup 模板里的 drop 由 DropValue 的栈引用检查过滤，不会因此产生噪音）
        public override bool MayThrow => true;
        public override bool NeedsPreserve =>
            base.NeedsPreserve || Object.NeedsPreserve || (KeyExpr != null && KeyExpr.NeedsPreserve);
        public override string Emit()
        {
            string o = Object.Precedence < 17 ? "(" + Object.Emit() + ")" : Object.Emit();
            if (KeyExpr != null)
                return o + "[" + KeyExpr.Emit() + "]";
            return o + "." + KeyName;
        }
    }

    /// <summary>字面量容器条目：静态键 / 计算键 [expr] / 展开 ...x / 数组元素</summary>
    public class IrContainerItem
    {
        public string? KeyName;     // 静态键（obj.key）
        public IrValue? KeyExpr;    // 计算键（obj[expr]）
        public IrValue Value = null!;
        public bool IsSpread;       // ...x
    }

    /// <summary>对象/数组字面量容器（后续 define_field/define_array_el 往里填充）</summary>
    public class IrLiteralContainer : IrValue
    {
        public bool IsArray;
        public List<IrContainerItem> Items = new List<IrContainerItem>();
        public override bool NeedsPreserve =>
            Items.Any(it => it.Value.NeedsPreserve || (it.KeyExpr != null && it.KeyExpr.NeedsPreserve));
        public override string Emit()
        {
            if (IsArray)
                return "[" + string.Join(", ", Items.Select(it => (it.IsSpread ? "..." : "") + it.Value.Emit())) + "]";
            return "{" + string.Join(", ", Items.Select(it =>
            {
                if (it.IsSpread) return "..." + it.Value.Emit();
                if (it.KeyExpr != null) return "[" + it.KeyExpr.Emit() + "]: " + it.Value.Emit();
                return (it.KeyName != null ? it.KeyName + ": " : "") + it.Value.Emit();
            })) + "}";
        }
    }

    /// <summary>闭包函数值（fclosure），函数体由 EmitPass 递归生成后命名</summary>
    public class IrClosureValue : IrValue
    {
        public JsFunctionBytecode Function;
        public string? ResolvedName;
        public IrClosureValue(JsFunctionBytecode func) { Function = func; }
        public override string Emit() => ResolvedName ?? ("closure_" + Id.ToString());
    }

    /// <summary>类定义值（define_class）：ctor + 原型/静态成员容器</summary>
    public class IrClassValue : IrValue
    {
        public string? Name;
        public IrValue? Parent;        // extends 父类（HasHeritage 时有效）
        public bool HasHeritage;
        public IrValue? Ctor;          // 构造函数（通常是 IrClosureValue）
        public IrLiteralContainer Proto = new IrLiteralContainer { IsArray = false };
        public IrLiteralContainer StaticItems = new IrLiteralContainer { IsArray = false };
        public override string Emit() => "class " + (Name ?? "") + " {/*...*/}";
    }

    /// <summary>
    /// for_of_next / for_in_next 产生的迭代器占位值。
    /// 用类型（而非变量名字符串）匹配，避免与用户变量撞名、兼容无符号 jsc。
    /// </summary>
    public class IrIteratorPlaceholder : IrValue
    {
        public enum Kind { OfValue, OfDone, InValue, InDone }
        public Kind K;
        public IrIteratorPlaceholder(Kind k) { K = k; }
        public override string Emit() => K switch
        {
            Kind.OfValue => "for_of_value",
            Kind.OfDone => "for_of_done",
            Kind.InValue => "for_in_value",
            _ => "for_in_done",
        };
    }

    /// <summary>
    /// 特殊栈标记：catch offset / gosub 返回地址 / for_of 迭代器哨兵（非普通数据值），
    /// 以及编译器伪变量（this_func=当前函数自身 / home_object / super 原型 / super 构造器）
    /// </summary>
    public class IrSpecialMarker : IrValue
    {
        public enum MarkerKind { CatchOffset, ReturnAddress, IteratorGuard, ThisFunc, HomeObject, SuperProto, SuperCtor }
        public MarkerKind Kind;
        public long TargetPc;
        public IrSpecialMarker(MarkerKind kind, long targetPc) { Kind = kind; TargetPc = targetPc; }
        // 输出合法的 JS 表达式（undefined + 注释），避免兜底路径产生语法错误
        public override string Emit() => Kind switch
        {
            MarkerKind.SuperProto or MarkerKind.SuperCtor => "super",
            MarkerKind.ThisFunc => "undefined /*this_func*/",
            MarkerKind.HomeObject => "undefined /*home_object*/",
            _ => "undefined /*marker:" + Kind + "@" + TargetPc + "*/"
        };
    }
}
