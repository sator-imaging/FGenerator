using FGeneratorDebugger;
using FGenerator.Sandbox;
using FGenerator.Sandbox.StackArray;
using FGenerator.Sandbox.StackList;
using System;
using System.Threading.Tasks;

#pragma warning disable SMA0024  // Enum to String
#pragma warning disable CA1050   // Declare types in namespaces
#pragma warning disable CS9113   // Parameter is unread.
#pragma warning disable IDE1006  // Naming Styles
#pragma warning disable CA1707   // Identifiers should not contain underscores
#pragma warning disable CA1715   // Identifiers should have correct prefix
#pragma warning disable CA1816   // Call GC.SuppressFinalize correctly
#pragma warning disable SMA0040  // Missing Using Statement
#pragma warning disable SMA0030  // Invalid Struct Constructor
#pragma warning disable CA1861   // Avoid constant arrays as arguments
#pragma warning disable SMA0020  // Unchecked Cast to Enum Type
#pragma warning disable SMA0021  // Cast from Enum Type to Other
#pragma warning disable IDE0390  // Make method synchronous
#pragma warning disable IDE0018  // Inline variable declaration

return FUnit.Run(args, describe =>
{
    describe("AutoNotifyGenerator: property change notification", it =>
    {
        it("fires PropertyChanged and updates FullName/Age", () =>
        {
            var person = new Person<int>();
            var raised = new System.Collections.Generic.List<string>();
            person.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            person.FirstName = "John";
            person.LastName = "Doe";
            person.Age = 310;
            person.CelebrateBirthday();

            Must.HaveSameSequence(["FirstName", "LastName", "Age", "CelebrateBirthday"], raised);
            Must.BeEqual("John Doe", person.FullName);
        });
    });

    describe("Stack Generators", it =>
    {
        it("hash codes differ for distinct instances", () =>
        {
            var hashes = new[]
            {
                new StackArray1().GetHashCode(),
                new StackArray3().GetHashCode(),
                new StackArray6().GetHashCode(),
                new StackArray7().GetHashCode(),
                new StackArray14().GetHashCode(),
                new StackArray15().GetHashCode(),
                new StackArray16().GetHashCode(),
                new StackList1<int>() { 1 }.GetHashCode(),
                new StackList3<int>() { 1, 2, 3 }.GetHashCode(),
                new StackListSwapRemove<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.GetHashCode(),
            };

            var distinct = new System.Collections.Generic.HashSet<int>(hashes);
            Must.BeEqual(hashes.Length, distinct.Count);
        });
    });
});





// TEST: partial type declaration within global namespace

[FGDebug]
public partial record Debug<T> : IDisposable
{
    [FGDebug]
    sealed partial class Nested
    {
        [FGDebug]
        partial class OneAnother
        {
        }
    }

    void IDisposable.Dispose() => throw new NotImplementedException();

    private protected async Task __PrivateProtectedGenericAsyncMethod__<TValue>(T t, TValue? u)
        where TValue : class?, IDisposable, new() => throw new NotImplementedException();

    internal string? InternalNullableStringReturningMethod(ref T value) => throw new NotImplementedException();
    protected string? ProtectedNullableTParameterMethod(out T? value) => throw new NotImplementedException();

    T NonNullableT => throw new NotImplementedException();
    T? NullableT => throw new NotImplementedException();

    public T this[int? value] => NonNullableT;
    public T? this[(float? num, string? text) value] => NullableT;

    protected internal int GetSet { get; set; }
    protected internal int GetInit { get; init; }

    public record __NestedRecord__<A>(int? Number, string Text) where A : struct { }
    internal record struct __NestedRecordStruct__<A>(int? Number, string Text) where A : class { }
    protected readonly record struct __NestedReadOnlyRecordStruct__<A>(int? Number, string Text) where A : class? { }
    protected internal readonly struct __NestedReadOnlyStruct__<A>(int? Number, string Text) where A : notnull { }
    protected internal readonly ref struct __NestedReadOnlyRefStruct__<A>(int? Number, string Text) where A : notnull { }
    private protected struct __NestedStruct__<A>(int? Number, string Text) where A : unmanaged { }
    protected internal class __NestedClass__<A>(int? Number, string Text) where A : new() { }
    protected sealed class __NestedSealedClass__<A>(int? Number, string Text) where A : class, new() { }
    internal abstract class __NestedAbstractClass__<A>(int? Number, string Text) where A : class?, new() { }
    private static class __NestedStaticClass__<A> where A : IDisposable?, new() { }
    interface __INestedInterface__<in A, out B> where A : B, IDisposable, new(), allows ref struct { }

    public class NestAlpha
    {
        public class NestBravo
        {
            public class NestCharlie : IDisposable
            {
                public void Dispose() { }
            }
        }
        public class Foo
        {
            public class Bar
            {
                public class Baz
                {
                }
            }
        }
    }
}
