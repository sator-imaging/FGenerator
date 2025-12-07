// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/FGenerator

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FGenerator
{
    /// <summary>
    /// Represents the outcome of analyzing a target, including diagnostic metadata and message.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct AnalyzeResult
    {
        private static readonly Dictionary<string, DiagnosticDescriptor> DescriptorCache = new();

        private readonly string Id;
        private readonly string Title;
        private readonly DiagnosticSeverity Severity;
        private readonly string Message;
        private readonly Location? OverrideLocation;

        /// <summary>
        /// Creates a new analysis result with the specified diagnostic details.
        /// </summary>
        /// <param name="id">Diagnostic identifier without the generator prefix.</param>
        /// <param name="title">Diagnostic title displayed by IDE tooling.</param>
        /// <param name="severity">Severity level reported for the diagnostic.</param>
        /// <param name="message">Diagnostic message describing the issue.</param>
        /// <param name="overrideLocation">Optional location to use instead of the target's location.</param>
        public AnalyzeResult(
            string id,
            string title,
            DiagnosticSeverity severity,
            string message,
            Location? overrideLocation = null  // TODO: v2
        )
        {
            Id = id;
            Title = title;
            Severity = severity;
            Message = message;
            OverrideLocation = overrideLocation;
        }

        /// <summary>
        /// Creates a <see cref="Diagnostic"/> for this analysis result using a cached descriptor.
        /// </summary>
        /// <param name="diagnosticIdPrefix">Prefix applied to the diagnostic ID (e.g., "GEN").</param>
        /// <param name="diagnosticCategory">Category reported to Roslyn for this diagnostic.</param>
        /// <param name="location">Location used when no override location was provided on the result.</param>
        /// <returns>A diagnostic created from a cached descriptor keyed by the composed diagnostic ID.</returns>
        public Diagnostic ToDiagnostic(string diagnosticIdPrefix, string diagnosticCategory, Location location)
        {
            var diagnosticId = $"{diagnosticIdPrefix}{Id}";

            if (!DescriptorCache.TryGetValue(diagnosticId, out var result) || !string.Equals(result.Title.ToString(), Title, System.StringComparison.Ordinal))
            {
                DescriptorCache[diagnosticId] = result = new DiagnosticDescriptor(
                    id: diagnosticId,
                    title: Title,
                    messageFormat: "{0}",
                    category: diagnosticCategory,
                    defaultSeverity: Severity,
                    isEnabledByDefault: true);
            }

            return Diagnostic.Create(result, OverrideLocation ?? location, Message);
        }
    }
}
