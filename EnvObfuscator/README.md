<div align="center">

# EnvObfuscator

Generates Obfuscated `ReadOnlyMemory<char>` Properties from `.env`

</div>

&nbsp;


- Reads the last `/* ... */` multiline comment in the attribute's leading trivia.
- Generates `public static ReadOnlyMemory<char>` properties for each valid entry.
- Generates `Validate_<PropertyName>(ReadOnlySpan<char>)` for constant-time comparison.
- Deterministic output via `seed` support.


&nbsp;





# 🚀 Getting Started

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

Use the generated properties:

```cs
var key = new string(EnvSecrets.API_KEY.Span);
var url = new string(EnvSecrets.SERVICE_URL.Span);
var secret = new string(EnvSecrets.SECRET.Span);
var empty = new string(EnvSecrets.EMPTY.Span); // ""

// Validation (full-length compare to avoid timing differences)
if (EnvSecrets.Validate_SECRET("PA$$WORD"))
{
    // ok
}
```


## Diagnostics

- Missing multiline comment yields a warning.
- Invalid lines are ignored and reported (first invalid line is shown).
- Seed value `0` is allowed but warned (deterministic and predictable).


## Known Limitations

> [!IMPORTANT]
> Obfuscated name collisions can surface as compiler errors, e.g.:
> `error CS0101: The namespace '<random_namespace>' already contains a definition for '<random_class>'` or similar.


&nbsp;





# 🕹️ Technical Specs

- Each non-empty, non-`#` line is parsed as `KEY=VALUE` (split on the first `=`).
    - Keys/values are trimmed; values may contain `=` after the first.
- Keys are normalized to valid identifiers (invalid characters become `_`).
    - Keys that normalize to invalid identifiers (e.g., keywords) are skipped.
    - Duplicate names are deconflicted with `_1`, `_2`, ...
- `Validate_<PropertyName>(ReadOnlySpan<char>)` short-circuits only on length mismatch, then performs a full-length compare to avoid leaking timing information.
- Obfuscation details:
    - Builds a base character table from all values + a default extra set.
    - Duplicates the table, XOR-encodes with odd/even keys, then shuffles.
    - Random helpers are emitted into random namespaces with random class/field names.
- `seed` (if provided) controls the output deterministically.
    - The type’s assembly-unique identifier is mixed into the seed so each target differs.

> [!NOTE]
> Values are trimmed; leading/trailing spaces are not preserved.
