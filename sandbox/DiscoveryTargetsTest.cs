using System;

namespace SandboxTest
{
    [DiscoverTargets]
    public partial class DiscoveryTargetsTestClass
    {
        [DiscoverTargets]
        public int discoveryField;

        [DiscoverTargets]
        public int DiscoveryProperty { get; set; }

        [DiscoverTargets]
        public event Action DiscoveryEvent;

        [DiscoverTargets]
        public void DiscoveryMethod() { }

        [DiscoverTargets]
        public int this[int index] => 0;
    }

    [DiscoverTargets]
    public enum DiscoveryTargetsTestEnum
    {
        [DiscoverTargets]
        Value1
    }

    [DiscoverTargets]
    public delegate void DiscoveryTargetsTestDelegate();

    public partial class DiscoveryTargetsOuterClass
    {
        [DiscoverTargets]
        public partial class DiscoveryTargetsNestedClass
        {
            [DiscoverTargets]
            public void NestedMethod() { }
        }
    }
}
