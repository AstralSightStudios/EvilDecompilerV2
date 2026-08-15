# EvilDecompilerV2

高保真quickjs反编译器，通过SSA IR 与 AST，将字节码转化为JavaScript代码。

v1: https://github.com/66hh/EvilDecompiler

#### 示例

```
原始代码:
// 02: 运算符（算术/位/逻辑/比较/三元/typeof）
let x = 10, y = 3;
print(x + y, x - y, x * y, x / y, x % y, x ** y);
print(x & y, x | y, x ^ y, ~x, x << 2, x >> 1, x >>> 1);
print(x > y, x < y, x >= y, x <= y, x == y, x != y, x === y, x !== y);
print(true && false, true || false, !true);
let z = x > y ? "big" : "small";
print(typeof x, typeof "s", typeof undefined, typeof null);
print(1 + "2", "3" * 2, x ?? y, null ?? "default");
x++; x--; ++x; --x;
x += 5; x -= 2; x *= 2; x /= 3; x %= 4; x **= 2;
print(x, z);

反编译:
function eval() {
    var x, y, z;
    x = 10;
    y = 3;
    print(x + y, x - y, x * y, x / y, x % y, x ** y);
    print(x & y, x | y, x ^ y, ~x, x << 2, x >> 1, x >>> 1);
    print(x > y, x < y, x >= y, x <= y, x == y, x != y, x === y, x !== y);
    print(true && false, true || false, !true);
    z = x > y ? "big" : "small";
    print(typeof x, typeof "s", typeof undefined, typeof null);
    print(1 + "2", "3" * 2, x ?? y, null ?? "default");
    x++;
    x--;
    x = x + 1;
    x = x - 1;
    x = x + 5;
    x = x - 2;
    x = x * 2;
    x = x / 3;
    x = x % 4;
    x = x ** 2;
    print(x, z);
}

```

# 警告
此反编译器仍处于实验性版本，请勿用于生产环境。

# 许可证
本软件遵循AGPL3.0发布。
