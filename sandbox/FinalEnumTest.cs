using FinalEnumGenerator;
using System;
using System.ComponentModel;

#pragma warning disable SMA0026  // Enum Obfuscation
#pragma warning disable CA1707   // Identifiers should not contain underscores
#pragma warning disable SMA0027 // Unusual Enum Definition

namespace SampleConsumer
{
    // Unity compatibility
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    sealed class InspectorNameAttribute(string ___) : Attribute { public readonly string not_used = ___; }

    [FinalEnum]
    [Flags]
    public enum MyTestFlagsType
    {
        // ERROR: Display name cannot have ','
        //[InspectorName("子,丑,寅,卯,辰,巳,午,未,申,酉,戌,亥")]
        None = 0,

        [Category("日本語")]  // TEST: 3 chars in IDE but more bytes in UTF-8
        Flag1 = 1,
        Flag2 = 2,

        [Category("ひらがな \"français\" カタカナ")]
        Flag3 = 4,
        Flag4 = 8,

        [Category("Display Name")]
        Flag5 = 16,

        //[Category("Display")]    // ERROR
        [Category("DisplayName")]  // OK: boundary aware
        Flag6 = 32,

        // ERROR: token conflicts with 'Display Name'
        //[Category("Name")]
        Flag7 = 64,
    }

    [FinalEnum]
    [Flags]
    public enum UnderlyingValueUInt : uint  // TEST: expect generated method uses `ulong` as underlying value
    {
        Expect_Unsigned_Int64,
    }

    [FinalEnum]
    [Flags]
    public enum UnderlyingValueShort : short  // TEST: expect generated method uses `long` as underlying value
    {
        Expect_Int64,
    }


    public class EnumContainer
    {
        public record struct Nest
        {
            [FinalEnum]
            public enum DeepNestedEnumType  // TEST: > 10 items (switch uncommented?)
            {
                // OK: Non-Flags enum can have ','
                [InspectorName("子,丑,寅,卯,辰,巳,午,未,申,酉,戌,亥")]
                One,
                Two,
                Three,
                Four,
                Five,
                Six,
                Seven,
                Eight,
                Nine,
                Ten,
                Eleven,
                Twelve,
            }
        }
    }


    [FinalEnum]
    public enum ExtremeNonFlagsUnsigned : ulong
    {
        MinValue = ulong.MinValue,
        SafeValue = ulong.MaxValue >> 1,

        // ERROR: MinValue | MaxValue will set all flag (== ~0 results ambiguous)
        //MaxValue = ulong.MaxValue,
    }

    [FinalEnum]
    public enum ExtremeNonFlagsSigned : long
    {
        MinValue = long.MinValue,
        SafeValue = long.MaxValue >> 1,

        // ERROR: MinValue | MaxValue will set all flag (== ~0 results ambiguous)
        //MaxValue = long.MaxValue,
    }


    [FinalEnum]
    [Flags]
    public enum ExtremeFlagsUnsigned : ulong
    {
        MinValue = ulong.MinValue,
        SafeValue = ulong.MaxValue >> 1,

        // ERROR: MinValue | MaxValue will set all flag (== ~0 results ambiguous)
        //MaxValue = ulong.MaxValue,
    }

    [FinalEnum]
    [Flags]
    public enum ExtremeFlagsSigned : long
    {
        MinValue = long.MinValue,
        SafeValue = long.MaxValue >> 1,

        // ERROR: MinValue | MaxValue will set all flag (== ~0 results ambiguous)
        //MaxValue = long.MaxValue,
    }
}
