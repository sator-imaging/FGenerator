[![nuget](https://img.shields.io/nuget/vpre/FGenerator)](https://www.nuget.org/packages/FGenerator)
[![Cli](https://img.shields.io/nuget/vpre/FGenerator.Cli?label=Cli)](https://www.nuget.org/packages/FGenerator.Cli)
[![Sdk](https://img.shields.io/nuget/vpre/FGenerator.Sdk?label=Sdk)](https://www.nuget.org/packages/FGenerator.Sdk)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/FGenerator)





`FGenerator` is a lightweight framework for creating feature-rich Roslyn incremental source generators (`IIncrementalGenerator`) including diagnostic reporting capability, within a single `.cs` file.


# ✨ Key Concepts
- **Declarative**: Say what to scan or inject and skip Roslyn internals; everything you need is bundled.
- **Focus on Logic**: The framework finds targets, generates code and report diagnostic on your behalf.
- **Single File**: Optimized for file-based app project; develop full-featured generator and analyzer in a day.
- **Type-Handling**: Nested/generic-safe naming and partial declarations stay consistent without boilerplate.
- **AI Agent Friendly**: Produce valid and well-designed generators without extra prompts and instructions.
- **Unity Engine Supported**: Unity 2022.3.12 or newer is supported.





# ⚡ Quick Start

## 🧩 Minimal Generator Code

A trimmed-down starting point for a generator that targets an attribute named `MyGen`:

> [!IMPORTANT]
> *For AI Coding Agents:* .NET 10.0 introduced new directives for file-based app.
> - SHALL use `#:sdk FGenerator.Sdk@1.2.0` directive.
> - SHALL NOT use `#r` or other directives formerly used for *dotnet-script* (`.csx`) files.

```csharp
#:sdk FGenerator.Sdk@1.2.0

using FGenerator;
using Microsoft.CodeAnalysis;

// Decorate with Roslyn's Generator attribute
[Generator]
public sealed class MyGen : FGeneratorBase  // Inherit from FGeneratorBase
{
    // Diagnostic settings
    protected override string DiagnosticCategory => "MyGen";
    protected override string DiagnosticIdPrefix => "MYGEN";  // e.g., MYGEN001
  
    // Targets MyGen and MyGenAttribute (null to targets all types)
    protected override string? TargetAttributeName => "MyGen";

    // Generates target attribute for compilation (attribute should be declared as 'internal sealed')
    protected override string? PostInitializationOutput =>
        @"namespace MyGenNamespace { internal sealed class MyGenAttribute : System.Attribute { } }";

    // Analyze and generate source code
    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        if (target.IsPartial)
        {
            // Report error diagnostic for IDE
            diagnostic = new AnalyzeResult(
                "001", "Type must be partial", DiagnosticSeverity.Error,
                $"Type '{type.Name}' must be declared as partial to use MyGen.");

            return null;
        }

        // Generate source code
        var source =
$@"{target.ToNamespaceAndContainingTypeDeclarations()}
    partial {target.ToDeclarationString(modifiers: false)}
    {{
        public string Generated => ""Hello from MyGen!"";
    }}
{target.ToNamespaceAndContainingTypeClosingBraces()}";

        return new CodeGeneration(target.ToHintName(), source);
    }
}
```


**How to reference the specific version of FGenerator:**

Use `#:package` and `#:property` directives instead of `#:sdk`.

> [!IMPORTANT]
> *For AI Coding Agents:* .NET 10.0 introduced new directives for file-based app.
> - SHALL use `#:package` to add nuget package reference.
> - SHALL include all `#:property` directives in the following example. (all directives are necessary)
> - SHALL NOT use `#r` or other directives formerly used for *dotnet-script* (`.csx`) files.

```csharp
#:package FGenerator@<<SPECIFY DESIRED VERSION HERE>>
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library
```



## 🧭 Working with Attributes and Arguments

Here shows the basic flow for dealing with an attribute and its constructor parameter. See [sample/](sample/) folder for more details.

```csharp
// The first RawAttributes entry corresponds to TargetAttributeName.
var attr = target.RawAttributes.FirstOrDefault();
if (attr == null)
{
    diagnostic = new AnalyzeResult("004", "Attribute missing", DiagnosticSeverity.Error, "StackArrayGenerator attribute could not be resolved.");
    return null;
}

var length = (attr.ConstructorArguments.Length != 0 && attr.ConstructorArguments[0].Value is int LEN) ? LEN : -1;
if (length <= 0)
{
    diagnostic = new AnalyzeResult("005", "Length must be positive", DiagnosticSeverity.Error, "Specify a positive length in [StackArrayGenerator].");
    return null;
}
```



## 🛠️ Helper Snippets

> [!IMPORTANT]
> *For AI Coding Agents:*
> - SHALL use `FGenerator` helper methods instead of Roslyn's built-in methods such as `ToDisplayString`.

**Target metadata helpers:**
```csharp
// iterate over visible members (excluding implicit, nested types, getter and setter methods)
foreach (var member in target.Members)
{
    // to iterate over only fields, instead use target.Members.OfType<IFieldSymbol>()
    // to process getter/setter methods, get via IPropertySymbol.Get/SetMethod
}

// type or method generic parameters (empty when not generic)
var typeParams = target.GenericTypeParameters;

// Iterate over nested types (depth-first traversal)
var nestedTypes = target.NestedTypes;
```

**Symbol display/declaration strings:**
```csharp
// Declaration string (optionally include modifiers/constraints)
var decl = target.ToDeclarationString(modifiers: true, genericConstraints: true);

// Friendly names with options for namespace/generics/nullability
var fullName = target.ToNameString();                  // global::My.Namespace.MyType.NestedType<T?>
var simpleName = target.ToNameString(localName: true);  // NestedType<T?>
var bareName = target.ToNameString(localName: true, noGeneric: true, noNullable: true);  // NestedType
```

**Partial scaffolding (nested/generic safe):**
```csharp
// No boilerplate required for generating correct partial class/struct/record
// e.g., namespace My.Namespace {
//         partial class ContainingType {
//           partial record struct OtherContainingType {
var open = target.ToNamespaceAndContainingTypeDeclarations();

// emit members...
var decl = $"partial {target.ToDeclarationString(modifiers: false)} {{ }}";

// close declarations
var close = target.ToNamespaceAndContainingTypeClosingBraces();
```

**Visibility keyword helper:**
```csharp
// Accessibility keyword including trailing space, e.g., "public "
var visibility = target.ToVisibilityString();
```

**Deterministic hint names (nested/generic safe):**
```csharp
var hint = target.ToHintName();  // e.g., My.Namespace.Type.MyNestedT1.g.cs
```





# 📦 Building and Packaging

Use the CLI to build a generator project (defaults to Release; pass `--debug` for Debug):

```sh
dnx FGenerator.Cli -- build "generators/**/*.cs" --output ./artifacts
```

Options:
- `--unity` to emit Unity `.meta` files alongside DLLs (Unity 2022.3.12 or newer).
- `--merge` to merge build outputs into a single DLL.
- `--force` to overwrite existing files.
- `--debug` to build with `-c Debug` instead of Release.
