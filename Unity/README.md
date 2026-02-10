> [!TIP]
> There are Unity-compatible source generators in [../Compiled](../Compiled/) folder.
> (`.meta` files included)



> [!IMPORTANT]
> Analyzers in this folder are not safe for use in production environment.
> Consider use [Unity-Analyzers](https://github.com/sator-imaging/Unity-Analyzers) instead.



# Unity Analyzers

This directory contains Unity-specific source generators and analyzers built with `FGenerator`.

## `UnityAsyncMethodAuditor`

An analyzer that audits async methods for unsafe calls to `UnityEngine.Object` members.

### Purpose

Unity's Object API must be called from the main thread. However, async methods may resume on different threads after `await` points, making it unsafe to access Unity objects after awaiting.

This analyzer specifically detects:
- Instance method calls to `UnityEngine.Object` (including MonoBehaviour, ScriptableObject) methods (e.g., `GetComponent`, `Instantiate`) within async methods.
- Member access (properties/fields) on Unity objects, **except** when using the safe helper methods.

### Injected Helpers

The analyzer injects the following helper methods into the assembly. These are **excluded** from analysis, allowing you to safely access Unity objects (assuming the helpers handle thread safety or you accept the risk):

```csharp
internal static class UnityAsyncMethodAuditor
{
    public static bool TryGet<TObject, TValue>(this TObject self, Func<TObject, TValue> getter, out TValue result)
        where TObject : UnityEngine.Object;

    public static bool TrySet<TObject, TValue>(this TObject self, TValue value, Action<TObject, TValue> setter)
        where TObject : UnityEngine.Object;

    public static bool TryRun<TObject>(this TObject self, Action<TObject> action)
        where TObject : UnityEngine.Object;
}
```

> [!TIP]
> Prefer using `TrySet` over `TryRun` when setting values. `TrySet` allows passing the value as an argument, which enables the lambda to be static and cached, avoiding closure allocations.


### Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| UAMA001 | Error | Unity object access in async method |

### How It Works

The analyzer uses symbol-based analysis to detect Unity object access:

1. **Targets all types** (no attribute required)
2. **Enumerates all async methods** in each type
3. **Analyzes expressions** in the method body:
   - Detects receiver-less method invocations (e.g., `GetComponent<T>()`) on Unity objects.
   - Detects member access on Unity objects.
   - **Ignores** usages of `TryGet`, `TrySet`, and `TryRun` extension methods.
4. **Reports violations** (`UAMA`) when unsafe patterns are found.

### Example

```csharp
using UnityEngine;
using System.Threading.Tasks;

public class MyBehaviour : MonoBehaviour
{
    // ❌ UAMA001: Instance method call or property access in async method
    private async Task UnsafeMethod()
    {
        await Task.Delay(1000);
        GetComponent<Rigidbody>();         // Error: Instance method call
        transform.position = Vector3.one;  // Error: Property access
    }

    // ✅ Safe (Ignored): Usage of TryGet/TrySet/TryRun
    private async Task SafeMethod()
    {
        await Task.Delay(1000);
        
        // Try methods are ignored by the analyzer
        if (!this.TryGet(x => x.transform, out var t)) return;

        // Prefer TrySet to avoid closure allocations (lambda can be static)
        if (!this.TrySet(Vector3.zero, (x, val) => x.transform.position = val)) return;

        // TryRun is also safe but may allocate if capturing variables (Designed for method invocation)
        if (!this.TryRun(x => x.transform.position = Vector3.one)) return;
    }
}
```
