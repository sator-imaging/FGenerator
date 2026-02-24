using System;

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

    public partial class DiscoverOuterClass
    {
        [Discover]
        public partial class DiscoverNestedClass
        {
            [Discover]
            public void NestedMethod() { }
        }
    }
}
