# Documentation Coverage Report

## Overall Check Result
The English README.md provides high coverage for the core features and APIs of FGenerator. It effectively guides users through the Quick Start process, explains how to work with target metadata, and lists available helper methods and CLI options. Most public-facing APIs are well-documented with clear examples.

## Detailed Result Follows

### ✅ Covered Features
- **Core Framework**: `FGeneratorBase` and its abstract/virtual members (`DiagnosticIdPrefix`, `DiagnosticCategory`, `TargetAttributeName`, `PostInitializationOutput`, `CombineCompilationProvider`, `Generate`).
- **Target Metadata**: `Target` properties such as `IsPartial`, `RawAttributes`, `Compilation`, `GenericTypeParameters`, `Members`, `NestedTypes`, and `ContainingTypes`.
- **Helper Methods**: `Utils` extensions like `ToHintName`, `ToAssemblyUniqueIdentifier`, `ToNameString`, `ToDeclarationString`, `ToVisibilityString`, and scaffolding methods (`ToNamespaceAndContainingTypeDeclarations`, etc.).
- **CLI Tooling**: `build` command and flags like `--output`, `--unity`, `--merge`, `--force`, and `--debug`.
- **SDK & Directives**: .NET 10.0 file-based app directives (`#:sdk`, `#:package`, `#:property`).
- **Samples**: Reference to the `sample/` directory.

### ⚠️ Missing or Undocumented Features
- **Target Properties**:
  - `Target.SpecialType`: Not mentioned in the metadata section.
  - `Target.IsGeneric`: While `GenericTypeParameters` is documented, the boolean flag `IsGeneric` is not.
- **CLI Options**:
  - `--configuration` / `-c`: This option is implemented in the CLI but missing from the options list in the README (only `--debug` is mentioned).

### 🔍 Discrepancies
- **Visibility**: `ToContainingTypeDeclarations` and `ToContainingTypeClosingBraces` are documented in the "Helper Methods" section but are currently marked as `internal` in `src/Utils.cs`.
