<div align="center">

# EnvObfuscator

Generates Obfuscated Properties from `.env` File Content

</div>

&nbsp;


- Reads the last `/* ... */` multiline comment in the attribute's leading trivia.
- Generates `public static Memory<char>` properties for each valid entry.
- Generates `Validate_<PropertyName>(ReadOnlySpan<char>)` for constant-time comparison.
- Deterministic output via `seed` support.


&nbsp;





# 🚀 Getting Started

To avoid embedding "raw data" as an assembly metadata, `EnvObfuscator` uses preceding block comment as a source.

```cs
using EnvObfuscator;

/*
API_KEY=abc123
SERVICE_URL=https://example.com
SECRET=PA$$WORD
EMPTY=
*/
[Obfuscate(seed: 12345)]
static partial class EnvSecrets
{
}
```

How to use the generated properties:

```cs
// Always returns a freshly decoded clone each time
var apiKey = EnvSecrets.API_KEY;
var cache = apiKey.ToString();

// Consuming decoded data...

// Zeroing out the span — more for peace of mind than actual security
apiKey.Span.Clear();
cache = "";

// Validation (no decoding, full-length compare to avoid timing differences)
if (EnvSecrets.Validate_SECRET("PA$$WORD"))
{
    //...
}
```


## Diagnostics

- Missing multiline comment yields a warning.
- Invalid lines are ignored and reported (first invalid line is shown).
- Invalid keys (non-identifiers, invalid characters, or duplicates) are errors.
- Seed value `0` is allowed but warned (deterministic and predictable).
- Obfuscation keys must be non-zero (error); change the seed to generate different keys.


## Known Limitations

> [!IMPORTANT]
> Obfuscated name collisions can surface as compiler errors, e.g.:
> `error CS0101: The namespace '<random_namespace>' already contains a definition for '<random_class>'` or similar.


&nbsp;





# 🕹️ Technical Specs

- Each non-empty, non-`#` line is parsed as `KEY=VALUE` (split on the first `=`).
    - Keys/values are trimmed; values may contain `=` after the first.
- Keys must already be valid C# identifiers.
    - Invalid characters or keywords cause an error.
    - Duplicate names cause an error.
- `Validate_<PropertyName>(ReadOnlySpan<char>)` short-circuits only on length mismatch, then performs a full-length compare to avoid leaking timing information.
- Clear decoded values with `Span.Clear()` after use to zero sensitive data.
- Obfuscation details:
    - Builds a base character table from all values + a default extra set.
    - Duplicates the table, XOR-encodes with odd/even keys, then shuffles.
    - Random helpers are emitted into random namespaces with random class/field names.
- `seed` (if provided) controls the output deterministically.
    - The type’s assembly-unique identifier is mixed into the seed so each target differs.

> [!NOTE]
> Values are trimmed; leading/trailing spaces are not preserved.
