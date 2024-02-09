using Microsoft.CodeAnalysis;

namespace PodNet.Analyzers.Testing.CodeAnalysis.Fakes;

/// <summary>A fake for proxying calls to the underlying <paramref name="globalOptions"/>, <paramref name="optionsForSyntaxTrees"/> and <paramref name="optionsForAdditionalTexts"/>.</summary>
/// <param name="globalOptions">The fake global analyzer options instance.</param>
/// <param name="optionsForSyntaxTrees">The fake options for individual <see cref="SyntaxTree"/>s.</param>
/// <param name="optionsForAdditionalTexts">The fake options for inidividual <see cref="Microsoft.CodeAnalysis.AdditionalText"/>s.</param>
public class AnalyzerConfigOptionsProvider(
    AnalyzerConfigOptions globalOptions,
    Dictionary<SyntaxTree, AnalyzerConfigOptions> optionsForSyntaxTrees,
    Dictionary<Microsoft.CodeAnalysis.AdditionalText, AnalyzerConfigOptions> optionsForAdditionalTexts) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
{
    /// <summary>The global options, as they are available from the options provided during construction.</summary>
    public override AnalyzerConfigOptions GlobalOptions => globalOptions;

    /// <summary>The lookup options for each additional syntax tree.</summary>
    public Dictionary<SyntaxTree, AnalyzerConfigOptions> OptionsForSyntaxTrees { get; } = optionsForSyntaxTrees;

    /// <summary>The lookup options for each additional text file.</summary>
    public Dictionary<Microsoft.CodeAnalysis.AdditionalText, AnalyzerConfigOptions> OptionsForAdditionalTexts { get; } = optionsForAdditionalTexts;

    /// <summary>Gets the <see cref="GlobalOptions"/>, and any additional options specific to <paramref name="key"/>.</summary>
    /// <param name="key">The syntax tree to look up additional options for.</param>
    /// <returns>The <see cref="GlobalOptions"/>, and any additional options specific to <paramref name="key"/>.</returns>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree key)
    {
        if (OptionsForSyntaxTrees.TryGetValue(key, out var value))
            return new(new Dictionary<string, string?>(GlobalOptions.Values.Concat(value.Values)));
        return GlobalOptions;
    }

    /// <inheritdoc cref="GetOptions(SyntaxTree)"/>
    public override AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.AdditionalText key)
    {
        if (OptionsForAdditionalTexts.TryGetValue(key, out var value))
            return new(new Dictionary<string, string?>(GlobalOptions.Values.Concat(value.Values)));
        return GlobalOptions;
    }
}
