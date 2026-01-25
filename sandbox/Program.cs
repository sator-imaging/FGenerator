using FGDebugGenerator;
using FGenerator.Sandbox;
using FinalEnums;
using SampleConsumer;
using SampleConsumer.NestedNamespace;
using SampleConsumer.StackArray;
using SampleConsumer.StackList;
using System;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable SMA0024  // Enum to String
#pragma warning disable CA1050   // Declare types in namespaces
#pragma warning disable CS9113   // Parameter is unread.
#pragma warning disable IDE1006  // Naming Styles
#pragma warning disable CA1707   // Identifiers should not contain underscores
#pragma warning disable CA1715   // Identifiers should have correct prefix
#pragma warning disable CA1816   // Call GC.SuppressFinalize correctly
#pragma warning disable SMA0040  // Missing Using Statement
#pragma warning disable SMA0030  // Invalid Struct Constructor
#pragma warning disable CA1861   // Avoid constant arrays as arguments
#pragma warning disable SMA0020  // Unchecked Cast to Enum Type
#pragma warning disable SMA0021  // Cast from Enum Type to Other
#pragma warning disable IDE0390  // Make method synchronous
#pragma warning disable IDE0018  // Inline variable declaration

return FUnit.Run(args, describe =>
{
    describe("FinalEnumGenerator: basic scenarios", it =>
    {
        it("ToStringFast returns display text", () =>
        {
            Must.BeEqual("Seven", EnumContainer.Nest.DeepNestedEnumType.Seven.ToStringFast());
        });

        it("TryParse(string) succeeds/fails as expected", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("Eight", out EnumContainer.Nest.DeepNestedEnumType parsed));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Eight, parsed);
            Must.BeTrue(!FinalEnum.TryParse("Value_Not_Found", out parsed));
            Must.BeTrue(!FinalEnum.TryParse(string.Empty, out parsed));
            Must.BeTrue(!FinalEnum.TryParse("  ", out parsed));
            Must.BeTrue(!FinalEnum.TryParse((string?)null, out parsed));
        });

        it("TryParse honors comparison and whitespace", () =>
        {
            Must.BeTrue(!FinalEnum.TryParse("seven", out EnumContainer.Nest.DeepNestedEnumType parsed, StringComparison.Ordinal));
            Must.BeTrue(FinalEnum.TryParse("  Seven  ", out parsed, StringComparison.OrdinalIgnoreCase));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Seven, parsed);
        });

        it("TryParse(utf8) parses Twelve", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("Twelve"u8, out EnumContainer.Nest.DeepNestedEnumType parsed));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Twelve, parsed);
        });

        it("TryParse(utf8) with ignoring letter casing", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("twelve"u8, out EnumContainer.Nest.DeepNestedEnumType parsed, ignoreCase: true));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Twelve, parsed);

            Must.BeTrue(FinalEnum.TryParse("TWELVE"u8, out parsed, ignoreCase: true));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Twelve, parsed);

            Must.BeTrue(FinalEnum.TryParse("   TWELVE   "u8, out parsed, ignoreWhiteSpace: true, ignoreCase: true));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Twelve, parsed);

            Must.BeTrue(!FinalEnum.TryParse("t"u8, out parsed, ignoreWhiteSpace: true, ignoreCase: true));
        });

        it("TryParse(utf8) can ignore surrounding whitespace when requested", () =>
        {
            var padded = "  Eleven  "u8;
            Must.BeTrue(!FinalEnum.TryParse(padded, out EnumContainer.Nest.DeepNestedEnumType parsed));
            Must.BeTrue(FinalEnum.TryParse(padded, out parsed, ignoreWhiteSpace: true));
            Must.BeEqual(EnumContainer.Nest.DeepNestedEnumType.Eleven, parsed);

            Must.BeTrue(!FinalEnum.TryParse("     "u8, out parsed, ignoreWhiteSpace: true));
        });

        it("ToStringUtf8 returns display text", () =>
        {
            var utf8 = EnumContainer.Nest.DeepNestedEnumType.Seven.ToStringUtf8();
            Must.BeEqual("Seven", Encoding.UTF8.GetString(utf8.Span));
        });

        it("GetNames/Values/Utf8Names preserve declaration order", () =>
        {
            var expectedNames = new[]
            {
                "子,丑,寅,卯,辰,巳,午,未,申,酉,戌,亥",
                "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
            };
            var expectedValues = new[]
            {
                EnumContainer.Nest.DeepNestedEnumType.One,
                EnumContainer.Nest.DeepNestedEnumType.Two,
                EnumContainer.Nest.DeepNestedEnumType.Three,
                EnumContainer.Nest.DeepNestedEnumType.Four,
                EnumContainer.Nest.DeepNestedEnumType.Five,
                EnumContainer.Nest.DeepNestedEnumType.Six,
                EnumContainer.Nest.DeepNestedEnumType.Seven,
                EnumContainer.Nest.DeepNestedEnumType.Eight,
                EnumContainer.Nest.DeepNestedEnumType.Nine,
                EnumContainer.Nest.DeepNestedEnumType.Ten,
                EnumContainer.Nest.DeepNestedEnumType.Eleven,
                EnumContainer.Nest.DeepNestedEnumType.Twelve,
            };

            var names = EnumContainer.Nest.DeepNestedEnumType.One.GetNames();
            var values = EnumContainer.Nest.DeepNestedEnumType.One.GetValues();
            var utf8 = EnumContainer.Nest.DeepNestedEnumType.One.GetNamesUtf8();
            var utf8Text = new string[utf8.Length];
            for (int i = 0; i < utf8.Length; i++)
            {
                utf8Text[i] = Encoding.UTF8.GetString(utf8[i].Span);
            }

            Must.HaveSameSequence(expectedNames, names);
            Must.HaveSameSequence(expectedValues, values);
            Must.HaveSameSequence(expectedNames, utf8Text);
        });

        it("IsDefined matches declared numeric values", () =>
        {
            Must.BeTrue(EnumContainer.Nest.DeepNestedEnumType.One.IsDefined(0));
            Must.BeTrue(EnumContainer.Nest.DeepNestedEnumType.Seven.IsDefined(6));
            Must.BeTrue(EnumContainer.Nest.DeepNestedEnumType.Twelve.IsDefined(11));
            Must.BeTrue(!EnumContainer.Nest.DeepNestedEnumType.One.IsDefined(99));
        });

        it("ToStringFast/Utf8 return empty or throw for unknown non-flags", () =>
        {
            var unknown = (EnumContainer.Nest.DeepNestedEnumType)123;
            Must.BeEqual(string.Empty, unknown.ToStringFast());
            Must.BeEqual(0, unknown.ToStringUtf8().Length);

            try
            {
                _ = unknown.ToStringFast(throwOnUnknown: true);
                Must.BeTrue(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Must.BeEqual("value", ex.ParamName);
            }

            try
            {
                _ = unknown.ToStringUtf8(throwOnUnknown: true);
                Must.BeTrue(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Must.BeEqual("value", ex.ParamName);
            }
        });

        it("ContainsTokenIgnoreCase", () =>
        {
            Must.BeTrue(FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "AlphaXyz"u8));
            Must.BeTrue(FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "alphaXYZ"u8));

            // ContainsTokenIgnoreCase logic tests
            // --> checks start and end char first, and then checks inbetween chars
            //     so need to verify the inbetween chars are taken into account
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "Az"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "aZ"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "A_z"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "a_Z"u8));
            // start and end match, same length
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "A______z"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "a______Z"u8));

            // Inbetween extraction tests
            // --> Algorithm: checks first char and word boundary, then last char and word boundary,
            //                finally compares inbetween portiion of token.
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "AlphaX_z"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "alphaX_Z"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "A_phaXyz"u8));
            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("AlphaXyz,Foo"u8, "a_phaXyZ"u8));

            Must.BeTrue(!FinalEnum.ContainsTokenIgnoreCase("Abc,Def"u8, "Def___"u8));
        });
    });

    describe("FinalEnumGenerator: flags enums", it =>
    {
        it("ToStringFast and TryParse for flags", () =>
        {
            Must.BeEqual("日本語", MyTestFlagsType.Flag1.ToStringFast());
            Must.BeTrue(FinalEnum.TryParse("日本語, Flag4", out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);
        });

        it("TryParse trims and parses combined flags", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("   Flag2,  ひらがな \"français\" カタカナ   ", out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);
        });

        it("TryParse(utf8) parses combined flags", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("日本語, Flag4"u8, out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);
        });

        it("TryParse handles single custom name", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("日本語", out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag1, parsed);
        });

        it("ToStringFast handles combined flags", () =>
        {
            var combined = MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4;
            var text = combined.ToStringFast();
            Must.BeEqual("日本語, Flag4", text);
        });

        it("ToStringUtf8 handles combined flags", () =>
        {
            var combined = MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4;
            var utf8 = combined.ToStringUtf8();
            Must.BeEqual("日本語, Flag4", Encoding.UTF8.GetString(utf8.Span));
        });

        it("ToStringFast returns empty for unknown flags unless throwing", () =>
        {
            var unknown = (MyTestFlagsType)int.MinValue;
            Must.BeEqual(string.Empty, unknown.ToStringFast());

            try
            {
                _ = unknown.ToStringFast(throwOnUnknown: true);
                Must.BeTrue(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Must.BeEqual("value", ex.ParamName);
            }
        });

        it("ToStringUtf8 returns empty for unknown flags unless throwing", () =>
        {
            var unknown = (MyTestFlagsType)int.MinValue;
            var utf8 = unknown.ToStringUtf8();
            Must.BeEqual(0, utf8.Length);

            try
            {
                _ = unknown.ToStringUtf8(throwOnUnknown: true);
                Must.BeTrue(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Must.BeEqual("value", ex.ParamName);
            }
        });

        it("IsDefined only accepts single defined flags", () =>
        {
            Must.BeTrue(MyTestFlagsType.Flag1.IsDefined(1));
            Must.BeTrue(MyTestFlagsType.Flag4.IsDefined(8));
            Must.BeTrue(MyTestFlagsType.Flag1.IsDefined(0));
            Must.BeTrue(MyTestFlagsType.Flag1.IsDefined(3));

            Must.BeTrue(!MyTestFlagsType.Flag1.IsDefined(int.MaxValue));  // OK: unknown
        });

        it("IsDefined uses correct numeric width for unsigned/signed enums", () =>
        {
            Must.BeTrue(UnderlyingValueUInt.Expect_Unsigned_Int64.IsDefined(0UL));
            Must.BeTrue(!UnderlyingValueUInt.Expect_Unsigned_Int64.IsDefined(1UL));
            Must.BeTrue(UnderlyingValueShort.Expect_Int64.IsDefined(0));
            Must.BeTrue(!UnderlyingValueShort.Expect_Int64.IsDefined(1));
        });

        it("TryParse rejects missing delimiters or substrings for flags", () =>
        {
            Must.BeTrue(!FinalEnum.TryParse("Flag1 Flag4", out MyTestFlagsType parsed));
            Must.BeTrue(!FinalEnum.TryParse("Flag14", out parsed));

            Must.BeTrue(!FinalEnum.TryParse("Flag1 Flag4"u8, out parsed));
        });

        it("TryParse accepts comma-only separated flags", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("日本語,Flag4", out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);
        });

        it("TryParse handles casing, trailing commas, and duplicate tokens", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("日本語,flag4", out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);

            Must.BeTrue(FinalEnum.TryParse("日本語,", out parsed));
            Must.BeEqual(MyTestFlagsType.Flag1, parsed);

            Must.BeTrue(FinalEnum.TryParse(",日本語", out parsed));
            Must.BeEqual(MyTestFlagsType.Flag1, parsed);

            Must.BeTrue(FinalEnum.TryParse("日本語,日本語", out parsed));
            Must.BeEqual(MyTestFlagsType.Flag1, parsed);
        });

        it("TryParse accepts comma+space variations for flags", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("日本語, Flag4", out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);

            Must.BeTrue(FinalEnum.TryParse("日本語 ,Flag4", out parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);

            Must.BeTrue(FinalEnum.TryParse("日本語 , Flag4", out parsed));
            Must.BeEqual(MyTestFlagsType.Flag1 | MyTestFlagsType.Flag4, parsed);
        });

        it("TryParse(utf8) accepts comma-only separated flags", () =>
        {
            Must.BeTrue(FinalEnum.TryParse("Flag2,ひらがな \"français\" カタカナ"u8, out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);
        });

        it("TryParse(utf8) handles casing, trailing commas, and duplicate tokens", () =>
        {
            var mixedCase = "flag2,ひらがな \"français\" カタカナ"u8;
            Must.BeTrue(FinalEnum.TryParse(mixedCase, out MyTestFlagsType parsed, ignoreCase: true));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);

            var trailing = "Flag2,"u8;
            Must.BeTrue(FinalEnum.TryParse(trailing, out parsed));
            Must.BeEqual(MyTestFlagsType.Flag2, parsed);

            var leading = ",ひらがな \"français\" カタカナ"u8;
            Must.BeTrue(FinalEnum.TryParse(leading, out parsed));
            Must.BeEqual(MyTestFlagsType.Flag3, parsed);

            var dupes = "Flag2,Flag2"u8;
            Must.BeTrue(FinalEnum.TryParse(dupes, out parsed));
            Must.BeEqual(MyTestFlagsType.Flag2, parsed);
        });

        it("TryParse(utf8) accepts comma+space variations for flags", () =>
        {
            var spacedUtf8 = "Flag2, ひらがな \"français\" カタカナ"u8;
            Must.BeTrue(FinalEnum.TryParse(spacedUtf8, out MyTestFlagsType parsed));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);

            var frontSpacedUtf8 = "Flag2 ,ひらがな \"français\" カタカナ"u8;
            Must.BeTrue(FinalEnum.TryParse(frontSpacedUtf8, out parsed));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);

            var bothSpacedUtf8 = "Flag2 , ひらがな \"français\" カタカナ"u8;
            Must.BeTrue(FinalEnum.TryParse(bothSpacedUtf8, out parsed));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);

            var UPPER_SPACE = "  , FLAG2    ,  ひらがな \"français\" カタカナ  , FLAG2  ,,,  ,"u8;
            Must.BeTrue(FinalEnum.TryParse(UPPER_SPACE, out parsed, ignoreWhiteSpace: true, ignoreCase: true));
            Must.BeEqual(MyTestFlagsType.Flag2 | MyTestFlagsType.Flag3, parsed);
        });

        it("ToStringFastFlags throws on mixed known/unknown when requested", () =>
        {
            var value = (MyTestFlagsType)((int)MyTestFlagsType.Flag1 | int.MinValue);
            Must.BeEqual(string.Empty, value.ToStringFast());

            try
            {
                _ = value.ToStringFast(throwOnUnknown: true);
                Must.BeTrue(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Must.BeEqual("value", ex.ParamName);
            }
        });

        it("ToStringUtf8Flags throws on mixed known/unknown when requested", () =>
        {
            var value = (MyTestFlagsType)((int)MyTestFlagsType.Flag1 | int.MinValue);
            Must.BeEqual(0, value.ToStringUtf8().Length);

            try
            {
                _ = value.ToStringUtf8(throwOnUnknown: true);
                Must.BeTrue(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Must.BeEqual("value", ex.ParamName);
            }
        });
    });

    describe("EnvObfuscator: basic scenarios", it =>
    {
        it("Decodes basic values", () =>
        {
            Must.BeEqual("XX", new string(EnvContainer.EnvObfuscationTest.Value.Span));
            Must.BeEqual("XX", new string(EnvContainer.EnvObfuscationTest.OTHER.Span));
        });

        it("Decodes multi-language and spacing", () =>
        {
            Must.BeEqual("アメンボ赤いな HAHIFUHE FOOOOO", new string(EnvContainer.EnvObfuscationTest.JA.Span));
            Must.BeEqual("アメンボ赤いな HAHIFUHE FOOOOO", new string(EnvContainer.CacheJA));

            Must.BeEqual("START    END   \\r\\n", new string(EnvContainer.EnvObfuscationTest.WHITE_SPACE.Span));
        });

        it("Decodes lines containing '=' and surrogate pairs", () =>
        {
            Must.BeEqual("== value can have '=' (base64 value is allowed)", new string(EnvContainer.EnvObfuscationTest.EQUAL.Span));
            Must.BeEqual("🎉 ← サロゲートペアが必要な絵文字", new string(EnvContainer.EnvObfuscationTest.SurrogatePair.Span));
        });

        it("Empty value returns empty", () =>
        {
            Must.BeEqual(0, EnvContainer.EnvObfuscationTest.EMPTY.Length);
        });

        it("Validate compares full input", () =>
        {
            Must.BeTrue(EnvContainer.EnvObfuscationTest.Validate_Value("XX"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_Value("XX "));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_Value("X"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_Value("XY"));
            Must.BeTrue(EnvContainer.EnvObfuscationTest.Validate_OTHER("XX"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_OTHER("XX "));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_OTHER("X"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_OTHER("XY"));
            Must.BeTrue(EnvContainer.EnvObfuscationTest.Validate_WHITE_SPACE("START    END   \\r\\n"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_WHITE_SPACE("START   END   \\r\\n"));
            Must.BeTrue(EnvContainer.EnvObfuscationTest.Validate_JA("アメンボ赤いな HAHIFUHE FOOOOO"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_JA("アメンボ赤いな HAHIFUHE FOOOO"));
            Must.BeTrue(EnvContainer.EnvObfuscationTest.Validate_SurrogatePair("🎉 ← サロゲートペアが必要な絵文字"));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_SurrogatePair("🎉 ← サロゲートペアが必要な絵文"));
            Must.BeTrue(EnvContainer.EnvObfuscationTest.Validate_EMPTY(""));
            Must.BeTrue(!EnvContainer.EnvObfuscationTest.Validate_EMPTY(" "));
        });
    });

    describe("AutoNotifyGenerator: property change notification", it =>
    {
        it("fires PropertyChanged and updates FullName/Age", () =>
        {
            var person = new Person<int>();
            var raised = new System.Collections.Generic.List<string>();
            person.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            person.FirstName = "John";
            person.LastName = "Doe";
            person.Age = 310;
            person.CelebrateBirthday();

            Must.HaveSameSequence(["FirstName", "LastName", "Age", "CelebrateBirthday"], raised);
            Must.BeEqual("John Doe", person.FullName);
        });
    });

    describe("Stack Generators", it =>
    {
        it("hash codes differ for distinct instances", () =>
        {
            var hashes = new[]
            {
                new StackArray1().GetHashCode(),
                new StackArray3().GetHashCode(),
                new StackArray6().GetHashCode(),
                new StackArray7().GetHashCode(),
                new StackArray14().GetHashCode(),
                new StackArray15().GetHashCode(),
                new StackArray16().GetHashCode(),
                new StackList1<int>() { 1 }.GetHashCode(),
                new StackList3<int>() { 1, 2, 3 }.GetHashCode(),
                new StackListSwapRemove<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.GetHashCode(),
            };

            var distinct = new System.Collections.Generic.HashSet<int>(hashes);
            Must.BeEqual(hashes.Length, distinct.Count);
        });
    });
});





// TEST: partial type declaration within global namespace

[FGDebug]
public partial record Debug<T> : IDisposable
{
    void IDisposable.Dispose() => throw new NotImplementedException();

    private protected async Task __PrivateProtectedGenericAsyncMethod__<TValue>(T t, TValue? u)
        where TValue : class?, IDisposable, new() => throw new NotImplementedException();

    internal string? InternalNullableStringReturningMethod(ref T value) => throw new NotImplementedException();
    protected string? ProtectedNullableTParameterMethod(out T? value) => throw new NotImplementedException();

    T NonNullableT => throw new NotImplementedException();
    T? NullableT => throw new NotImplementedException();

    public T this[int? value] => NonNullableT;
    public T? this[(float? num, string? text) value] => NullableT;

    protected internal int GetSet { get; set; }
    protected internal int GetInit { get; init; }

    public record __NestedRecord__<A>(int? Number, string Text) where A : struct { }
    internal record struct __NestedRecordStruct__<A>(int? Number, string Text) where A : class { }
    protected readonly record struct __NestedReadOnlyRecordStruct__<A>(int? Number, string Text) where A : class? { }
    protected internal readonly struct __NestedReadOnlyStruct__<A>(int? Number, string Text) where A : notnull { }
    protected internal readonly ref struct __NestedReadOnlyRefStruct__<A>(int? Number, string Text) where A : notnull { }
    private protected struct __NestedStruct__<A>(int? Number, string Text) where A : unmanaged { }
    protected internal class __NestedClass__<A>(int? Number, string Text) where A : new() { }
    protected sealed class __NestedSealedClass__<A>(int? Number, string Text) where A : class, new() { }
    internal abstract class __NestedAbstractClass__<A>(int? Number, string Text) where A : class?, new() { }
    private static class __NestedStaticClass__<A> where A : IDisposable?, new() { }
    interface __INestedInterface__<in A, out B> where A : B, IDisposable, new(), allows ref struct { }

    public class NestAlpha
    {
        public class NestBravo
        {
            public class NestCharlie : IDisposable
            {
                public void Dispose() { }
            }
        }
        public class Foo
        {
            public class Bar
            {
                public class Baz
                {
                }
            }
        }
    }
}
