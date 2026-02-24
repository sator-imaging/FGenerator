// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

using System;

namespace FGenerator
{
    /// <summary>
    /// Specifies the types of symbols to discover when searching for target attributes.
    /// </summary>
    [Flags]
    public enum DiscoveryTargets
    {
        /// <summary>Discover classes, structs, interfaces, enums, and delegates.</summary>
        Type = 1 << 0,
        /// <summary>Discover fields and enum members.</summary>
        Field = 1 << 1,
        /// <summary>Discover properties and indexers.</summary>
        Property = 1 << 2,
        /// <summary>Discover events.</summary>
        Event = 1 << 3,
        /// <summary>Discover methods, constructors, destructors, and operators.</summary>
        Method = 1 << 4,
    }
}
