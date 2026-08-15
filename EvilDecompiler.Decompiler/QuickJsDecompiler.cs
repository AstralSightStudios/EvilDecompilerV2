using EvilDecompiler.ByteCode;
using EvilDecompiler.ByteCode.Instruction;
using EvilDecompiler.Decompiler.Analyze;
using EvilDecompiler.Decompiler.Instruction;
using EvilDecompiler.JsObject.Types;
using EvilDecompiler.JsObject.Types.Objects;
using System.Text;

namespace EvilDecompiler.Decompiler
{
    public class QuickJsDecompiler
    {
        protected AtomSet atomSet;

        protected JsFunctionBytecode function;

        protected QuickJsInstruction[] ins;

        public QuickJsDecompiler(JsFunctionBytecode func, AtomSet atoms)
        {
            QuickJsDisAssembler disassembler = new QuickJsDisAssembler(new MemoryStream(func.Bytecode), func, atoms);
            ins = disassembler.ReadAllInstructions();

            QuickJsInstructionLifter lifter = new QuickJsInstructionLifter(ins);
            ins = lifter.LiftToIR();

            function =  func;
            atomSet = atoms;
        }

        private void BuildLocalVarDefine(ref StringBuilder builder, int padding)
        {

            if (function.VarCount == 0)
                return;

            builder.Append(new string(' ', padding * 4));

            builder.Append("let ");
            for (int i = 0; i < function.VarCount; i++)
            {
                if (i > 0)
                    builder.Append(", loc" + i.ToString());
                else
                    builder.Append("loc" + i.ToString());
            }
            builder.Append(";");
        }

        private void BuildFunctionDefine(ref StringBuilder builder, int padding)
        {
            builder.Append(new string(' ', padding * 4));

            builder.Append("function ");

            string funcName = "sub_" + function.GetHashCode().ToString();

            JsString? atomString = atomSet.Get(function.FunctionName);

            if (atomString != null)
            {
                funcName = atomString.Value.Replace("<", "").Replace(">", "");
            }

            builder.Append(funcName);

            builder.Append("(");

            for (int i = 0; i < function.ArgCount; i++)
            {
                if (i > 0)
                    builder.Append(", arg" + i.ToString());
                else
                    builder.Append("arg" + i.ToString());
            }

            builder.Append(")");
        }

        public string Decompile(bool detail = false, int padding = 0)
        {

            StringBuilder builder = new StringBuilder();

            BuildFunctionDefine(ref builder, padding);

            builder.Append('\n');
            builder.Append(new string(' ', padding * 4));
            builder.Append("{\n");

            padding++;

            BuildLocalVarDefine(ref builder, padding);

            Stack<string> stack = new Stack<string>();
            List<string> data = new List<string>();

            for (int i = 0; i < ins.Length; i++)
            {

                QuickJsInstruction curIns = ins[i];

                if (detail)
                {
                    // debug
                    string[] stackArray = stack.ToArray();
                    for (int j = 0; j < stack.Count; j++)
                    {
                        builder.Append('\n');
                        builder.Append(new string(' ', padding * 4));
                        builder.Append("// Stack: " + (stack.Count() - j).ToString() + ", Value: " + stackArray[j]);
                    }

                    builder.Append('\n');
                    builder.Append(new string(' ', padding * 4));
                    builder.Append("// Instruction: " + ins[i].ToString());

                    builder.Append('\n');
                }

                switch (curIns)
                {

                    case QuickJsInstructionGetVar getVar:
                        stack.Push(getVar.Value);
                        break;

                    case QuickJsInstructionGetProperty getProperty:

                        // 判断是否要在栈中保留原始变量

                        string num = "";

                        if (getProperty.StackValue)
                            num = stack.Pop();

                        string origin;

                        if (getProperty.PushOrigin)
                        {
                            origin = stack.Peek();
                        }
                        else
                        {
                            origin = stack.Pop();
                        }

                        if (getProperty.StackValue)
                            stack.Push(origin + "[" + num + "]");
                        else
                            stack.Push(origin + "[\"" + getProperty.Value + "\"]");

                        break;

                    case QuickJsInstructionPushValue pushValue:
                        stack.Push(pushValue.Value);
                        break;

                    case QuickJsInstructionTypeOf typeOf:
                        if (typeOf.IsType == "")
                        {
                            stack.Push("typeof " + stack.Pop());
                        }
                        else
                        {
                            stack.Push("typeof " + stack.Pop() + " === " + typeOf.IsType);
                        }
                        break;

                    case QuickJsInstructionMath math:
                        string secondNumber = stack.Pop();
                        string firstNumber = stack.Pop();

                        stack.Push("(" + firstNumber + " " + math.Symbol + " " + secondNumber + ")");
                        break;

                    case QuickJsInstructionCompare compare:
                        string secondValue = stack.Pop();
                        string firstValue = stack.Pop();

                        stack.Push(firstValue + " " + compare.Symbol + " " + secondValue);

                        break;

                    case QuickJsInstructionClosure closure:

                        if (closure.Function != null)
                        {

                            string funcName = "sub_" + function.GetHashCode().ToString();

                            JsString? atomString = atomSet.Get(function.FunctionName);

                            if (atomString != null)
                            {
                                funcName = atomString.Value.Replace("<", "").Replace(">", "");
                            }

                            try
                            {
                                QuickJsDecompiler closureDecompiler = new QuickJsDecompiler(closure.Function, atomSet);
                                builder.Append('\n');
                                builder.Append(closureDecompiler.Decompile(detail, padding));
                                stack.Push(funcName);
                            }
                            catch (Exception e)
                            {
                                builder.Append("\n /*\n " + e.ToString() + "\n*/");
                                stack.Push(funcName);
                            }

                        }
                        else
                        {
                            stack.Push("null");
                        }

                        break;

                    case QuickJsInstructionThrow @throw:

                        builder.Append('\n');
                        builder.Append(new string(' ', padding * 4));

                        if (@throw.NoArg)
                            builder.Append("throw");
                        else
                            builder.Append("throw " + stack.Pop());

                        break;

                    case QuickJsInstructionCall call:

                        string p = "";

                        for (int j = 0; j < call.ExtPopCount; j++)
                        {
                            if (j == 0)
                                p += stack.Pop();
                            else
                                p += ", " + stack.Pop();
                        }

                        if (call.HasResult)
                        {

                            string str = stack.Pop().TrimEnd('\"').TrimStart('\"') + "(" + p + ");";
                            stack.Pop();

                            if (call.Constructor)
                                str = "new " + str;

                            string retVar = "ret_" + call.GetHashCode().ToString();

                            str = "let " + retVar + " = " + str;

                            builder.Append('\n');
                            builder.Append(new string(' ', padding * 4));

                            builder.Append(str);

                            stack.Push(retVar);
                        }
                        else
                        {
                            builder.Append('\n');
                            builder.Append(new string(' ', padding * 4));
                            builder.Append(stack.Pop().TrimEnd('\"').TrimStart('\"') + "(" + p + ");");
                            stack.Pop();
                        }

                        break;

                    case QuickJsInstructionSetVar setVar:

                        if (!setVar.Uninitialized)
                        {
                            string expression = stack.Pop();
                            builder.Append('\n');
                            builder.Append(new string(' ', padding * 4));

                            if (setVar.GlobalVar)
                                builder.Append("Global[\"" + setVar.Value + "\"] = " + expression + ";");
                            else
                                builder.Append(setVar.Value + " = " + expression + ";");

                            if (setVar.PopNewValue)
                                if (setVar.GlobalVar)
                                    stack.Push("Global[\"" + setVar.Value + "\"]");
                                else
                                    stack.Push(setVar.Value);
                        }

                        break;

                    case QuickJsInstructionDup dup:
                        string value = stack.Peek();
                        for (int j = 0; j < dup.DupCount; j++)
                        {
                            stack.Push(value);
                        }

                        break;

                    case QuickJsInstructionReturn ret:

                        builder.Append('\n');
                        builder.Append(new string(' ', padding * 4));

                        if (ret.HasValue)
                            builder.Append("return " + stack.Pop() + ";");
                        else
                            builder.Append("return;");

                        break;

                    case QuickJsInstructionPop pop:
                        stack.Pop();
                        break;

                    case QuickJsInstructionObject obj:
                        stack.Push("{}");
                        break;

                    default:
                        builder.Append('\n');

                        string args = "";

                        for (int j = 0; j < curIns.getOpCode().PopCount; j++)
                        {
                            builder.Append('\n');
                            builder.Append(new string(' ', padding * 4));

                            string val = stack.Pop();

                            builder.Append("// Stack: " + stack.Count().ToString() + ", Value: " + val);

                            if (j != 0)
                                args += ", " + val;
                            else
                                args += val;
                        }

                        for (int j = 0; j < curIns.getOpCode().PushCount; j++)
                        {
                            stack.Push("OP_" + curIns.getOpCode().Name + "(" + args + ")[" + j.ToString() + "]");
                        }

                        builder.Append('\n');
                        builder.Append(new string(' ', padding * 4));
                        builder.Append("// Unsupported Instruction: " + curIns.ToString());
                        builder.Append('\n');

                        break;
                }
            }

            padding--;

            builder.Append('\n');
            builder.Append(new string(' ', padding * 4));
            builder.Append("}\n");

            return builder.ToString();
        }

    }
}
