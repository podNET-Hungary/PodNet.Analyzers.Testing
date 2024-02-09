using Microsoft.CodeAnalysis;

namespace PodNet.Analyzers.Testing.CodeAnalysis.Fakes;

/// <summary>A fake for proxying calls to the underlying <paramref name="globalOptions"/>, <paramref name="optionsForSyntaxTrees"/> and <paramref name="optionsForAdditionalTexts"/>.</summary>
/// <param name="globalOptions">The fake global analyzer options instance.</param>
/// <param name="optionsForSyntaxTrees">The fake options for individual <see cref="SyntaxTree"/>s.</param>
/// <param name="optionsForAdditionalTexts">The fake options for inidividual <see cref="Microsoft.CodeAnalysis.AdditionalText"/>s.</param>
public class AnalyzerConfigOptionsProvider(
    AnalyzerConfigOptions globalOptions,
    IReadOnlyDictionary<SyntaxTree, AnalyzerConfigOptions> optionsForSyntaxTrees,
    IReadOnlyDictionary<Microsoft.CodeAnalysis.AdditionalText, AnalyzerConfigOptions> optionsForAdditionalTexts) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
{
    /// <summary>The global options, as they are available from the options provided during construction.</summary>
    public override AnalyzerConfigOptions GlobalOptions => globalOptions;

    /// <summary>Gets the <see cref="GlobalOptions"/>, and any additional options specific to <paramref name="key"/>.</summary>
    /// <param name="key">The syntax tree to look up additional options for.</param>
    /// <returns>The <see cref="GlobalOptions"/>, and any additional options specific to <paramref name="key"/>.</returns>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree key)
    {
        if (optionsForSyntaxTrees.TryGetValue(key, out var value))
            return new(new Dictionary<string, string?>(GlobalOptions.Values.Concat(value.Values)));
        return GlobalOptions;
    }

    /// <inheritdoc cref="GetOptions(SyntaxTree)"/>
    public override AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.AdditionalText key)
    {
        if (optionsForAdditionalTexts.TryGetValue(key, out var value))
            return new(new Dictionary<string, string?>(GlobalOptions.Values.Concat(value.Values)));
        return GlobalOptions;
    }
}
