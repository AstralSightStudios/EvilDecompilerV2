namespace EvilDecompiler.Decompiler.Passes
{
    /// <summary>函数级分析/转换 Pass 接口</summary>
    public interface IFunctionPass
    {
        string Name { get; }
        void Run(IrFunctionContext ctx);
    }

    /// <summary>Pass 运行上下文：贯穿整条管线的共享状态</summary>
    public class IrFunctionContext
    {
        public IR.IrFunction Function;

        public IrFunctionContext(IR.IrFunction function)
        {
            Function = function;
        }
    }

    /// <summary>Pass 管线管理器</summary>
    public class PassManager
    {
        private readonly List<IFunctionPass> passes = new List<IFunctionPass>();

        public PassManager Add(IFunctionPass pass)
        {
            passes.Add(pass);
            return this;
        }

        public void Run(IrFunctionContext ctx)
        {
            foreach (var pass in passes)
                pass.Run(ctx);
        }
    }
}
