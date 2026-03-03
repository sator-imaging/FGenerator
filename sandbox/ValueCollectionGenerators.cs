using System;

namespace FGenerator.Sandbox
{
    namespace StackArray
    {
        // TEST: GetHashCode (HashCode.Combine call differences)
        [StackArray(6, typeof(int))]
        public struct StackArray6
        {
        }

        [StackArray(7, typeof(int))]
        public struct StackArray7
        {
        }

        [StackArray(14, typeof(int))]
        public struct StackArray14
        {
        }

        [StackArray(15, typeof(int))]
        public struct StackArray15
        {
        }

        [StackArray(16, typeof(int))]
        public struct StackArray16
        {
        }

        // TEST: Complex target types
        [StackArray(1, typeof(DateTimeOffset))]
        public struct StackArray1
        {
        }

        [StackArray(3, typeof(int?))]
        public struct StackArray3
        {
        }
    }

    namespace StackList
    {
        // TEST: GetHashCode() result
        [StackList(1)]
        public struct StackList1<T> where T : unmanaged
        {
        }

        [StackList(3)]
        public struct StackList3<T> where T : unmanaged
        {
        }

        // TEST: IndexOf should use span's IndexOf instead of EqualityComparer<T>.Default (IEquatable<T>)
        // TEST: SwapRemove in action
        [StackList(9, SwapRemove = true)]
        public struct StackListSwapRemove<T> where T : unmanaged, IEquatable<T>
        {
        }
    }
}