//// TEST: The actual seed is calculated by `userSeed ^ fnv1aHash(typeFullName)`.
////       Thus the following 2 known FNV-1a 32bit collision pair CAN/MUST produce
////       the same internal seed and causes compile error, as expected.
////       --> Deterministic build works as designed.
///* a=b */
//[EnvObfuscator.Obfuscate(310)] partial class altarage { }
///* a=b */
//[EnvObfuscator.Obfuscate(310)] partial class zinke { }

namespace EnvObfuscator.Test
{
    public partial class EnvContainer
    {
        public static readonly string CacheJA = EnvObfuscationTest.JA.ToString();

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
        public static partial class EnvObfuscationTest
        {
        }



        [Obfuscate] partial class NoEnvComment { }

        // TEST: Syntax (Warning)
        [Obfuscate] partial class UnsupportedTrivia { }

        /* */
        [Obfuscate] partial class NoValidEnvEntries { }

        /* a=b
        NO_EQUAL_CAUSES_WARNING */
        [Obfuscate] partial class InvalidEnvIgnored { }

        /* a=b */
        [Obfuscate(0)] partial class SeedZeroProducesDeterministicBuild { }
        /* a=b */
        [Obfuscate(seed: 0)] partial class SeedZeroProducesDeterministicBuildWithPrefix { }


        //// TEST: ERROR

        //[Obfuscate] class NotPartial { }

        ///* 1foo=bar */
        //[Obfuscate] partial class InvalidKeyName { }

        // TODO: Invalid obfuscation key error cannot be tested (likely never happens...?)
    }
}
