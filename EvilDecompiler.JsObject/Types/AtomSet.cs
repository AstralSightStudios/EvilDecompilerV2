using EvilDecompiler.JsObject.Types.Objects;

namespace EvilDecompiler.JsObject.Types
{

    public class AtomSet
    {

        public List<JsString> Atoms = new List<JsString>();

        public int InternalCount;

        public AtomSet()
        {
            Atoms.Add(new JsString("null"));
            Atoms.Add(new JsString("false"));
            Atoms.Add(new JsString("true"));
            Atoms.Add(new JsString("if"));
            Atoms.Add(new JsString("else"));
            Atoms.Add(new JsString("return"));
            Atoms.Add(new JsString("var"));
            Atoms.Add(new JsString("this"));
            Atoms.Add(new JsString("delete"));
            Atoms.Add(new JsString("void"));
            Atoms.Add(new JsString("typeof"));
            Atoms.Add(new JsString("new"));
            Atoms.Add(new JsString("in"));
            Atoms.Add(new JsString("instanceof"));
            Atoms.Add(new JsString("do"));
            Atoms.Add(new JsString("while"));
            Atoms.Add(new JsString("for"));
            Atoms.Add(new JsString("break"));
            Atoms.Add(new JsString("continue"));
            Atoms.Add(new JsString("switch"));
            Atoms.Add(new JsString("case"));
            Atoms.Add(new JsString("default"));
            Atoms.Add(new JsString("throw"));
            Atoms.Add(new JsString("try"));
            Atoms.Add(new JsString("catch"));
            Atoms.Add(new JsString("finally"));
            Atoms.Add(new JsString("function"));
            Atoms.Add(new JsString("debugger"));
            Atoms.Add(new JsString("with"));
            Atoms.Add(new JsString("__FILE__"));
            Atoms.Add(new JsString("__DIR__"));
            Atoms.Add(new JsString("class"));
            Atoms.Add(new JsString("const"));
            Atoms.Add(new JsString("enum"));
            Atoms.Add(new JsString("export"));
            Atoms.Add(new JsString("extends"));
            Atoms.Add(new JsString("import"));
            Atoms.Add(new JsString("super"));
            Atoms.Add(new JsString("implements"));
            Atoms.Add(new JsString("interface"));
            Atoms.Add(new JsString("let"));
            Atoms.Add(new JsString("package"));
            Atoms.Add(new JsString("private"));
            Atoms.Add(new JsString("protected"));
            Atoms.Add(new JsString("public"));
            Atoms.Add(new JsString("static"));
            Atoms.Add(new JsString("yield"));
            Atoms.Add(new JsString("await"));
            Atoms.Add(new JsString(""));
            Atoms.Add(new JsString("length"));
            Atoms.Add(new JsString("fileName"));
            Atoms.Add(new JsString("lineNumber"));
            Atoms.Add(new JsString("message"));
            Atoms.Add(new JsString("errors"));
            Atoms.Add(new JsString("stack"));
            Atoms.Add(new JsString("name"));
            Atoms.Add(new JsString("toString"));
            Atoms.Add(new JsString("toLocaleString"));
            Atoms.Add(new JsString("valueOf"));
            Atoms.Add(new JsString("eval"));
            Atoms.Add(new JsString("prototype"));
            Atoms.Add(new JsString("constructor"));
            Atoms.Add(new JsString("configurable"));
            Atoms.Add(new JsString("writable"));
            Atoms.Add(new JsString("enumerable"));
            Atoms.Add(new JsString("value"));
            Atoms.Add(new JsString("get"));
            Atoms.Add(new JsString("set"));
            Atoms.Add(new JsString("of"));
            Atoms.Add(new JsString("__proto__"));
            Atoms.Add(new JsString("undefined"));
            Atoms.Add(new JsString("number"));
            Atoms.Add(new JsString("boolean"));
            Atoms.Add(new JsString("string"));
            Atoms.Add(new JsString("object"));
            Atoms.Add(new JsString("symbol"));
            Atoms.Add(new JsString("integer"));
            Atoms.Add(new JsString("unknown"));
            Atoms.Add(new JsString("arguments"));
            Atoms.Add(new JsString("callee"));
            Atoms.Add(new JsString("caller"));
            Atoms.Add(new JsString("<eval>"));
            Atoms.Add(new JsString("<ret>"));
            Atoms.Add(new JsString("<var>"));
            Atoms.Add(new JsString("<arg_var>"));
            Atoms.Add(new JsString("<with>"));
            Atoms.Add(new JsString("lastIndex"));
            Atoms.Add(new JsString("target"));
            Atoms.Add(new JsString("index"));
            Atoms.Add(new JsString("input"));
            Atoms.Add(new JsString("defineProperties"));
            Atoms.Add(new JsString("apply"));
            Atoms.Add(new JsString("join"));
            Atoms.Add(new JsString("concat"));
            Atoms.Add(new JsString("split"));
            Atoms.Add(new JsString("construct"));
            Atoms.Add(new JsString("getPrototypeOf"));
            Atoms.Add(new JsString("setPrototypeOf"));
            Atoms.Add(new JsString("isExtensible"));
            Atoms.Add(new JsString("preventExtensions"));
            Atoms.Add(new JsString("has"));
            Atoms.Add(new JsString("deleteProperty"));
            Atoms.Add(new JsString("defineProperty"));
            Atoms.Add(new JsString("getOwnPropertyDescriptor"));
            Atoms.Add(new JsString("ownKeys"));
            Atoms.Add(new JsString("add"));
            Atoms.Add(new JsString("done"));
            Atoms.Add(new JsString("next"));
            Atoms.Add(new JsString("values"));
            Atoms.Add(new JsString("source"));
            Atoms.Add(new JsString("flags"));
            Atoms.Add(new JsString("global"));
            Atoms.Add(new JsString("unicode"));
            Atoms.Add(new JsString("raw"));
            Atoms.Add(new JsString("new.target"));
            Atoms.Add(new JsString("this.active_func"));
            Atoms.Add(new JsString("<home_object>"));
            Atoms.Add(new JsString("<computed_field>"));
            Atoms.Add(new JsString("<static_computed_field>"));
            Atoms.Add(new JsString("<class_fields_init>"));
            Atoms.Add(new JsString("<brand>"));
            Atoms.Add(new JsString("#constructor"));
            Atoms.Add(new JsString("as"));
            Atoms.Add(new JsString("from"));
            Atoms.Add(new JsString("meta"));
            Atoms.Add(new JsString("*default*"));
            Atoms.Add(new JsString("*"));
            Atoms.Add(new JsString("Module"));
            Atoms.Add(new JsString("then"));
            Atoms.Add(new JsString("resolve"));
            Atoms.Add(new JsString("reject"));
            Atoms.Add(new JsString("promise"));
            Atoms.Add(new JsString("proxy"));
            Atoms.Add(new JsString("revoke"));
            Atoms.Add(new JsString("async"));
            Atoms.Add(new JsString("exec"));
            Atoms.Add(new JsString("groups"));
            Atoms.Add(new JsString("status"));
            Atoms.Add(new JsString("reason"));
            Atoms.Add(new JsString("globalThis"));
            Atoms.Add(new JsString("not-equal"));
            Atoms.Add(new JsString("timed-out"));
            Atoms.Add(new JsString("ok"));
            Atoms.Add(new JsString("toJSON"));
            Atoms.Add(new JsString("Object"));
            Atoms.Add(new JsString("Array"));
            Atoms.Add(new JsString("Error"));
            Atoms.Add(new JsString("Number"));
            Atoms.Add(new JsString("String"));
            Atoms.Add(new JsString("Boolean"));
            Atoms.Add(new JsString("Symbol"));
            Atoms.Add(new JsString("Arguments"));
            Atoms.Add(new JsString("Math"));
            Atoms.Add(new JsString("JSON"));
            Atoms.Add(new JsString("Date"));
            Atoms.Add(new JsString("Function"));
            Atoms.Add(new JsString("GeneratorFunction"));
            Atoms.Add(new JsString("ForInIterator"));
            Atoms.Add(new JsString("RegExp"));
            Atoms.Add(new JsString("ArrayBuffer"));
            Atoms.Add(new JsString("SharedArrayBuffer"));
            Atoms.Add(new JsString("Uint8ClampedArray"));
            Atoms.Add(new JsString("Int8Array"));
            Atoms.Add(new JsString("Uint8Array"));
            Atoms.Add(new JsString("Int16Array"));
            Atoms.Add(new JsString("Uint16Array"));
            Atoms.Add(new JsString("Int32Array"));
            Atoms.Add(new JsString("Uint32Array"));
            Atoms.Add(new JsString("Float32Array"));
            Atoms.Add(new JsString("Float64Array"));
            Atoms.Add(new JsString("DataView"));
            Atoms.Add(new JsString("Map"));
            Atoms.Add(new JsString("Set"));
            Atoms.Add(new JsString("WeakMap"));
            Atoms.Add(new JsString("WeakSet"));
            Atoms.Add(new JsString("Map Iterator"));
            Atoms.Add(new JsString("Set Iterator"));
            Atoms.Add(new JsString("Array Iterator"));
            Atoms.Add(new JsString("String Iterator"));
            Atoms.Add(new JsString("RegExp String Iterator"));
            Atoms.Add(new JsString("Generator"));
            Atoms.Add(new JsString("Proxy"));
            Atoms.Add(new JsString("Promise"));
            Atoms.Add(new JsString("PromiseResolveFunction"));
            Atoms.Add(new JsString("PromiseRejectFunction"));
            Atoms.Add(new JsString("AsyncFunction"));
            Atoms.Add(new JsString("AsyncFunctionResolve"));
            Atoms.Add(new JsString("AsyncFunctionReject"));
            Atoms.Add(new JsString("AsyncGeneratorFunction"));
            Atoms.Add(new JsString("AsyncGenerator"));
            Atoms.Add(new JsString("EvalError"));
            Atoms.Add(new JsString("RangeError"));
            Atoms.Add(new JsString("ReferenceError"));
            Atoms.Add(new JsString("SyntaxError"));
            Atoms.Add(new JsString("TypeError"));
            Atoms.Add(new JsString("URIError"));
            Atoms.Add(new JsString("InternalError"));
            Atoms.Add(new JsString("<brand>"));
            Atoms.Add(new JsString("Symbol.toPrimitive"));
            Atoms.Add(new JsString("Symbol.iterator"));
            Atoms.Add(new JsString("Symbol.match"));
            Atoms.Add(new JsString("Symbol.matchAll"));
            Atoms.Add(new JsString("Symbol.replace"));
            Atoms.Add(new JsString("Symbol.search"));
            Atoms.Add(new JsString("Symbol.split"));
            Atoms.Add(new JsString("Symbol.toStringTag"));
            Atoms.Add(new JsString("Symbol.isConcatSpreadable"));
            Atoms.Add(new JsString("Symbol.hasInstance"));
            Atoms.Add(new JsString("Symbol.species"));
            Atoms.Add(new JsString("Symbol.unscopables"));
            Atoms.Add(new JsString("Symbol.asyncIterator"));

            InternalCount = Atoms.Count;
        }

        public JsString? Get(int index)
        {
            // JS_ATOM_NULL == 0
            // 0代表JS_ATOM_NULL所以得从1开始算得-1

            if (index == 0)
                return null;

            try
            {
                return Atoms[index - 1];
            }
            catch
            {
                return new JsString("UnknownAtomString" + index.ToString());
            }
        }

        public JsString? Get(AtomIdx index)
        {
            return Get(index.Value);
        }

        public JsString? Get(BcIdx index)
        {
            return Get(index.ToAtomIdx());
        }

        public int Add(JsString value)
        {
            // JS_ATOM_NULL == 0
            // 由于是Count所以不用+1

            Atoms.Add(value);
            return Atoms.Count;
        }

        public bool IsInternal(int index)
        {
            return InternalCount >= index;
        }
    }
}
