<div align="center">

# FinalEnumGenerator

Generates High-Performance `string` and `UTF-8` Extensions for Enums

</div>

&nbsp;


- Generates high-performance `ToStringFast()`, `ToStringUtf8()`, and `TryParse()` methods.
- Adds helpers `GetNames()`, `GetValues()`, `GetNamesUtf8()`, and `IsDefined(long/ulong)` on the enum type.
- Supports display names from `InspectorNameAttribute` (Unity compatibility) or `CategoryAttribute`.
- Emits diagnostics for non-public containing types and ambiguous/invalid display names.


&nbsp;





# 🚀 Getting Started

The sample code talks enough.

```cs
using FinalEnumGenerator;

[FinalEnum]
[Flags]
public enum MyEnum
{
    None = 0,

    // Customize name by Category attribute from System.ComponentModel
    // to avoid unnecessary dependency to FinalEnum
    [Category("日本語")]
    Flag1 = 1,
    Flag2 = 2,

    // Non-alphanumerics are supported ofcourse!
    [Category("ひらがな \"français\" カタカナ")]
    Flag3 = 4,
    Flag4 = 8,

    // ERROR: Display name cannot have ',' to avoid ambiguity of Flags enums
    [InspectorName("子,丑,寅,卯,辰,巳,午,未,申,酉,戌,亥")]  // InspectorName can be used (Unity)
    InspectorNameAttribute_from_Unity = 16,
}
```


Here shows how to use the generated extensions.

```cs
using FinalEnums;

if (FinalEnum.TryParse("日本語", out var value))
{
    var text = value.ToStringFast();
    var utf8 = value.ToStringUtf8();
}

// Getting all entries looks a little bit weird
// --> for type safety, '.Flag1' or other entry is required as a receiver
var allNames = MyEnum.Flag1.GetNames();    // GetNames/Values always allocates array
var allValues = MyEnum.Flag1.GetValues();

// Utf8 byte arrays are cached internally but array of ReadOnlyMemory allocates
var allUtf8Arrays = MyEnum.Flag1.GetNamesUtf8();

// Check by underlying primitive
if (MyEnum.Flag1.IsDefined(100))
{
    // accepts `long` or `ulong` depending on the Enum shape
}
```


&nbsp;





# 🕹️ Technical Specs

- `TryParse` checks string/byte length first and uses delimiter-aware token matching (including `[Flags]`).
- `[Flags]` fast-path uses a switch for single flags and falls back to a builder for combined values; combined string/UTF-8 outputs allocate only in that fallback path.
- `TryParse` supports both `string` and `ReadOnlySpan<byte>`.
    - `string` path accepts a `StringComparison` and always trims input.
    - UTF-8 path can optionally ignore whitespace separators.
- The generated members live in `public static class FinalEnums.FinalEnum` and reuse shared helpers (throw helper, boundary-aware `ContainsToken`).
- `[Flags]` parsing:
    - Delimiter-aware token matching succeeds when at least one known token is present; unknown tokens are ignored rather than rejected.
    - Commas are rejected for `[Flags]` display names to keep parsing unambiguous.
- Display name diagnostics include empty/whitespace-only, leading/trailing whitespace, commas, and overlapping tokens; matching is case-insensitive after trimming.

> [!NOTE]
> `TryParse` always allows fuzzy match (can contain undefined name).
> `ToStringFast/Utf8` does not allow converting undefined value.
> `Flags` enums should have value 0 is defined (FinalEnum doesn't validate).
