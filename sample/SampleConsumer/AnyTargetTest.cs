
[assembly: AnyTarget]

namespace SampleConsumer.NestedNamespace.AnyTarget
{
    [AnyTarget]
    class Class
    {
        [AnyTarget]
        public static void Method() { }
    }

    [AnyTarget]
    readonly record struct Class<T>
    {
        [AnyTarget]
        public static void Method<U>() { }
    }
}
