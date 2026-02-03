#:package FUnit@*
#:package FinalEnumGenerator@2.3.1
#:package EnvObfuscator@2.3.1
//                      ~~~~~~~~~~ Push AFTER compiled generators are published

using EnvObfuscator;
using FinalEnumGenerator;

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
