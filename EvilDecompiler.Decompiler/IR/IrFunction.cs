using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler.IR
{
    /// <summary>
    /// 函数级 IR 上下文：符号表（VarDefs 真名）+ SSA 编号分配 + 块列表。
    ///
    /// 栈帧布局（quickjs.c JS_CallInternal，权威）：
    ///   arg_buf[0..ArgCount)  → get_arg N 访问 → VarDefs[N]
    ///   var_buf[0..VarCount)  → get_loc N 访问 → VarDefs[ArgCount + N]
    /// var_ref N → ClosureVarDefs[N]（闭包捕获变量）
    /// </summary>
    public class IrFunction
    {
        public JsFunctionBytecode Bytecode;
        public AtomSet Atoms;
        public QuickJsInstruction[] Instructions = null!;
        public List<IrBlock> Blocks = new List<IrBlock>();

        /// <summary>dup 物化产生的编译器临时变量名集合（类型级标记，避免按名字前缀误判用户变量）</summary>
        public HashSet<string> CompilerTemps = new HashSet<string>();

        /// <summary>被 special_object(this_func) 初始化的局部槽索引：命名函数表达式的自引用绑定，不生成 let 声明</summary>
        public HashSet<int> ThisFuncLocals = new HashSet<int>();

        /// <summary>被 special_object(home_object) 初始化的局部槽索引：super 访问的宿主对象，不生成 let 声明</summary>
        public HashSet<int> HomeObjectLocals = new HashSet<int>();

        /// <summary>是否为编译器伪变量局部槽（this_func / home_object）</summary>
        public bool IsSpecialLocal(int idx) => ThisFuncLocals.Contains(idx) || HomeObjectLocals.Contains(idx);

        private int nextValueId;

        public IrFunction(JsFunctionBytecode bytecode, AtomSet atoms)
        {
            Bytecode = bytecode;
            Atoms = atoms;
        }

        public int AllocValueId() => nextValueId++;

        /// <summary>函数名（去 <> 包裹）</summary>
        public string GetFunctionName()
        {
            JsString? s = Atoms.Get(Bytecode.FunctionName);
            if (s == null) return "sub_" + Bytecode.GetHashCode().ToString("x");
            string name = s.Value.Replace("<", "").Replace(">", "");
            return name.Length > 0 ? name : "anonymous";
        }

        /// <summary>本函数名（与 QuickJsDecompilerV2.DecompileInto 的 Sanitize 规则一致）；无名返回 null</summary>
        public string? GetOwnFunctionName()
        {
            JsString? s = Atoms.Get(Bytecode.FunctionName);
            if (s == null || s.Value.Length == 0) return null;
            return s.Value.Replace("<", "").Replace(">", "").TrimEnd('\0');
        }

        /// <summary>参数名列表</summary>
        public List<string> GetArgNames()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < Bytecode.ArgCount; i++)
                names.Add(GetVarDefName(i, "arg" + i.ToString()));
            return names;
        }

        /// <summary>局部变量名列表（get_loc 索引 → 真名）</summary>
        public List<string> GetLocalNames()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < Bytecode.VarCount; i++)
                names.Add(GetVarDefName(Bytecode.ArgCount + i, "loc" + i.ToString()));
            return names;
        }

        /// <summary>get_arg N 对应的符号名</summary>
        public string GetArgName(int idx) => GetVarDefName(idx, "arg" + idx.ToString());

        /// <summary>get_loc N 对应的符号名</summary>
        public string GetLocName(int idx) => GetVarDefName(Bytecode.ArgCount + idx, "loc" + idx.ToString());

        /// <summary>var_ref N 对应的闭包变量名</summary>
        public string GetVarRefName(int idx)
        {
            if (idx >= 0 && idx < Bytecode.ClosureVarDefs.Count)
            {
                JsString? s = Atoms.Get(Bytecode.ClosureVarDefs[idx].VarName);
                if (s != null && s.Value.Length > 0)
                    return SanitizeName(s.Value);
            }
            return "var_ref" + idx.ToString();
        }

        private string GetVarDefName(int varDefIdx, string fallback)
        {
            if (varDefIdx >= 0 && varDefIdx < Bytecode.VarDefs.Count)
            {
                JsString? s = Atoms.Get(Bytecode.VarDefs[varDefIdx].VarName);
                if (s != null && s.Value.Length > 0)
                    return SanitizeName(s.Value);
            }
            return fallback;
        }

        private static string SanitizeName(string name)
        {
            // 魔改版 QuickJS 生成的匿名函数名带尾随 \0（anonyFunc0\0）；
            // 模块导出槽名 *default* 含非法标识符字符 *，替换为 $
            name = name.Replace("<", "").Replace(">", "").Replace("*", "$").TrimEnd('\0');
            // QuickJS 内部变量名如 "new.target" 含非法标识符字符，替换为 _
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = char.IsLetter(c) || c == '_' || c == '$' || (i > 0 && char.IsDigit(c));
                if (!ok)
                    name = name.Substring(0, i) + "_" + name.Substring(i + 1);
            }
            return name;
        }
    }
}
