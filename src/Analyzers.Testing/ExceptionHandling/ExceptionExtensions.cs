using System.Runtime.CompilerServices;

namespace PodNet.Analyzers.Testing.ExceptionHandling;

/// <summary>
/// Convenience helpers for working with <see cref="Exception"/>s.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Includes the given key-value in the provided <paramref name="exception"/>'s <see cref="Exception.Data"/> dictionary. Mutates the given <paramref name="exception"/>, thus it can be chained.
    /// </summary>
    /// <typeparam name="TException">The type of the <paramref name="exception"/>. Should be inferred.</typeparam>
    /// <param name="exception">The <see cref="Exception"/> to append the data to.</param>
    /// <param name="value">The value to set in the dictionary for <paramref name="key"/>.</param>
    /// <param name="key">The string key to set in the dictionary with the provided <paramref name="value"/>. Defaults to the name of the <paramref name="value"/> expression.</param>
    /// <returns>The mutated <paramref name="exception"/> instance.</returns>
    public static TException WithData<TException>(this TException exception, object? value, [CallerArgumentExpression(nameof(value))] string key = "data") where TException : Exception
    {
        exception.Data[key] = value;
        return exception;
    }
}