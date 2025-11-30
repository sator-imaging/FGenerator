using System;
using StackArrayGenerator;
using StackListGenerator;

namespace SampleConsumer
{
    namespace StackArray
    {
        // TEST: GetHashCode (HashCode.Combine call differences)
        [StackArray(6, typeof(int))] public partial struct StackArray6 { }
        [StackArray(7, typeof(int))] public partial struct StackArray7 { }
        [StackArray(14, typeof(int))] public partial struct StackArray14 { }
        [StackArray(15, typeof(int))] public partial struct StackArray15 { }
        [StackArray(16, typeof(int))] public partial struct StackArray16 { }

        // TEST: Complex target types
        [StackArray(1, typeof(DateTimeOffset))] public partial struct StackArray1 { }
        [StackArray(3, typeof(int?))] public partial struct StackArray3 { }
    }

    namespace StackList
    {
        // TEST: GetHashCode() result
        [StackList(1)] public partial struct StackList1<T> where T : unmanaged { }
        [StackList(3)] public partial struct StackList3<T> where T : unmanaged { }

        // TEST: IndexOf should use span's IndexOf instead of EqualityComparer<T>.Default (IEquatable<T>)
        // TEST: SwapRemove in action
        [StackList(9, SwapRemove = true)]
        public partial struct StackListSwapRemove<T> where T : unmanaged, IEquatable<T>
        {
        }
    }
}
