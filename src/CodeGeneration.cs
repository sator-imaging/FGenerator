using System.Runtime.InteropServices;

namespace FGenerator
{
    /// <summary>
    /// Represents a generated source file payload for the incremental generator.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct CodeGeneration
    {
        /// <summary>
        /// File hint name passed to the source generator context.
        /// </summary>
        public readonly string HintName;

        /// <summary>
        /// Generated source contents.
        /// </summary>
        public readonly string Source;

        /// <summary>
        /// Creates a generation result using a target to build the hint name.
        /// </summary>
        /// <param name="target">Target used to derive a unique hint name.</param>
        /// <param name="source">Generated source code.</param>
        public CodeGeneration(Target target, string source)
            : this(target.ToHintName(), source) { }

        /// <summary>
        /// Creates a generation result with an explicit hint name and source contents.
        /// </summary>
        /// <param name="hintName">Hint name passed to the generator context.</param>
        /// <param name="source">Generated source code.</param>
        public CodeGeneration(string hintName, string source)
        {
            HintName = hintName;
            Source = source;
        }
    }
}
