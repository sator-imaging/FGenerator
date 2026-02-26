using System;

[module: Discover] // Won't support
[assembly: Discover]

namespace SandboxTest
{
    [Discover]
    public partial class DiscoverTestClass
    {
        [Discover]
        public int discoveryField;

        [Discover]
        public int DiscoveryProperty { get; set; }

        [Discover]
        public event Action DiscoveryEvent;

        [Discover]
        public void DiscoveryMethod() { }

        [Discover]
        public int this[int index] => 0;
    }

    [Discover]
    public enum DiscoverTestEnum
    {
        [Discover]
        Value1
    }

    [Discover]
    public delegate void DiscoverTestDelegate();

    [Discover]
    public delegate void DiscoverTestDelegateWithArgs(int a, string b);

    [Discover]
    public delegate TResult DiscoverTestGenericDelegate<T1, TResult>(T1 arg, Func<T1, TResult> func);

    [Discover]
    public partial class DiscoverGenericClass<T>
    {
        [Discover]
        public void Method<TMethod>(T typeArg, TMethod methodTypeArg) { }

        [Discover]
        public int this[T value] => 0;

        [Discover]
        public delegate TMethod DiscoverNestedGenericDelegate<TMethod>(T typeArg, TMethod methodTypeArg);

        [Discover]
        public TMethod DiscoverNestedGenericMethod<TMethod>(T typeArg, TMethod methodTypeArg) => methodTypeArg;
    }

    public partial class DiscoverOuterClass
    {
        [Discover]
        public partial class DiscoverNestedClass
        {
            [Discover]
            public void NestedMethod() { }
        }
    }

    public partial class DiscoveryMoreCases
    {
        [Discover]
        public int this[int x, int y] => x + y;

        [Discover]
        public void TupleMethod((int x, int y) t) { }

        [Discover]
        public int this[(int x, int y) t] => t.x;

        [Discover]
        public void NoArgMethod() { }

        [Discover]
        public void WithArgsMethod(int x, string y) { }
    }

    [Discover]
    public readonly partial record struct DiscoverGenericRecord<[Discover] TValue>
    {
        // TODO: There is no way to emit diagnostic on return type of method symbol.
        [return: Discover]
        [Discover]
        public static TResult DiscoverMethod<[Discover] TResult>([Discover] TResult value) => value;

        [Discover]
        public int this[[Discover] int index] => 0;
    }
}
