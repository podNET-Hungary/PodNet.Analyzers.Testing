using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;

namespace PodNet.Analyzers.Testing.CSharp;

/// <summary>A wrapper around the provided <paramref name="EmitResult"/> and <paramref name="ScriptState"/>.</summary>
/// <typeparam name="T">The expected result of the script to execute.</typeparam>
/// <param name="EmitResult">The result of <see cref="Compilation.Emit(Stream, Stream?, Stream?, Stream?, IEnumerable{ResourceDescription}?, EmitOptions, IMethodSymbol?, Stream?, IEnumerable{EmbeddedText}?, CancellationToken)"/>. Note that if <see cref="EmitResult.Success"/> is <see langword="false"/>, <see cref="PodCSharpCompilation.ExecuteScriptAsync"/> throws.</param>
/// <param name="ScriptState">The state of the executed script, including its result.</param>
public record ScriptExecutionResult<T>(EmitResult EmitResult, ScriptState<T> ScriptState)
{
    /// <summary>The result of the executed script, or the default value of <typeparamref name="T"/>.</summary>
    public T? ScriptResult => ScriptState is { ReturnValue: var value } ? value : default;
};
