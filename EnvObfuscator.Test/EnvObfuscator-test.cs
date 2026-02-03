using EnvObfuscator.Test;

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
    describe("EnvObfuscator: basic scenarios", it =>
    {
        it("Decodes basic values", () =>
        {
            Must.BeEqual("XX", new string(EnvObfuscationTestLoader.Value.Span));
            Must.BeEqual("XX", new string(EnvObfuscationTestLoader.OTHER.Span));
        });

        it("Decodes multi-language and spacing", () =>
        {
            Must.BeEqual("アメンボ赤いな HAHIFUHE FOOOOO", new string(EnvObfuscationTestLoader.JA.Span));
            Must.BeEqual("アメンボ赤いな HAHIFUHE FOOOOO", new string(EnvContainer.CacheJA));

            Must.BeEqual("START    END   \\r\\n", new string(EnvObfuscationTestLoader.WHITE_SPACE.Span));
        });

        it("Decodes lines containing '=' and surrogate pairs", () =>
        {
            Must.BeEqual("== value can have '=' (base64 value is allowed)", new string(EnvObfuscationTestLoader.EQUAL.Span));
            Must.BeEqual("🎉 ← サロゲートペアが必要な絵文字", new string(EnvObfuscationTestLoader.SurrogatePair.Span));
        });

        it("Empty value returns empty", () =>
        {
            Must.BeEqual(0, EnvObfuscationTestLoader.EMPTY.Length);
        });

        it("Validate compares full input", () =>
        {
            Must.BeTrue(EnvObfuscationTestLoader.Validate_Value("XX"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_Value("XX "));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_Value("X"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_Value("XY"));
            Must.BeTrue(EnvObfuscationTestLoader.Validate_OTHER("XX"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_OTHER("XX "));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_OTHER("X"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_OTHER("XY"));
            Must.BeTrue(EnvObfuscationTestLoader.Validate_WHITE_SPACE("START    END   \\r\\n"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_WHITE_SPACE("START   END   \\r\\n"));
            Must.BeTrue(EnvObfuscationTestLoader.Validate_JA("アメンボ赤いな HAHIFUHE FOOOOO"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_JA("アメンボ赤いな HAHIFUHE FOOOO"));
            Must.BeTrue(EnvObfuscationTestLoader.Validate_SurrogatePair("🎉 ← サロゲートペアが必要な絵文字"));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_SurrogatePair("🎉 ← サロゲートペアが必要な絵文"));
            Must.BeTrue(EnvObfuscationTestLoader.Validate_EMPTY(""));
            Must.BeTrue(!EnvObfuscationTestLoader.Validate_EMPTY(" "));
        });
    });
});
