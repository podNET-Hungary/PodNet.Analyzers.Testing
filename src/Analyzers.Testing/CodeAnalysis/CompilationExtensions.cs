using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PodNet.Analyzers.Testing.CodeAnalysis;

/// <summary>
/// Convenience methods for working with <see cref="Compilation"/>s.
/// </summary>
public static class CompilationExtensions
{
    /// <summary>
    /// Gets all <see cref="Diagnostic"/>s for the <paramref name="compilation"/>, optionally including from emitting to a (discarded) in-memory stream.
    /// </summary>
    /// <param name="compilation">The compilation to gather diagnostics for.</param>
    /// <param name="includeEmit">Set to true to include diagnostics from emitting the compilation.</param>
    /// <returns>The gathered diagnostics.</returns>
    public static ImmutableArray<Diagnostic> GetDiagnostics(this Compilation compilation, bool includeEmit = false)
    {
        var diagnostics = compilation.GetDiagnostics();
        if (includeEmit)
        {
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            diagnostics = diagnostics.AddRange(result.Diagnostics);
        }
        return diagnostics;
    }
}
