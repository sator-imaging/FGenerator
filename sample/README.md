# 🎯 FGenerator Samples

Single-file source generators built with `FGeneratorBase`, plus a console app that consumes them.

## 📂 What's Inside
- [AutoNotifyGenerator](#🔔-autonotifygenerator) — adds `INotifyPropertyChanged` to partial classes marked with `[AutoNotify]`.
- [StackListGenerator](#📦-stacklistgenerator) — emits a fixed-capacity `IList<T>` implementation for partial structs annotated with `[StackList(length, SwapRemove = ...)]`.
- [StackArrayGenerator](#🧱-stackarraygenerator) — emits inline array storage for partial structs annotated with `[StackArray(length, typeof(T))]`.
- [SampleConsumer](#▶️-sampleconsumer) — console app that references the merged generator DLLs and exercises all three generators.

## 🔔 AutoNotifyGenerator
📝 [AutoNotifyGenerator.cs](AutoNotifyGenerator.cs)
- Generates `PropertyChanged`, `OnPropertyChanged`, and `SetField` helpers for partial classes with `[AutoNotify]`.
- Converts private fields into properties (trims leading underscores, uppercases the first letter) and uses `SetField` to raise change notifications.
- Uses `ToNamespaceAndContainingTypeDeclarations/ClosingBraces`, `Target.Members`, and `ToHintName` to safely emit nested partial types.
- Validates the target is a partial named type and reports diagnostics otherwise; attribute is injected via `PostInitializationOutput`.
- Typical use: mark a partial class, keep private fields, and use the generated properties/`SetField` for change notification.

## 📦 StackListGenerator
📝 [StackListGenerator.cs](StackListGenerator.cs)
- Builds a fixed-capacity list with `MaxCount`, `IList<T>` implementation, `AsSpan()`/`AsFullSpan()`, and optional swap-remove behavior.
- Adds helpers: `AddUnique`, `AddRange`, `AddRangeTruncateOverflow`, `AddRangeDropOldest`, and `AddRangeUnique`.
- Enforces partial non-readonly structs, positive length, unmanaged element constraint, and warns when `IEquatable<T>` is missing (falls back to `EqualityComparer<T>` for `Contains`/`IndexOf`).
- Enumerator instance members are marked `[EditorBrowsable(EditorBrowsableState.Never)]`; `Reset` and `IsReadOnly` stay explicit interface implementations.
- Attribute `[StackList(length, SwapRemove = true/false)]` is injected via `PostInitializationOutput`; hashing samples `_count`, first/middle/last elements.
- Typical use: `public partial struct StackList10<T> where T : unmanaged { }` then call `Add`, index into the list, or iterate with `foreach`.

## 🧱 StackArrayGenerator
📝 [StackArrayGenerator.cs](StackArrayGenerator.cs)
- Mirrors a simple inline array pattern: generates backing fields, a `Span<T>` via `AsSpan()`, and a `ref` indexer for direct element access.
- Guards against invalid usage (non-struct, non-partial, readonly structs, non-positive length, ref-like or managed element types) with diagnostics.
- Enumerator instance members are marked `[EditorBrowsable(EditorBrowsableState.Never)]`; `Reset` remains an explicit interface implementation.
- Provides a `ReadOnlySpan<T>` constructor that copies into the storage and optionally allows length mismatches.
- Attribute `[StackArray(length, typeof(T))]` is injected through `PostInitializationOutput`; hashing combines `Length` plus up to seven evenly spaced elements.
- Typical use: declare `partial struct IntArray10` with `[StackArray(10, typeof(int))]` and access elements via the indexer or `AsSpan()`.

## ▶️ SampleConsumer
📂 [SampleConsumer/](SampleConsumer/)
- Demonstrates `Container.Person<T>` using `[AutoNotify]`, plus the `[StackArray]`/`[StackList]` structs from [ValueCollectionGenerators.cs](SampleConsumer/ValueCollectionGenerators.cs).
- Run the sample console app: `dotnet run --project SampleConsumer/SampleConsumer.csproj`
- The project references the prebuilt analyzers from `../.z__merged_dll__`; adjust the analyzer references if you build the generators differently.
