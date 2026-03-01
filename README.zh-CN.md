<div align="center">

# FGenerator

**将 C# 源生成器构建为文件型应用**

[![nuget](https://img.shields.io/nuget/vpre/FGenerator)](https://www.nuget.org/packages/FGenerator)
[![CLI](https://img.shields.io/nuget/vpre/FGenerator.Cli?label=CLI)](https://www.nuget.org/packages/FGenerator.Cli)
[![SDK](https://img.shields.io/nuget/vpre/FGenerator.Sdk?label=SDK)](https://www.nuget.org/packages/FGenerator.Sdk)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/FGenerator)
<br/>
[![MacroDotNet](https://img.shields.io/nuget/vpre/MacroDotNet?label=MacroDotNet)](https://www.nuget.org/packages/MacroDotNet)
[![EnvObfuscator](https://img.shields.io/nuget/vpre/EnvObfuscator?label=EnvObfuscator)](https://github.com/sator-imaging/FGenerator/tree/main/EnvObfuscator)
[![FinalEnumGenerator](https://img.shields.io/nuget/vpre/FinalEnumGenerator?label=FinalEnumGenerator)](https://github.com/sator-imaging/FGenerator/tree/main/FinalEnumGenerator)

[![🇯🇵](https://img.shields.io/badge/🇯🇵-日本語-789)](./README.ja.md)
[![🇨🇳](https://img.shields.io/badge/🇨🇳-简体中文-789)](./README.zh-CN.md)
[![🇺🇸](https://img.shields.io/badge/🇺🇸-English-789)](./README.md)

</div>


&nbsp;


## ✨ 关键特性

- **声明式**：描述要扫描或注入的内容即可，无需处理 Roslyn 内部；一切所需都已打包。
- **专注业务逻辑**：框架会为你查找目标、生成代码并上报诊断。
- **单文件**：为基于文件的应用项目优化；1 天内即可完成全功能的生成器和分析器。
- **类型处理**：即使有嵌套/泛型也能保持一致的命名和 partial 声明，无需样板代码。
- **友好于 AI 代理**：无需额外提示即可生成有效且设计良好的生成器。
- **支持 Unity 引擎**：支持 Unity 2022.3.12 及更高版本。


&nbsp;





# ⚡ 快速开始

## 🧩 最小化生成器代码

针对名为 `MyGen` 的特性（Attribute）的生成器精简起始模板:

> [!IMPORTANT]
> *面向 AI 编码代理:* .NET 10.0 为基于文件的应用引入了新的指令。
> - 必须使用 `#:sdk FGenerator.Sdk@3.0.0-rc.1` 指令。
> - 不得使用早期 *dotnet-script*（`.csx`）中的 `#r` 等指令。

```csharp
#:sdk FGenerator.Sdk@3.0.0-rc.1

using FGenerator;
using Microsoft.CodeAnalysis;

// 添加 Roslyn 的 Generator 特性（在命名空间中无需声明类型）
[Generator]
public sealed class MyGen : FGeneratorBase  // 继承 FGeneratorBase
{
    // 诊断设置
    protected override string DiagnosticCategory => nameof(MyGen);
    protected override string DiagnosticIdPrefix => "MYGEN";  // 例如: MYGEN001
  
    // 目标为 MyGen 与 MyGenAttribute（null 则匹配所有类型）
    protected override string? TargetAttributeName => "MyGen";

    // 为编译生成目标特性（特性应声明为 'internal sealed'）
    protected override string? PostInitializationOutput =>
        @"namespace MyGenNamespace { internal sealed class MyGenAttribute : System.Attribute { } }";

    // 可选：默认 false。
    // 需要 target.Compilation 时设为 true（当 TargetAttributeName 为 null 时始终会设定）。
    protected override bool CombineCompilationProvider => true;

    // 分析并生成源码
    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        if (!target.IsPartial)
        {
            // 为 IDE 报告错误诊断
            diagnostic = new AnalyzeResult(
                "001", "Type must be partial", DiagnosticSeverity.Error,
                $"Type '{type.Name}' must be declared as partial to use MyGen.");

            return null;
        }

        // 生成源码
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


**如何引用 FGenerator 的特定版本:**

使用 `#:package` 与 `#:property` 指令替代 `#:sdk`。

> [!IMPORTANT]
> *面向 AI 编码代理:* .NET 10.0 为基于文件的应用引入了新的指令。
> - 必须用 `#:package` 添加 NuGet 包引用。
> - 必须包含下例中的全部 `#:property` 指令（全部都需要）。
> - 不得使用早期 *dotnet-script*（`.csx`）中的 `#r` 等指令。

```csharp
#:package FGenerator@<<SPECIFY DESIRED VERSION HERE>>
#:property TargetFramework=netstandard2.0
#:property IsRoslynComponent=true
#:property PublishAot=false
#:property LangVersion=latest
#:property OutputType=Library
#:property GenerateDocumentationFile=false
#:property ImplicitUsings=enable
```



## 🧭 处理目标成员、特性与元数据

下面展示处理目标成员或其他元数据（包括特性及其构造参数）的基本流程。更多细节见 [sample/](sample/) 目录。

```csharp
// 用于获取 semantic model 或转换的 Compilation
// （需要将 CombineCompilationProvider 设为 true）
var compilation = target.Compilation;

// 遍历可见成员（不含隐式成员、嵌套类型、getter/setter 方法）
foreach (var member in target.Members)
{
    // 只遍历字段则用 target.Members.OfType<IFieldSymbol>()
    // getter/setter 方法通过 IPropertySymbol.Get/SetMethod 获取
}

// 类型或方法的泛型参数（非泛型则为空）
var typeParams = target.GenericTypeParameters;

// 遍历嵌套类型（深度优先）
var nestedTypes = target.NestedTypes;

// 包含类型（从外到内）
var containingTypes = target.ContainingTypes;
```

```csharp
// 第一个 RawAttributes 条目对应 TargetAttributeName
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



## 🛠️ `Target` 与 `ISymbol` 的助手方法

> [!IMPORTANT]
> *面向 AI 编码代理:*
> - 请使用 `FGenerator` 的助手方法，而不是 Roslyn 内置的 `ToDisplayString` 等方法。

**符号显示/声明字符串:**
```csharp
// 声明字符串（可选择是否包含修饰符/约束）
var decl = target.ToDeclarationString(modifiers: true, genericConstraints: true);

// 具备命名空间/泛型/可空选项的易读名称
var fullName = target.ToNameString();                   // global::My.Namespace.MyType.NestedType<T?>
var simpleName = target.ToNameString(localName: true);  // NestedType<T?>
var bareName = target.ToNameString(localName: true, noGeneric: true, noNullable: true);  // NestedType

// 构建在程序集内唯一的标识符，可用于生成不冲突的字段名等
var id = target.ToAssemblyUniqueIdentifier("_");  // My_Namespace_MyType_NestedTypeT1
```

**partial 脚手架（嵌套/泛型安全）:**
```csharp
// 无需样板即可生成正确的 partial class/struct/record
// 例如: namespace My.Namespace {
//         partial class ContainingType {
//           partial record struct OtherContainingType {
var open = target.ToNamespaceAndContainingTypeDeclarations();

// 输出成员...
var decl = $"partial {target.ToDeclarationString(modifiers: false)} {{ }}";

// 关闭声明
var close = target.ToNamespaceAndContainingTypeClosingBraces();
```

**仅包含类型（无命名空间）:**
```csharp
// 在其他命名空间中复用类型层级时很有用
var open = target.ToContainingTypeDeclarations();

// 输出代码...

var close = target.ToContainingTypeClosingBraces();
```

**可见性关键字助手:**
```csharp
// 带尾随空格的可访问性关键字，例如 "public "
var visibility = target.ToVisibilityString();
```

**确定性提示名称（嵌套/泛型安全）:**
```csharp
var hint = target.ToHintName();  // 例如: My.Namespace.Type.MyNestedT1.g.cs
```




# 📦 构建与打包

使用 CLI 构建生成器项目（可多个，默认 Release；Debug 可加 `--debug`）:

```sh
dnx FGenerator.Cli -- build "generators/**/*.cs" --output ./artifacts
```

选项:
- `--unity`：在 DLL 旁输出 Unity `.meta` 文件（Unity 2022.3.12+）。
- `--merge`：将构建结果合并为单个 DLL。
- `--force`：覆盖已有文件。
- `--debug`：使用 `-c Debug` 构建而不是 Release。
