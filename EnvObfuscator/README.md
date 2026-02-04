<div align="center">

# EnvObfuscator

Generates Obfuscated Properties from `.env` File Content

[![nuget](https://img.shields.io/nuget/vpre/EnvObfuscator)](https://www.nuget.org/packages/EnvObfuscator)
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/FGenerator/6.1-envobfuscator)

</div>

&nbsp;


- `public static Memory<char>` properties for each `.env` entry.
- `Validate_<PropertyName>(ReadOnlySpan<char>)` for constant-time comparison.
- Ability to generate GUID-named chaff classes.


&nbsp;





# 🚀 Getting Started

To avoid embedding "raw data" as an assembly metadata, `EnvObfuscator` uses preceding block comment as a source.

```cs
using EnvObfuscator;

/*
# 👇 Copy & paste .env file content
API_KEY=abc123
SERVICE_URL=https://example.com
SECRET=PA$$WORD
EMPTY=
*/
[Obfuscate(seed: 12345)]  // Omit the argument to use random seed
static partial class EnvSecrets
{
}
```


> [!IMPORTANT]
> The properties and methods are contained within a dedicated class called `<TargetClass>Loader` that is designed to remove the unnecessary `Obfuscate` attribute marker from the actual obfuscation class. Note that the original target class with marker attribute will have GUID-named decoys which always throw.

```cs
// Always returns a freshly decoded clone each time
var apiKey = EnvSecretsLoader.API_KEY;
var cache = apiKey.ToString();

// Consuming decoded data...

// Zeroing out the span — more for peace of mind than actual security
apiKey.Span.Clear();
cache = "";

// Validation (no decoding, full-length compare to avoid timing differences)
if (EnvSecretsLoader.Validate_SECRET("PA$$WORD"))
{
    //...
}
```


> [!TIP]
> As `string` type is immutable (cannot zero them explicitly) and GC-collected object (not erased on demand), instead using `stackalloc` can achieve validation under full control.

```cs
var password = (stackalloc char[] { ... });

if (EnvSecretsLoader.Validate_SECRET(password))
{
    //...
}

password.Clear();  // Fills memory by zero
```



## Emitting Chaff

Intended to eliminate unnecessary "marker" for obfuscation, generated classes don't have `DynamicallyAccessedMembers` or `UnityEngine.Scripting.Preserve` attribute.

If you need to include the generated GUID-named chaff classes in build, use `link.xml` to prevent trimming on Native AOT or Unity IL2CPP build.

```cs
// Declare necessary chaff classes as desired

// Tip: The working dummy should provide the same functionality,
//      but also logs user information for later use in banning.

/* API_KEY=working-dummy-to-detect-reverse-engineering */
[Obfuscate] partial class DbSecrets { }

/* API_KEY=working-dummy-to-detect-reverse-engineering */
[Obfuscate] partial class DatabaseSecrets { }

/* API_KEY=key-for-valid-usage */
[Obfuscate] partial class MySecrets { }
```


Here shows sample `link.xml` for Unity. See the following link for more details.

- Unity: https://docs.unity3d.com/Manual/managed-code-stripping-xml-formatting.html
- C# / .NET: https://github.com/dotnet/runtime/blob/main/docs/tools/illink/data-formats.md


```xml
<linker>
  <!-- Preserve an entire assembly -->
  <assembly fullname="MyAssembly">
    <!-- Preserve a specific type -->
    <type fullname="MyNamespace.MyClass" preserve="all"/>
    <!-- Preserve only methods -->
    <type fullname="MyNamespace.MyOtherClass" preserve="methods"/>
  </assembly>

  <!-- Preserve Unity built-in assemblies -->
  <assembly fullname="UnityEngine.CoreModule">
    <type fullname="UnityEngine.GameObject" preserve="all"/>
  </assembly>
</linker>
```


&nbsp;





# Diagnostics

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
