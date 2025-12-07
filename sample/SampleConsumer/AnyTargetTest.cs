[module: AnyTarget]    // won't support
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
    readonly record struct Class<[AnyTarget] TValue>
    {
        // TODO: there is no way to emit diagnostic on return type of method symbol.
        [return: AnyTarget]
        [AnyTarget]
        public static TResult Method<TResult>([AnyTarget] TResult value) => value;
    }
}
