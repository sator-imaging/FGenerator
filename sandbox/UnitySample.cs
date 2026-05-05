//#define __expected_error

using System;

#if __expected_error
using System.Threading.Tasks;
using UnityEngine;
#endif

#pragma warning disable IDE0078  // Use pattern matching
#pragma warning disable IDE0130  // Namespace does not match folder structure
#pragma warning disable CA1822  // Mark members as static
#pragma warning disable CA1050  // Declare types in namespaces
#pragma warning disable CA1816  // Call GC.SuppressFinalize correctly

namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public class MonoBehaviour : Component
    {
        public bool IsEnabled { get; set; }
        public string Name = string.Empty;
        public void Foo() { }
        public event Action? OnChanged;
    }
}

#if __expected_error

public class Behaviour : MonoBehaviour
{
    public async void Receiverless() => Foo();  // Error
}

public class BehaviourOther : MonoBehaviour, IDisposable
{
    public async Task This()
    {
        this.Foo();  // Error
    }

    public void Dispose() { }
}

namespace SampleConsumer
{
    internal class PureCSharpDirectMethodCallOnNullable
    {
        public async Task Test()
        {
            ((Behaviour?)null)?.Foo();  // Error
        }
    }

    internal class PureCSharpNullableMethod
    {
        public async Task Test()
        {
            Behaviour? nullable = null;
            nullable?.Foo();  // Error
        }
    }

    internal class PureCSharpNullablePropertyGet
    {
        public async Task Test()
        {
            Behaviour? nullable = null;
            _ = nullable?.Name;  // Error
        }
    }

    internal class PureCSharpNullablePropertySet
    {
        public async Task Test()
        {
            Behaviour? nullable = null;
            nullable?.Name = "Test";  // Error
        }
    }

    internal class PureCSharpDirectMethodCallOnNew
    {
        public async Task Test()
        {
            (new Behaviour()).Foo();  // Error
        }
    }

    internal class PureCSharpEvent
    {
        public async Task Test()
        {
            using var other = new BehaviourOther();
            other.OnChanged += () => { };  // Error
        }
    }

    internal class PureCSharpPropertySet
    {
        readonly Behaviour behaviour = new();
        public async Task Test() => behaviour.Name = "Test";  // Error
    }

    internal class PureCSharpPropertyGet
    {
        readonly Behaviour behaviour = new();
        public async Task<string> Test() => behaviour.Name;  // Error
    }

    internal class PureCSharpPropertyBoolean
    {
        readonly Behaviour behaviour = new();
        public async Task Test() => behaviour.IsEnabled = true;  // Error
    }

    internal class PureCSharpMethodCall
    {
        readonly Behaviour behaviour = new();
        public async Task Test() => behaviour.Foo();  // Error
    }

    internal class PureCSharp_NoError
    {
        public string Test()  // not async
        {
            var b = new Behaviour();
            b.Name = "Test";
            b.IsEnabled = true;
            b.Foo();

            return "Ok";
        }

        public async Task TestAsync()  // get/set via Try method
        {
            var behaviour = new Behaviour();
            if (behaviour != null && ((behaviour != null)))
            {
                behaviour.Name = "Test";
                behaviour.IsEnabled = true;
                behaviour.Foo();
            }

            behaviour.Name = "Test";

            if (!behaviour.TryGet(x => x.Name, out var name))
            {
                return;
            }

            if (behaviour.TrySet(("Test", true), (x, args) =>
                {
                    x.Name = args.Item1;
                    x.IsEnabled = args.Item2;
                    x.Foo();
                })
            )
            {
                await TestAsync();  // can call itself
            }

            behaviour.TrySet(name, (x, y) => x.Name = y);
            behaviour.TrySet(true, (x, y) => x.IsEnabled = y);
            behaviour.TryRun(x => x.Foo());
        }
    }
}

#endif
