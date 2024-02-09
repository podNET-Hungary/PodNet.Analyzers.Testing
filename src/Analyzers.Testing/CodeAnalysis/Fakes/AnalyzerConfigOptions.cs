using System.Diagnostics.CodeAnalysis;

namespace PodNet.Analyzers.Testing.CodeAnalysis.Fakes;

/// <summary>A fake options container that proxies <see cref="TryGetValue(string, out string?)"/> calls to the provided <paramref name="values"/>. Uses <see cref="StringComparer.OrdinalIgnoreCase"/> for comparison.</summary>
/// <param name="values">The values to proxy <see cref="TryGetValue(string, out string?)"/> calls to.</param>
public class AnalyzerConfigOptions(IReadOnlyDictionary<string, string?> values) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
{
    /// <summary>The values the current container proxies calls to.</summary>
    public IReadOnlyDictionary<string, string?> Values { get; } = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the value for the given <paramref name="key"/> using <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
    /// <param name="key">The key to look up in the wrapped dictionary.</param>
    /// <param name="value">The value, if found.</param>
    /// <returns><see langword="true"/> if the key was contained in the wrapped dictionary.</returns>
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => Values.TryGetValue(key, out value);

    /// <inheritdoc cref="IReadOnlyDictionary{TKey, TValue}.Keys" />
    public override IEnumerable<string> Keys => Values.Keys;
}