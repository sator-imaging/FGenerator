#:package FUnit@*
#:package FinalEnumGenerator@2.2.3
#:package EnvObfuscator@2.2.3
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
            Must.BeEqual("Value", Obfuscator.Foo.ToString());
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
