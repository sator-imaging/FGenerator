using EnvObfuscator;

#pragma warning disable CS8981  // The type name '{0}' only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable IDE1006  // Naming Styles


//// TEST: The actual seed is calculated by `userSeed ^ fnv1aHash(typeFullName)`.
////       Thus the following 2 known FNV-1a 32bit collision pair CAN/MUST produce
////       the same internal seed and causes compile error, as expected.
////       --> Deterministic build works as designed.
///* a=b */
//[Obfuscate(310)] partial class altarage { }
///* a=b */
//[Obfuscate(310)] partial class zinke { }


namespace FGenerator.Sandbox
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


        //// TEST: ERROR
        //[Obfuscate] class NotPartial { }
    }
}
