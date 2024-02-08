using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Immutable;

namespace PodNet.Analyzers.Testing.CSharp;

/// <summary>This <see cref="MetadataReferenceResolver"/> can be used to resolve in-memory references for assemblies, so that there's no need to actually persist dynamic assemblies when executing them after compilation. This is actually a proxy for a <see cref="MetadataReferenceResolver"/>, which, if fails to <see cref="MetadataReferenceResolver.ResolveReference"/>, the ones provided via <see cref="AddInMemoryReference"/> will be tried.</summary>
/// <remarks>
/// For context, the default implementation of <see cref="CSharpScript"/> doesn't allow metadata and assemblies that are in-memory and not sourced from the filesystem: 
/// <list type="bullet">
/// <item><see href="https://github.com/dotnet/roslyn/issues/2246"/></item>
/// <item><see href="https://stackoverflow.com/questions/61711153/system-io-filenotfoundexception-when-using-csharpscript-in-blazor-wasm"/></item>
/// </list>
/// <para>This class is immutable.</para>
/// </remarks>
/// <param name="rootResolver">The root resolver to proxy <see cref="ResolveReference"/> calls to. Defaults to <see cref="ScriptMetadataResolver.Default"/>.</param>
/// <param name="inMemoryReferences">The additional references to try to resolve from, when <paramref name="rootResolver"/> can't resolve one.</param>
public sealed class InMemoryScriptMetadataResolver(
    MetadataReferenceResolver? rootResolver = null,
    ImmutableDictionary<string, PortableExecutableReference>? inMemoryReferences = null) : MetadataReferenceResolver
{
    /// <summary>The root resolver to proxy <see cref="ResolveReference"/> calls to. Defaults to <see cref="ScriptMetadataResolver.Default"/>.</summary>
    public MetadataReferenceResolver RootResolver { get; } = rootResolver ?? ScriptMetadataResolver.Default;

    /// <summary>The additional references to try to resolve from, when <see cref="RootResolver"/> can't resolve one.</summary>
    public ImmutableDictionary<string, PortableExecutableReference> InMemoryReferences { get; } = inMemoryReferences ?? ImmutableDictionary<string, PortableExecutableReference>.Empty;

    public override bool Equals(object? other) => Equals(this, other);
    public override int GetHashCode() => RootResolver.GetHashCode() * 21;

    /// <summary>Resolves the given reference using <see cref="RootResolver"/>, or when that fails to resolve, <see cref="InMemoryReferences"/>. The latter only takes into account <paramref name="reference"/>.</summary>
    public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string? baseFilePath, MetadataReferenceProperties properties) 
        => RootResolver.ResolveReference(reference, baseFilePath, properties) is { Length: > 0 } root ? root
            : InMemoryReferences.TryGetValue(reference, out var executable) ? [executable] : [];

    /// <summary>Creates a new <see cref="InMemoryScriptMetadataResolver"/> with the <paramref name="reference"/> being added to its <see cref="InMemoryReferences"/>.</summary>
    public InMemoryScriptMetadataResolver AddInMemoryReference(string reference, PortableExecutableReference executable)
        => new(RootResolver, InMemoryReferences.Add(reference, executable));
}
