using Microsoft.CodeAnalysis.Text;

namespace PodNet.Analyzers.Testing.CodeAnalysis.Fakes;

/// <summary>A fake wrapper around <paramref name="path"/> and <paramref name="text"/>.</summary>
/// <param name="path">The "path" for the text file. Doesn't have to point to a valid location.</param>
/// <param name="text">The source text of the additional text file.</param>
public class AdditionalText(string path, SourceText? text) : Microsoft.CodeAnalysis.AdditionalText
{
    /// <inheritdoc cref="AdditionalText"/>
    public AdditionalText(string path, string text) : this(path, SourceText.From(text)) { }

    /// <summary>The "path" for the text file. Doesn't necessarily point to a valid location.</summary>
    public override string Path => path;
    
    /// <summary>Return the wrapped text of the current fake.</summary>
    /// <param name="cancellationToken">The cancellation token. Unused.</param>
    /// <returns>The source text that was referenced during construction of this instance.</returns>
    public override SourceText? GetText(CancellationToken cancellationToken = default) => text;
}