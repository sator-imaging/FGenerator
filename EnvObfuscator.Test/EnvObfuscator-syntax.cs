//// TEST: The actual seed is calculated by `userSeed ^ fnv1aHash(typeFullName)`.
////       Thus the following 2 known FNV-1a 32bit collision pair CAN/MUST produce
////       the same internal seed and causes compile error, as expected.
////       --> Deterministic build works as designed.
///* a=b */
//[EnvObfuscator.Obfuscate(310)] partial class altarage { }
///* a=b */
//[EnvObfuscator.Obfuscate(310)] partial class zinke { }

namespace EnvObfuscator.Test;

public partial class EnvContainer
{
    public static readonly string CacheJA = EnvObfuscationTestLoader.JA.ToString();

    /* TEST: Another comment block before env & blank lines after env comment */

    /*
    # ◆ comment
    Value=XX
    OTHER=XX

    # traling whitespace is trimmed
    EMPTY=

    JA=アメンボ赤いな HAHIFUHE FOOOOO

    # leading whitespace is ignored
       WHITE_SPACE  =      START    END   \r\n

    EQUAL=== value can have '=' (base64 value is allowed)
    SurrogatePair=🎉 ← サロゲートペアが必要な絵文字
    */

    [Obfuscate()]
    public static class EnvObfuscationTest
    {
    }


    [Obfuscate]
    private class NoEnvComment
    {
    }

    // TEST: Syntax (Warning)
    [Obfuscate]
    private class UnsupportedTrivia
    {
    }

    /* */
    [Obfuscate]
    private class NoValidEnvEntries
    {
    }

    /* a=b
    NO_EQUAL_CAUSES_WARNING */
    [Obfuscate]
    private class InvalidEnvIgnored
    {
    }

    /* a=b */
    [Obfuscate(0)]
    private class SeedZeroProducesDeterministicBuild
    {
    }

    /* a=b */
    [Obfuscate(seed: 0)]
    private class SeedZeroProducesDeterministicBuildWithPrefix
    {
    }


    //// TEST: ERROR

    //[Obfuscate] class NotPartial { }

    ///* 1foo=bar */
    //[Obfuscate] partial class InvalidKeyName { }

    // TODO: Invalid obfuscation key error cannot be tested (likely never happens...?)
}