using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;

namespace PodNet.Analyzers.Testing.CSharp;

public record ScriptExecutionResult<T>(EmitResult EmitResult, ScriptState<T> ScriptState)
{
    public T? ScriptResult => ScriptState is { ReturnValue: var value } ? value : default;
};
