#:package FUnit@*
#:package FinalEnumGenerator@3.0.0
#:package EnvObfuscator@3.0.0
#:package MacroDotNet@3.0.0
//                    ~~~~~~~~~~~~ Push AFTER compiled generators are published

using EnvObfuscator;
using FinalEnumGenerator;
using MacroDotNet;

#pragma warning disable SMA0026 // Enum Obfuscation

return FUnit.Run(args, describe =>
{
    describe("Basic Tests", it =>
    {
        it("EnvObfuscator", () =>
        {
            Must.BeEqual("Value", ObfuscatorLoader.Foo.ToString());

            var foo = (stackalloc char[] { 'V', 'a', 'l', 'u', 'e' });
            Must.BeTrue(ObfuscatorLoader.Validate_Foo(foo));
            foo.Clear();
        });

        it("FinalEnums", () =>
        {
            Must.BeTrue(FinalEnums.Test.TryParse("Foo", out var result));
            Must.BeEqual(Test.Foo, result);
        });

        it("MacroDotNet", () =>
        {
            var ex = new MacroExample();
            Must.BeEqual(1, ex.IncrementCounter());
        });
    });
});





/*
Foo=Value
*/
[Obfuscate]
partial class Obfuscator { }



[FinalEnum]
enum Test
{
    Default,
    Foo,
    Bar,
}


public partial class MacroExample
{
    [Macro("public int Increment$displayName() => ++$fieldName;")]
    private int _counter;
}
