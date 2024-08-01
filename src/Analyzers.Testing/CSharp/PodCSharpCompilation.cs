﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace PodNet.Analyzers.Testing.CSharp;

/// <summary>A highly opinionated helper around <see cref="CSharpCompilation"/>, that allows for running <see cref="IIncrementalGenerator"/>s and executing <see cref="CSharpScript"/>s for testing.</summary>
public static class PodCSharpCompilation
{
    /// <summary>
    /// Gets a <see cref="SyntaxTree"/> that represents the following code:
    /// <code>
    /// // <auto-generated/>
    /// global using global::System;
    /// global using global::System.Collections.Generic;
    /// global using global::System.IO;
    /// global using global::System.Linq;
    /// global using global::System.Net.Http;
    /// global using global::System.Threading;
    /// global using global::System.Threading.Tasks;
    /// </code>
    /// Useful to simulate the <c>ImplicitUsings</c> MSBuild property, which adds these <see langword="global using"/>s.
    /// </summary>
    public static SyntaxTree GetImplicitUsings()
        => CSharpSyntaxTree.ParseText("""
                // <auto-generated/>
                global using global::System;
                global using global::System.Collections.Generic;
                global using global::System.IO;
                global using global::System.Linq;
                global using global::System.Net.Http;
                global using global::System.Threading;
                global using global::System.Threading.Tasks;
                """);

    /// <summary>Filters the given assemblies to assemblies that have a <see cref="Assembly.Location"/> and are not <see cref="Assembly.IsDynamic"/>, then loads their <see cref="MetadataReference"/> from their physical location. Doesn't cache the instances, so avoid using in performance critical paths.</summary>
    /// <param name="assemblies">The assemblies to filter and project.</param>
    /// <returns>The filtered assemblies' reference.</returns>
    public static ImmutableArray<PortableExecutableReference> GetValidReferences(IEnumerable<Assembly> assemblies)
        => assemblies.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                     .Select(a => MetadataReference.CreateFromFile(a.Location))
                     .ToImmutableArray();

    /// <summary>Gets all valid assembly references from <see cref="AssemblyLoadContext.Default" /> (see <see cref="GetValidReferences"/>).</summary>
    public static ImmutableArray<PortableExecutableReference> GetCurrentReferences()
        => GetValidReferences(AssemblyLoadContext.Default.Assemblies);

    /// <summary>Creates an opinionated <see cref="CSharpCompilation"/> to simplify testing of <see cref="IIncrementalGenerator"/>s.</summary>
    /// <param name="sourceTexts">The initial source texts to add to the <see cref="CSharpCompilation"/>. If <paramref name="implicitUsings"/> is <see langword="true"/>, default <see langword="global using"/>s are also generated and added to the compilation as a source text (see <seealso cref="GetImplicitUsings"/>).</param>
    /// <param name="configureCompilation">An optional configuration callback that can be used to modify the default <see cref="CSharpCompilation"/> being created.</param>
    /// <param name="implicitUsings">If enabled, adds a generated source that simulates implicit default <see langword="global using"/>s (see <seealso cref="GetImplicitUsings"/>)).</param>
    /// <param name="addCurrentReferences">If enabled, loads all <see cref="MetadataReference"/>s available from <see cref="AssemblyLoadContext.Default"/> (see <seealso cref="GetCurrentReferences"/>)).</param>
    /// <param name="references">Additional references to add to the <see cref="CSharpCompilation"/>.</param>
    /// <param name="compilationOptions">The options to supply to the <see cref="CSharpCompilation"/> being created. Defaults to creating a <see cref="OutputKind.DynamicallyLinkedLibrary"/> with all other defaults unchanged.</param>
    /// <param name="assemblyName">The name of the underlying dynamic <see cref="Assembly"/> that can be created from the <see cref="CSharpCompilation"/>.</param>
    /// <returns>A preconfigured instance of <see cref="CSharpCompilation"/>.</returns>
    public static CSharpCompilation Create(
        IEnumerable<string> sourceTexts,
        Func<CSharpCompilation, CSharpCompilation>? configureCompilation = null,
        bool implicitUsings = true,
        bool addCurrentReferences = true,
        IEnumerable<MetadataReference>? references = null,
        CSharpCompilationOptions? compilationOptions = null,
        [CallerMemberName] string? assemblyName = null)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: sourceTexts.Select(s => CSharpSyntaxTree.ParseText(s)).Concat(implicitUsings ? [GetImplicitUsings()] : []),
            references: (references ?? []).Concat(addCurrentReferences ? GetCurrentReferences() : []),
            options: compilationOptions ?? new(OutputKind.DynamicallyLinkedLibrary));
        if (configureCompilation != null)
            compilation = configureCompilation(compilation);
        return compilation;
    }

    /// <summary>Creates a new <see cref="CSharpGeneratorDriver"/> and runs the supplied <paramref name="generators"/> on it using this <see cref="CSharpCompilation"/>.</summary>
    /// <param name="compilation">The compilation to run the provided <paramref name="generators"/> on.</param>
    /// <param name="generators">The generators to run on the <see cref="CSharpCompilation"/>.</param>
    /// <param name="configureGeneratorDriver">An optional callback to configure the default <see cref="CSharpGeneratorDriver"/>.</param>
    /// <param name="cancellationToken">The token that's being passed through to <see cref="GeneratorDriver.RunGenerators"/>.</param>
    /// <returns>The result of the run.</returns>
    public static GeneratorDriverRunResult RunGenerators(
        this CSharpCompilation compilation,
        IIncrementalGenerator[] generators,
        Func<CSharpGeneratorDriver, CSharpGeneratorDriver>? configureGeneratorDriver = null,
        CancellationToken cancellationToken = default)
    {
        var driver = CSharpGeneratorDriver.Create(generators);
        if (configureGeneratorDriver != null)
            driver = configureGeneratorDriver(driver);

        return driver.RunGenerators(compilation, cancellationToken).GetRunResult();
    }

    /// <summary>Creates a new <see cref="CSharpGeneratorDriver"/> and runs the supplied <paramref name="generators"/> on it using this <see cref="CSharpCompilation"/>. <b>Replaces</b> the <paramref name="compilation"/> with the resulting compilation.</summary>
    /// <param name="compilation">The compilation to run the provided <paramref name="generators"/> on.</param>
    /// <param name="generators">The generators to run on the <paramref name="compilation"/>.</param>
    /// <param name="configureGeneratorDriver">An optional callback to configure the default <see cref="CSharpGeneratorDriver"/>.</param>
    /// <param name="cancellationToken">The token that's being passed through to <see cref="GeneratorDriver.RunGeneratorsAndUpdateCompilation"/>.</param>
    /// <param name="diagnostics">The diagnostics generated by <see cref="GeneratorDriver.RunGeneratorsAndUpdateCompilation"/>.</param>
    /// <param name="outputCompilation">The updated compilation generated by <see cref="GeneratorDriver.RunGeneratorsAndUpdateCompilation"/>.</param>
    /// <returns>The result of the run.</returns>
    public static GeneratorDriverRunResult RunGenerators(
        this CSharpCompilation compilation,
        IIncrementalGenerator[] generators,
        out ImmutableArray<Diagnostic> diagnostics,
        out CSharpCompilation outputCompilation,
        Func<CSharpGeneratorDriver, CSharpGeneratorDriver>? configureGeneratorDriver = null,
        CancellationToken cancellationToken = default)
    {
        var driver = CSharpGeneratorDriver.Create(generators);
        if (configureGeneratorDriver != null)
            driver = configureGeneratorDriver(driver);

        var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputUntypedCompilation, out diagnostics, cancellationToken).GetRunResult();
        outputCompilation = (CSharpCompilation)outputUntypedCompilation;
        return result;
    }

    /// <summary>Compiles and emits this <paramref name="compilation"/>, <c>#r</c> loads it into the script context, and executes the provided <paramref name="cSharpScript"/> code on the resulting dynamic <see cref="Assembly"/>.</summary>
    /// <remarks>Calling this method with unvalidated code can pose a security risk.</remarks>
    /// <typeparam name="T">The type of the expected result from the script. Provide <see langword="object?"/> if no result is expected.</typeparam>
    /// <param name="compilation">The compilation to execute <paramref name="cSharpScript"/> on.</param>
    /// <param name="cSharpScript">The code to execute on the compiled assembly (see <see cref="CSharpScript"/>).</param>
    /// <param name="globals">The globals object. The public properties on this object will be available in the global scope of the <paramref name="cSharpScript"/>.</param>
    /// <param name="catchException">If specified, any exception thrown by the script top-level code is passed to <paramref name="catchException"/>. If it returns true the exception is caught and stored on the resulting <see cref="ScriptState"/>, otherwise the exception is propagated to the caller.</param>
    /// <param name="configureOptions">An optional callback to configure the default <see cref="ScriptOptions"/>.</param>
    /// <param name="cancellationToken">The cancellation token passed to underlying calls.</param>
    /// <param name="name">The name of the script being executed. Defaults to the caller member's name.</param>
    /// <returns>The result of the script execution, including the result from <see cref="Compilation.Emit(Stream, Stream?, Stream?, Stream?, IEnumerable{ResourceDescription}?, Microsoft.CodeAnalysis.Emit.EmitOptions, IMethodSymbol?, Stream?, IEnumerable{EmbeddedText}?, CancellationToken)"/> and any related <see cref="Diagnostic"/>s.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="Compilation.Emit(Stream, Stream?, Stream?, Stream?, IEnumerable{ResourceDescription}?, Microsoft.CodeAnalysis.Emit.EmitOptions, IMethodSymbol?, Stream?, IEnumerable{EmbeddedText}?, CancellationToken)"/> results in an <see cref="Microsoft.CodeAnalysis.Emit.EmitResult.Success"/> that is <see langword="false"/>.</exception>
    public static async Task<ScriptExecutionResult<T>> ExecuteScriptAsync<T>(
        this CSharpCompilation compilation,
        string cSharpScript,
        object? globals = null,
        Func<Exception, bool>? catchException = null,
        Func<ScriptOptions, ScriptOptions>? configureOptions = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string name = "Script")
    {
        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream, cancellationToken: cancellationToken);
        if (!emitResult.Success)
        {
            var exception = new InvalidOperationException($"Compilation error. See {nameof(InvalidOperationException.Data)} property for details. Diagnostics ({emitResult.Diagnostics.Length}): {string.Join("; ", emitResult.Diagnostics)}");
            exception.Data[nameof(emitResult)] = emitResult;
            throw exception;
        }
        assemblyStream.Position = 0;
        var reference = AssemblyMetadata.CreateFromStream(assemblyStream).GetReference();
        var options = ScriptOptions.Default
            .WithMetadataResolver(new InMemoryScriptMetadataResolver(ScriptMetadataResolver.Default)
                .AddInMemoryReference(name, reference))
            .AddReferences(reference)
            .AddReferences(CSharpInteractiveReferences)
            .AddImports(CSharpInteractiveUsings);
        if (configureOptions != null)
            options = configureOptions(options);
        var alc = new AssemblyLoadContext(name);
        assemblyStream.Position = 0;
        var assembly = alc.LoadFromStream(assemblyStream);
        var loader = new InteractiveAssemblyLoader();
        loader.RegisterDependency(assembly);

        var csharpScript = CSharpScript.Create<T>(cSharpScript, options, globals?.GetType(), loader);

        return new(emitResult, await csharpScript.RunAsync(globals, catchException, cancellationToken));
    }

    /// <summary>
    /// Copied from C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\CommonExtensions\Microsoft\VBCSharp\LanguageServices\InteractiveHost at \Core\CSharpInteractive.rsp and \Desktop\CSharpInteractive.rsp, version 2022 17.11.0 Preview 6.0
    /// </summary>
    public static IReadOnlyCollection<string> CSharpInteractiveReferences { get; } = [
        "System",
        "System.Core",
        "Microsoft.CSharp",
        "System.Collections",
        "System.Collections.Concurrent",
        "System.Console",
        "System.Diagnostics.Debug",
        "System.Diagnostics.Process",
        "System.Diagnostics.StackTrace",
        "System.Dynamic.Runtime",
        "System.Globalization",
        "System.IO",
        "System.IO.FileSystem",
        "System.IO.FileSystem.Primitives",
        "System.Linq",
        "System.Linq.Expressions",
        "System.Reflection",
        "System.Reflection.Extensions",
        "System.Reflection.Primitives",
        "System.Runtime",
        "System.Runtime.Extensions",
        "System.Runtime.Numerics",
        "System.Runtime.InteropServices",
        "System.Text.Encoding",
        "System.Text.Encoding.CodePages",
        "System.Text.Encoding.Extensions",
        "System.Text.RegularExpressions",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Threading.Tasks.Parallel",
        "System.Threading.Thread",
        "System.ValueTuple"];

    /// <summary>
    /// Copied from C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\CommonExtensions\Microsoft\VBCSharp\LanguageServices\InteractiveHost at \Core\CSharpInteractive.rsp and \Desktop\CSharpInteractive.rsp, version 2022 17.11.0 Preview 6.0
    /// </summary>
    public static IReadOnlyCollection<string> CSharpInteractiveUsings { get; } = [
        "System",
        "System.IO",
        "System.Collections.Generic",
        "System.Console",
        "System.Diagnostics",
        "System.Dynamic",
        "System.Linq",
        "System.Linq.Expressions",
        "System.Text",
        "System.Threading.Tasks"];
}