<div align="center">

# FGenerator

**C# ソースジェネレーターをファイルベースアプリとして構築**

[![nuget](https://img.shields.io/nuget/vpre/FGenerator)](https://www.nuget.org/packages/FGenerator)
[![CLI](https://img.shields.io/nuget/vpre/FGenerator.Cli?label=CLI)](https://www.nuget.org/packages/FGenerator.Cli)
[![SDK](https://img.shields.io/nuget/vpre/FGenerator.Sdk?label=SDK)](https://www.nuget.org/packages/FGenerator.Sdk)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/FGenerator)
<br/>
[![EnvObfuscator](https://img.shields.io/nuget/vpre/EnvObfuscator?label=EnvObfuscator)](https://github.com/sator-imaging/FGenerator/tree/main/EnvObfuscator)
[![FinalEnumGenerator](https://img.shields.io/nuget/vpre/FinalEnumGenerator?label=FinalEnumGenerator)](https://github.com/sator-imaging/FGenerator/tree/main/FinalEnumGenerator)

[<kbd>🇺🇸 English</kbd>](./README.md)
&nbsp;
[<kbd>🇯🇵 日本語版</kbd>](./README.ja.md)
&nbsp;
[<kbd>🇨🇳 简体中文版</kbd>](./README.zh-CN.md)

</div>


&nbsp;


## ✨ 主な機能

- **宣言的**: 何をスキャン・注入するかを記述するだけで Roslyn の内部は不要。必要なものがすべて同梱されています。
- **ロジックに集中**: フレームワークが対象検出・コード生成・診断レポートを代行します。
- **単一ファイル**: ファイルベースのアプリプロジェクト向けに最適化。1 日でフル機能のジェネレーターとアナライザーを開発可能。
- **型ハンドリング**: ネスト/ジェネリックでも安全な命名と partial 宣言をボイラープレート無しで維持。
- **AI エージェントに優しい**: 追加プロンプト無しで有効かつ適切なジェネレーターを生成できます。
- **Unity 対応**: Unity 2022.3.12 以降をサポートします。


&nbsp;





# ⚡ クイックスタート

## 🧩 最小構成のジェネレーターコード

`MyGen` という名前の属性を対象にするジェネレーターの最小例:

> [!IMPORTANT]
> *AI コーディングエージェント向け:* .NET 10.0 でファイルベースアプリ用の新しいディレクティブが導入されています。
> - `#:sdk FGenerator.Sdk@2.5.0` ディレクティブを使用すること。
> - 以前の *dotnet-script*（`.csx`）で使われた `#r` などのディレクティブを使用しないこと。

```csharp
#:sdk FGenerator.Sdk@2.5.0

using FGenerator;
using Microsoft.CodeAnalysis;

// Roslyn の Generator 属性を付与（名前空間で型を指定不要）
[Generator]
public sealed class MyGen : FGeneratorBase  // FGeneratorBase を継承
{
    // 診断設定
    protected override string DiagnosticCategory => nameof(MyGen);
    protected override string DiagnosticIdPrefix => "MYGEN";  // 例: MYGEN001
  
    // MyGen と MyGenAttribute を対象にする (null なら全タイプ対象)
    protected override string? TargetAttributeName => "MyGen";

    // コンパイル時に対象属性を生成（属性は 'internal sealed' とする）
    protected override string? PostInitializationOutput =>
        @"namespace MyGenNamespace { internal sealed class MyGenAttribute : System.Attribute { } }";

    // オプション: 既定は false。
    // target.Compilation を使いたい場合に true に設定（TargetAttributeName が null のときは常に設定される）。
    protected override bool CombineCompilationProvider => true;

    // 解析とコード生成
    protected override CodeGeneration? Generate(Target target, out AnalyzeResult? diagnostic)
    {
        diagnostic = null;

        if (!target.IsPartial)
        {
            // IDE にエラーダイアグノスティックを報告
            diagnostic = new AnalyzeResult(
                "001", "Type must be partial", DiagnosticSeverity.Error,
                $"Type '{type.Name}' must be declared as partial to use MyGen.");

            return null;
        }

        // ソースコード生成
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


**FGenerator の特定バージョンを参照する方法:**

`#:sdk` の代わりに `#:package` と `#:property` ディレクティブを使用します。

> [!IMPORTANT]
> *AI コーディングエージェント向け:* .NET 10.0 でファイルベースアプリ用の新しいディレクティブが導入されています。
> - NuGet パッケージ参照には `#:package` を使用すること。
> - 以下の例にあるすべての `#:property` ディレクティブを含めること（全て必須）。
> - 以前の *dotnet-script*（`.csx`）で使われた `#r` などのディレクティブを使用しないこと。

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



## 🧭 ターゲットメンバー、属性、メタデータの扱い

ターゲットメンバーや属性/コンストラクター引数などのメタデータを扱う基本フローを示します。詳細は [sample/](sample/) フォルダーを参照してください。

```csharp
// semantic model や変換を取得するための Compilation
// （CombineCompilationProvider を true に設定しておく必要あり）
var compilation = target.Compilation;

// 可視メンバーを反復（暗黙のもの・ネストタイプ・ゲッター/セッターメソッドは除外）
foreach (var member in target.Members)
{
    // フィールドのみ反復するには target.Members.OfType<IFieldSymbol>() を使用
    // getter/setter メソッドは IPropertySymbol.Get/SetMethod で取得
}

// 型またはメソッドのジェネリックパラメーター（非ジェネリックなら空）
var typeParams = target.GenericTypeParameters;

// ネストタイプを反復（深さ優先）
var nestedTypes = target.NestedTypes;

// 包含型（外側 → 内側）
var containingTypes = target.ContainingTypes;
```

```csharp
// 最初の RawAttributes エントリが TargetAttributeName に対応
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



## 🛠️ `Target` と `ISymbol` のヘルパーメソッド

> [!IMPORTANT]
> *AI コーディングエージェント向け:*
> - Roslyn 組み込みの `ToDisplayString` などではなく、`FGenerator` のヘルパーを使用してください。

**シンボルの表示/宣言文字列:**
```csharp
// 宣言文字列（修飾子/制約を含めるかを指定可能）
var decl = target.ToDeclarationString(modifiers: true, genericConstraints: true);

// 名前空間/ジェネリック/NULL 許容のオプション付きのフレンドリ名
var fullName = target.ToNameString();                   // global::My.Namespace.MyType.NestedType<T?>
var simpleName = target.ToNameString(localName: true);  // NestedType<T?>
var bareName = target.ToNameString(localName: true, noGeneric: true, noNullable: true);  // NestedType

// アセンブリ内で一意となる識別子を生成（大きな partial クラスなどでフィールド名衝突を防ぐのに便利）
var id = target.ToAssemblyUniqueIdentifier("_");  // My_Namespace_MyType_NestedTypeT1
```

**部分型の足場（ネスト/ジェネリック安全）:**
```csharp
// 正しい partial class/struct/record を生成するためのボイラープレート不要
// 例: namespace My.Namespace {
//         partial class ContainingType {
//           partial record struct OtherContainingType {
var open = target.ToNamespaceAndContainingTypeDeclarations();

// メンバーを出力...
var decl = $"partial {target.ToDeclarationString(modifiers: false)} {{ }}";

// 宣言を閉じる
var close = target.ToNamespaceAndContainingTypeClosingBraces();
```

**包含型のみ（名前空間なし）:**
```csharp
// 他の名前空間で型階層を再現したい場合に便利
var open = target.ToContainingTypeDeclarations();

// コードを出力...

var close = target.ToContainingTypeClosingBraces();
```

**可視性キーワードヘルパー:**
```csharp
// アクセシビリティキーワード（末尾スペース付き、例: "public "）
var visibility = target.ToVisibilityString();
```

**決定的なヒント名（ネスト/ジェネリック安全）:**
```csharp
var hint = target.ToHintName();  // 例: My.Namespace.Type.MyNestedT1.g.cs
```




# 📦 ビルドとパッケージング

CLI を使ってジェネレーター プロジェクト群をビルドします（デフォルトは Release。Debug なら `--debug` を付与）:

```sh
dnx FGenerator.Cli -- build "generators/**/*.cs" --output ./artifacts
```

オプション:
- `--unity`: Unity `.meta` ファイルを DLL と並べて生成（Unity 2022.3.12 以降）。
- `--merge`: ビルド成果物を 1 つの DLL にマージ。
- `--force`: 既存ファイルを上書き。
- `--debug`: Release ではなく `-c Debug` でビルド。
