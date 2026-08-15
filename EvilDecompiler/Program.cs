using EvilDecompiler.ByteCode;
using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.Decompiler;
using EvilDecompiler.JsObject;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler
{
    internal class Program
    {

        static string StripJscExtension(string path)
        {
            if (path.EndsWith(".jsc", StringComparison.OrdinalIgnoreCase))
                return path.Substring(0, path.Length - 4);
            return path;
        }

        static JsFunctionBytecode? GetFunctionBytecode(JsObjectReader jsObjectReader)
        {
            if (jsObjectReader.JsObject is JsModule module)
                return module.FunctionObject as JsFunctionBytecode;

            return jsObjectReader.JsObject as JsFunctionBytecode;
        }

        static void Disassemble(string path)
        {
            JsObjectReader jsObjectReader = new JsObjectReader(new MemoryStream(File.ReadAllBytes(path)));
            JsFunctionBytecode? functionBytecode = GetFunctionBytecode(jsObjectReader);

            if (functionBytecode == null || jsObjectReader.Atoms == null)
            {
                Console.WriteLine("Internal Error!");
                return;
            }

            string result = DisassembleRecursive(functionBytecode, jsObjectReader.Atoms, "<eval>");

            Console.WriteLine(result);

            File.WriteAllText(StripJscExtension(path) + ".disasm.txt", result);
        }

        /// <summary>递归反汇编：顶层函数 + 常量池里的所有嵌套闭包</summary>
        static string DisassembleRecursive(JsFunctionBytecode fb, AtomSet atoms, string label)
        {
            QuickJsDisAssembler disAssembler = new QuickJsDisAssembler(new MemoryStream(fb.Bytecode), fb, atoms);
            QuickJsInstruction[] ins = disAssembler.ReadAllInstructions();

            string result = "===== " + label + " =====\n";
            for (int i = 0; i < ins.Length; i++)
                result += ins[i].ToString() + "\n";

            foreach (var c in fb.CPool)
            {
                if (c is JsFunctionBytecode sub)
                {
                    string name = atoms.Get(sub.FunctionName)?.Value?.TrimEnd('\0') ?? "anonymous";
                    result += "\n" + DisassembleRecursive(sub, atoms, label + "/" + name);
                }
            }
            return result;
        }

        static void Decompile(string path, bool detail)
        {
            JsObjectReader jsObjectReader = new JsObjectReader(new MemoryStream(File.ReadAllBytes(path)));
            JsFunctionBytecode? functionBytecode = GetFunctionBytecode(jsObjectReader);

            if (functionBytecode == null || jsObjectReader.Atoms == null)
            {
                Console.WriteLine("Internal Error!");
                return;
            }

            string result;
            try
            {
                result = QuickJsDecompilerV2.Decompile(functionBytecode, jsObjectReader.Atoms);
            }
            catch (Exception e)
            {
                result = "/* decompile failed: " + e.Message + "\n" + e.StackTrace + "\n*/";
            }

            Console.WriteLine(result);

            File.WriteAllText(StripJscExtension(path) + ".decompiled.js", result);
        }

        static void Rewrite(string path)
        {
            JsObjectReader jsObjectReader = new JsObjectReader(new MemoryStream(File.ReadAllBytes(path)));

            if (jsObjectReader.JsObject == null || jsObjectReader.Atoms == null)
            {
                Console.WriteLine("Internal Error!");
                return;
            }

            string outPath = StripJscExtension(path) + ".rewritten.jsc";

            using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                new JsObjectWriter(fs, jsObjectReader.JsObject, jsObjectReader.Atoms);
            }

            Console.WriteLine("Rewritten to " + outPath);
        }

        static void PrintUsages()
        {
            Console.WriteLine("EvilDecompiler.exe <all|disassemble|decompile|decompile-detail|rewrite> [jsc file]");
        }

        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                PrintUsages();
                return 1;
            }

            if (!File.Exists(args[1]))
            {
                Console.WriteLine("File " + args[1] + " does not exist!");
                return 1;
            }

            try
            {
                switch (args[0])
                {
                    case "all":
                        Disassemble(args[1]);
                        Decompile(args[1], true);
                        break;
                    case "disassemble":
                        Disassemble(args[1]);
                        break;
                    case "decompile":
                        Decompile(args[1], false);
                        break;
                    case "decompile-detail":
                        Decompile(args[1], true);
                        break;
                    case "rewrite":
                        Rewrite(args[1]);
                        break;

                    default:
                        PrintUsages();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 2;
            }

            return 0;
        }
    }
}
