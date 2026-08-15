using EvilDecompiler.ByteCode;
using EvilDecompiler.Decompiler.AST;
using EvilDecompiler.Decompiler.AstPasses;
using EvilDecompiler.Decompiler.IR;
using EvilDecompiler.Decompiler.Passes;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.Decompiler
{
    /// <summary>
    /// 新一代反编译器入口：Pass 管线驱动。
    ///
    /// 流水线：
    ///   字节码 → BasicBlockPass(CFG) → SsaLiftPass(SSA 提升)
    ///   → PhiPatchPass(φ 校验) → StructurePass(控制流结构化)
    ///   → AstBuildPass(IR→AST) → AST Pass 管线(折叠/合并/规范化)
    ///   → AstPrinter(代码生成)
    /// </summary>
    public class QuickJsDecompilerV2
    {
        /// <summary>反编译函数为 AST 根块（不含函数签名行）</summary>
        public static AstBlock DecompileFunction(JsFunctionBytecode fb, AtomSet atoms)
        {
            var fn = new AstFunctionExpr();
            DecompileInto(fb, atoms, fn);
            AstBlock root = new AstBlock();
            root.Statements.AddRange(fn.Body.Statements);
            return root;
        }

        /// <summary>反编译函数并填充到 AstFunctionExpr（闭包递归共用）</summary>
        public static void DecompileInto(JsFunctionBytecode fb, AtomSet atoms, AstFunctionExpr target)
        {
            IrFunction func = new IrFunction(fb, atoms);

            QuickJsDisAssembler disassembler = new QuickJsDisAssembler(new MemoryStream(fb.Bytecode), fb, atoms);
            func.Instructions = disassembler.ReadAllInstructions();

            var ctx = new IrFunctionContext(func);
            var ssaPass = new SsaLiftPass();
            var structurePass = new StructurePass();

            new PassManager()
                .Add(new BasicBlockPass())
                .Add(ssaPass)
                .Add(new PhiPatchPass())
                .Add(structurePass)
                .Run(ctx);

            var builder = new AstBuildPass();
            AstBlock body = builder.Build(func, structurePass.Result, ssaPass.TmpVariables, ssaPass.GlobalVars);

            // AST Pass 管线（先清噪音再规范化；IfNormalize 需在解构重组前清掉 hole-check 空 if，
            // 规范化需跑两遍处理连锁空 if）
            new AstPassManager()
                .Add(new SelfAssignElimPass())
                .Add(new EvalRetSimplifyPass())
                .Add(new IfNormalizePass())
                .Add(new DestructureArrayPass())
                .Add(new DeclInitMergePass())
                .Add(new DeadCodePass())
                .Add(new TmpInlinePass())
                .Add(new ConstantFoldPass())
                .Add(new IfNormalizePass())
                .Add(new DeadCodePass())
                .Run(body);

            target.Name = Sanitize(fb, atoms);
            target.Args.AddRange(func.GetArgNames());
            target.Body = body;
            // quickjs func_kind 位于 Flag 位 4-5：JS_FUNC_ASYNC = 2
            target.IsAsync = ((fb.FunctionFlag.Flag >> 4) & 3) >= 2;
        }

        /// <summary>完整反编译：输出带函数签名的 JS 文本</summary>
        public static string Decompile(JsFunctionBytecode fb, AtomSet atoms)
        {
            var fn = new AstFunctionExpr();
            DecompileInto(fb, atoms, fn);

            var decl = new AstFunctionDecl
            {
                Name = fn.Name ?? "anonymous",
                Args = fn.Args,
                Body = fn.Body,
                IsAsync = fn.IsAsync
            };

            return AstPrinter.Print(decl);
        }

        private static string? Sanitize(JsFunctionBytecode fb, AtomSet atoms)
        {
            JsString? s = atoms.Get(fb.FunctionName);
            if (s == null || s.Value.Length == 0) return null;
            return s.Value.Replace("<", "").Replace(">", "").TrimEnd('\0');
        }
    }
}
