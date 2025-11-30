using FGDebugGenerator;
using SampleConsumer.NestedNamespace;
using SampleConsumer.StackArray;
using SampleConsumer.StackList;
using System;
using System.Threading.Tasks;

// Create a Person instance
var person = new Person<int>();

// Subscribe to PropertyChanged event (provided by the generator)
person.PropertyChanged += (sender, e) =>
{
    Console.WriteLine($"Property '{e.PropertyName}' changed!");
};

// Use the generated SetField method to update properties with notification
Console.WriteLine("Setting firstName...");
person.FirstName = "John";

Console.WriteLine("Setting lastName...");
person.LastName = "Doe";

Console.WriteLine("Celebrating birthday...");
person.CelebrateBirthday();

{
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(new Debug<int>().HelperMethodTests());
    Console.WriteLine($"Final state: {person.FullName}, Age: {person.Age}");
    Console.WriteLine($$"""
        ✓ Hash Code Tests
        * {{new StackArray1().GetHashCode()}}
        * {{new StackArray3().GetHashCode()}}
        * {{new StackArray6().GetHashCode()}}
        * {{new StackArray7().GetHashCode()}}
        * {{new StackArray14().GetHashCode()}}
        * {{new StackArray15().GetHashCode()}}
        * {{new StackArray16().GetHashCode()}}
        * {{new StackList1<int>() { 1 }.GetHashCode()}}
        * {{new StackList3<int>() { 1, 2, 3 }.GetHashCode()}}
        * {{new StackListSwapRemove<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.GetHashCode()}}
        """);
    Console.ForegroundColor = previousColor;
}



// TEST: partial type declaration within global namespace

#pragma warning disable CA1050
#pragma warning disable CS9113
#pragma warning disable IDE1006

[FGDebug]
public partial record Debug<T> : IDisposable
{
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
