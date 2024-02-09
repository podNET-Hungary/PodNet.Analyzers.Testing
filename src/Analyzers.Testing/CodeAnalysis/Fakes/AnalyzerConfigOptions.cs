using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PodNet.Analyzers.Testing.CodeAnalysis.Fakes;

/// <summary>A fake and mutable options container that proxies <see cref="TryGetValue(string, out string?)"/> calls to the provided <paramref name="values"/>. Uses <see cref="StringComparer.OrdinalIgnoreCase"/> for comparison. Implements <see cref="IDictionary{TKey,TValue}"/> for easier access.</summary>
/// <param name="values">The values to proxy <see cref="TryGetValue(string, out string?)"/> calls to.</param>
public partial class AnalyzerConfigOptions(Dictionary<string, string?> values) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
{
    /// <summary>Create a new options instance with empty key-values.</summary>
    public AnalyzerConfigOptions() : this([]) { }

    /// <summary>The values the current container proxies calls to.</summary>
    public Dictionary<string, string?> Values { get; } = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the value for the given <paramref name="key"/> using <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
    /// <param name="key">The key to look up in the wrapped dictionary.</param>
    /// <param name="value">The value, if found.</param>
    /// <returns><see langword="true"/> if the key was contained in the wrapped dictionary.</returns>
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => Values.TryGetValue(key, out value);
}