using System.Collections;

namespace PodNet.Analyzers.Testing.CodeAnalysis.Fakes;

partial class AnalyzerConfigOptions : IDictionary<string, string?>, ICollection<(string Key, string? Value)>
{
    /// <inheritdoc/>
    public void Add(string key, string? value) => ((IDictionary<string, string?>)Values).Add(key, value);

    /// <inheritdoc/>
    public bool ContainsKey(string key) => ((IDictionary<string, string?>)Values).ContainsKey(key);

    /// <inheritdoc/>
    public bool Remove(string key) => ((IDictionary<string, string?>)Values).Remove(key);

    /// <inheritdoc/>
    public void Add(KeyValuePair<string, string?> item) => ((ICollection<KeyValuePair<string, string?>>)Values).Add(item);

    /// <inheritdoc/>
    public void Clear() => ((ICollection<KeyValuePair<string, string?>>)Values).Clear();

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, string?> item) => ((ICollection<KeyValuePair<string, string?>>)Values).Contains(item);

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, string?>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, string?>>)Values).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<string, string?> item) => ((ICollection<KeyValuePair<string, string?>>)Values).Remove(item);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, string?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, string?>>)Values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Values).GetEnumerator();

    /// <inheritdoc/>
    public void Add((string Key, string? Value) item) => Values.Add(item.Key, item.Value);

    /// <inheritdoc/>
    public bool Contains((string Key, string? Value) item) => Values.TryGetValue(item.Key, out var value) && value == item.Value;

    /// <inheritdoc/>
    public bool Remove((string Key, string? Value) item) => Contains(item) && values.Remove(item.Key);

    IEnumerator<(string Key, string? Value)> IEnumerable<(string Key, string? Value)>.GetEnumerator() => Values.Select(kv => (kv.Key, kv.Value)).GetEnumerator();

    /// <inheritdoc/>
    public void CopyTo((string Key, string? Value)[] array, int arrayIndex) => Values.Select(kv => (kv.Key, kv.Value)).ToList().CopyTo(array, arrayIndex);

    /// <inheritdoc cref="Dictionary{TKey, TValue}.Keys" />
    public override IEnumerable<string> Keys => Values.Keys;

    ICollection<string> IDictionary<string, string?>.Keys => ((IDictionary<string, string?>)Values).Keys;

    ICollection<string?> IDictionary<string, string?>.Values => ((IDictionary<string, string?>)Values).Values;

    /// <inheritdoc/>
    public int Count => ((ICollection<KeyValuePair<string, string?>>)Values).Count;

    /// <inheritdoc/>
    public bool IsReadOnly => ((ICollection<KeyValuePair<string, string?>>)Values).IsReadOnly;

    /// <inheritdoc/>
    public string? this[string key] { get => ((IDictionary<string, string?>)Values)[key]; set => ((IDictionary<string, string?>)Values)[key] = value; }
}