<div align="center">

# MacroDotNet

Compile-Time **Macro for C#** You've Ever Dreamed

[![nuget](https://img.shields.io/nuget/vpre/MacroDotNet)](https://www.nuget.org/packages/MacroDotNet)

*Unity 2022.3.12+ is supported*

</div>


&nbsp;


- Not limited to specific use cases — features a simple, general-purpose text template engine.
- Supports various tokens — `$fieldName`, `$displayName`, `$typeName`, `$inline` and more.
- Multiple `[Macro]` templates per field — processed in declaration order.





&nbsp;

# 🚀 Getting Started

Use `[Macro]` on fields inside a `partial` type.

```cs
using MacroDotNet;

public partial class Example
{
    // Template can be used on multiple fields
    private const string IncrementTemplate =
        "$inline public $static $typeName Increment$displayName() => Interlocked.Increment(ref $fieldName);";

    [Macro(IncrementTemplate)] private static int _globalCounter;
    [Macro(IncrementTemplate)] private int _retry;
    [Macro(IncrementTemplate)] private int _retry2;
    [Macro(IncrementTemplate)] private int _retry3;
}
```


Generated members are emitted into the same containing type hierarchy.

```cs
// Atomic increment with aggressive inlining
Example.IncrementGlobalCounter();

// Reusable [Macro] template generates methods as you imagined
var ex = new Example();
ex.IncrementRetry();
ex.IncrementRetry2();
ex.IncrementRetry3();
```


## Advanced Usage

`[Macro]` can take arguments to inject per-field logic to template.

```cs
// Reusable notify interface (zero-overhead)
private const string NotifyTemplate = @"
    public $static void Set$displayName($typeName value)
    {
        // '$0' will be replaced with args[0]
        $0

        if ($fieldName == value) return;
        $fieldName = value;
        NotifyChanged(""$fieldName"");
    }
    public $static void Set$displayNameWithoutNotify($typeName value)
    {
        $0

        if ($fieldName == value) return;
        $fieldName = value;
    }";

// No args: $0 will be empty string
[Macro(NotifyTemplate)] private static int _foo;  // SetFoo, SetFooWithoutNotify (static)
[Macro(NotifyTemplate)] private int _bar;         // SetBar, SetBarWithoutNotify

// With args: Inject extra validation ($-tokens can be used in args)
[Macro(NotifyTemplate, "if (value <= 0) throw new ArgumentOutOfRangeException(\"$fieldName\");")]
private int _valueMustBePositive;
```

```cs
Example.SetFoo(-1);
ex.SetBarWithoutNotify(-1);
ex.SetValueMustBePositive(-1);  // Error
```





&nbsp;

# Supported Tokens

| Token | Details |
| --- | --- |
| `$fieldName` | Raw field name. |
| `$displayName` | Removes `^[a-zA-Z]*_` prefix, then uppercases first character when needed.<br/>Example: `_value` -> `Value`, `foo_map` -> `Map`. |
| `$typeName` | Field type with full name (only trailing `?` is stripped).<br/>Example: `List<string?>?` -> `global::System.Collections.Generic.List<string?>`. |
| `$typeShortName` | Field type local name (only trailing `?` is stripped).<br/>Example: `List<string?>?` -> `List<string?>`. |
| `$typeBareName` | Field type local bare name (no namespace, no generics, no nullable marker).<br/>Example: `Dictionary<int, string?>?` -> `Dictionary`. |
| `$containerType` | Containing type full name. Uses declared generic parameters (`T`), not constructed types (`<int>`).<br/>Example: `global::MyNamespace.MyType<T>`. |
| `$static` | `static` when the field is static, otherwise empty. |
| `$visibility` | Field accessibility keyword (`public`, `private`, etc.). |
| `$initialValue` | Field initializer text as-is, `(((default!)))` when missing.<br/>Examples: `new()`, `"Foo"`, `null`, `1`. |
| `$typeArgs` | Generic type arguments (empty for non-generic types).<br/>Example: `Dictionary<int, string>` -> `<int, string>`.<br/>Practical use: `IEnumerable$typeArgs` with `List<string>` becomes `IEnumerable<string>`. |
| `$typeConstraints` | Constraint clauses from the field type's generic parameters (`where ...`).<br/>Example: `where T : class, IDisposable, new() where U : unmanaged`. |
| `$0` ... `$9` | Indexed macro args from `[Macro(template, args: ...)]`.<br/>Example: `[Macro("public string X => \"$0-$1\";", "A", "B")]` -> `"A-B"`.<br/>Practical use: reuse one template and inject field-specific expressions/text. |

> Note: Tokens are case-sensitive.


## Sugar Tokens

- `$inline`: Fully-qualified `[MethodImpl(AggressiveInlining)]` attribute.
- `$noinline`: Fully-qualified `[MethodImpl(NoInlining)]` attribute.

## Escaping Token Text

When you want literal token text such as `$fieldName` inside generated string content, use Unicode escape for `$`.

```cs
[Macro(@"public string LiteralFieldToken => ""\u0024fieldName"";")]
private int _value;
```

This generates a member that returns `"$fieldName"` (not the actual field name). Note that `"^[Rr]egular ?[Ee]xpression$"` or other unrecognized $-tokens are leave untouched (i.e. escaping is not required for such case).


## How To Keep Using Statements

Generated output writes built-in default `using` statements first, then appends collected file-level `using` directives from the type declaration file.

If your IDE removes unused `using` directives, keep required symbols referenced in a non-compiled method.

```cs
[System.Diagnostics.Conditional("NOT_COMPILED")]
private static void KeepUsings()
{
    _ = typeof(System.Threading.Interlocked);
}
```


## Performance Tips

`MacroDotNet` generator allocates buffer based on the first-found `[Macro]` template size and reuse it for remaining Macro-annotated fields. To avoid unnecessary buffer expansion, place the most large template at the beginning of the type declaration.

Also, removing leading spaces can optimize memory footprint.

```cs
internal static class MyMacroTemplates
{
    // More readability but less efficiency
    public const string MyTemplate = @"
        // Template with indentation
        ";

    // Best for Unity projects
    public const string MyTemplate =
@"// Template w/o indentation
";

    // Best for latest .NET environment
    public const string MyTemplate = """
        // Raw string literal is most efficient and readable.
        """;
}
```





&nbsp;

# Diagnostics

- `MACRO001` (Error): Invalid target symbol (generator supports named types only).
- `MACRO002` (Error): Containing type must be declared `partial` when using `[Macro]`.
- `MACRO003` (Error): Too many macro args (maximum 10: `$0` to `$9`).
- `MACRO004` (Error): Generated code contains syntax errors (reports Roslyn parser errors with line/column and generated source preview).
- `MACRO_DEBUG` (Info): Debug-only generated code preview (emitted when building in Debug mode and generated code has no syntax errors).





&nbsp;

# 🕹️ Technical Specs

- Target scan is type-wide (`TargetAttributeName == null`), then only fields with `[Macro]` / `[MacroAttribute]` are processed.
- The generator injects this attribute during post-initialization:
    - `namespace MacroDotNet { [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)] internal sealed class MacroAttribute : Attribute { ... } }`
- Multiple `[Macro]` attributes on a single field are applied in order.
- Macro args are passed via `params string[] args`; `$0`..`$9` are replaced when provided.
- Line endings are normalized to `\n` in generated output.





&nbsp;

# TODO

- Positional $-token for generic type arguments and parameters.
    - `$typeArg0`..`9` returns `T`, `int`, `string?` (no angled brackets).
    - `$typeConstraint0`..`9` returns `T : ...` (no `where` keyword).
    - > Find `$typeArg` or `$typeConstraint` then check next char: `s` or `0`..`9`.
- Configuration for DEBUG-only features.
    - Disable syntax highlighting (`[StringSyntax(""C#-test"")]`).
    - Disable syntax validation on generated code.
    - Disable generated code preview (workaround for Visual Studio that always shows "File Not Found" for `.g.cs`).
    - > For the performance, DEBUG detection currently checks `Compilation.Options.OptimizationLevel == OptimizationLevel.Debug` instead of checking `DEBUG` symbol.
- `[LoopMacro]`: Annotate on the constant `int` field to use a value as a loop count.
    - Takes string arguments `beforeLoop`, `loopBody` and `afterLoop`.
    - Introduces new $-token `$loopIndex` that is 0-based loop counter. Any arithmetic operation of constant numbers are going to be compile-time constant so it can be adjusted to 1-based or other without overhead.
- `[GlobalMacro]`: Just an idea.
    - Able to declare class or namespace hierarchy in global namespace...?
