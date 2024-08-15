using DiffPlex.DiffBuilder;
using DiffPlex;
using System.Text;
using DiffPlex.DiffBuilder.Model;

namespace PodNet.Analyzers.Testing.Diffing;

/// <summary>
/// Helper to generate text diffs.
/// </summary>
public class TextDiff
{
    private static InlineDiffBuilder InlineDiffBuilder { get; } = new InlineDiffBuilder(Differ.Instance);

    /// <summary>
    /// Generates a string representing a line-by-line diff between two strings. For more control, see <see cref="DiffPlex.DiffBuilder.InlineDiffBuilder"/> at <see href="https://github.com/mmanela/diffplex/"/>.
    /// </summary>
    /// <param name="left">The first instance of text to compare.</param>
    /// <param name="right">The second instance of text to compare.</param>
    /// <param name="ignoreWhitespace">Set to true to ignore whitespace in the comparison.</param>
    /// <param name="changedLineNeighbors">Set to null to print the whole merged text. Set to a positive number to include this many number of neighboring lines next to changes.</param>
    /// <param name="includeLineNumbers">Set to true to include line numbers in the output.</param>
    /// <param name="consoleColorize">Set to true to include virtual terminal sequences that indicate changed background colors. Only set if known to be printing to a console, otherwise the escape sequences will pollute the output.</param>
    /// <returns>The comparison result. Null if <paramref name="left"/> and <paramref name="right"/> were found to be identical (considering <paramref name="ignoreWhitespace"/>).</returns>
    public static string? InlineDiff(string left, string right, bool ignoreWhitespace, int? changedLineNeighbors = 2, bool includeLineNumbers = true, bool consoleColorize = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(changedLineNeighbors ?? 0, 0, nameof(changedLineNeighbors));
        var diff = InlineDiffBuilder.BuildDiffModel(left, right, ignoreWhitespace);
        if (!diff.HasDifferences)
            return null;
        var sb = new StringBuilder();
        var totalLinesWidth = diff.Lines.Count.ToString().Length + 1;
        var changedIndexes = new HashSet<int>(diff.Lines.Select((l, i) => (HasChanges: l.Type is not ChangeType.Unchanged, Index: i)).Where(e => e.HasChanges).Select(e => e.Index));
        var lastEllipsis = false;
        foreach (var (line, index) in diff.Lines.Select((l, i) => (l, i)))
        {
            if (line.Type is ChangeType.Unchanged && changedLineNeighbors > 0 && !CloseToAChangedLine(index, changedIndexes, diff.Lines.Count, changedLineNeighbors.Value))
            {
                if (!lastEllipsis)
                {
                    sb.AppendLine("...");
                    lastEllipsis = true;
                }
                continue;
            }

            lastEllipsis = false;
            if (consoleColorize && line.Type is not ChangeType.Unchanged)
            {
                sb.Append(line.Type switch
                {
                    ChangeType.Deleted => "\x1B[41m",
                    ChangeType.Inserted => "\x1B[42m",
                    ChangeType.Imaginary => "\x1B[48;2;128;128;128m",
                    ChangeType.Modified => "\x1B[43m",
                    _ => throw new InvalidOperationException($"Unknown line diff type: {line.Type}")
                });
            }
            if (includeLineNumbers)
            {
                sb.Append(index.ToString()?.PadLeft(totalLinesWidth));
                sb.Append('|');
            }
            sb.Append(line.Type switch
            {
                ChangeType.Unchanged => "  |",
                ChangeType.Deleted => "- |",
                ChangeType.Inserted => "+ |",
                ChangeType.Imaginary => "  ",
                ChangeType.Modified => "M |",
                _ => throw new InvalidOperationException($"Unknown line diff type: {line.Type}")
            });
            sb.Append(line.Text);
            if (consoleColorize)
                sb.Append("\x1B[0m");
            sb.AppendLine();
        }

        return sb.ToString();

        static bool CloseToAChangedLine(int index, HashSet<int> changedIndexes, int totalLinesCount, int changedLineNeighbors)
        {
            for (var i = Math.Max(index - changedLineNeighbors, 0); i < Math.Min(totalLinesCount, index + changedLineNeighbors + 1); i++)
                if (i != index && changedIndexes.Contains(i))
                    return true;
            return false;
        }
    }
}
