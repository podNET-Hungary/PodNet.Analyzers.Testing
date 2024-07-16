using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PodNet.Analyzers.Testing.ExceptionHandling;

namespace PodNet.Analyzers.Testing.CodeAnalysis;

/// <summary>
/// Contains helpers that make assertions on code analysis simpler.
/// </summary>
public static class AssertExtensions
{
    /// <summary>
    /// Assert that no <see cref="Diagnostic"/>s' severity is of at least the provided <see cref="DiagnosticSeverity"/>. Optionally try emitting the compilation to an assembly to gather additional diagnostics.
    /// </summary>
    /// <param name="compilation">The compilation to test for compilation errors.</param>
    /// <param name="diagnosticSeverity">No diagnostic should be of this or higher severity for the assertion to pass.</param>
    /// <param name="includeEmit">Set to true to include diagnostics from emitting the compilation.</param>
    public static void AssertDiagnosticLevelBelow(this Compilation compilation, DiagnosticSeverity diagnosticSeverity, bool includeEmit = false)
    {
        var diagnostics = compilation.GetDiagnostics(includeEmit);
        var errors = diagnostics.Where(d => d.Severity < diagnosticSeverity).ToList();
        if (errors.Count > 0)
            throw new AssertFailedException($"""There were {errors.Count} errors in the compilation. See {nameof(Exception.Data)} for details.""").WithData(errors);
    }

    /// <summary>
    /// Assert that no <see cref="Diagnostic"/>s are errors. Optionally try emitting the compilation to an assembly to gather additional diagnostics.
    /// </summary>
    /// <param name="compilation">The compilation to test for compilation errors.</param>
    /// <param name="includeEmit">Set to true to include diagnostics from emitting the compilation.</param>
    public static void AssertNoErrors(this Compilation compilation, bool includeEmit = false)
        => AssertDiagnosticLevelBelow(compilation, DiagnosticSeverity.Error, includeEmit);

    /// <summary>
    /// Asserts that there are no <see cref="Diagnostic"/>s in the compilation of whatever severity level.
    /// </summary>    
    /// /// <param name="compilation">The compilation to check.</param>
    /// <param name="includeEmit">Set to true to include diagnostics from emitting the compilation.</param>
    public static void AssertNoDiagnostics(this Compilation compilation, bool includeEmit = false)
    {
        var diagnostics = compilation.GetDiagnostics(includeEmit);
        if (diagnostics.Length > 0)
            throw new AssertFailedException($"""There were {diagnostics.Length} diagnostics in the compilation. See {nameof(Exception.Data)} for details.""").WithData(diagnostics);

    }
}
