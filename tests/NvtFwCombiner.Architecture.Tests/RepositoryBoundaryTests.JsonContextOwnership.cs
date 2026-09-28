using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>
    /// Keeps one source-generated metadata owner for each JSON root type, so a document is never bound through two
    /// contexts whose options could drift, and its metadata graph is generated and initialized once.
    /// </summary>
    [Fact]
    public void SourceGeneratedJsonRootsHaveOneContextOwner()
    {
        string[] roots =
        [
            .. JsonSerializableRootRegex().Matches(ReadProductionSources())
                .Select(static match => string.Concat(
                    match.Groups["type"].Value.Where(static character => !char.IsWhiteSpace(character)))),
        ];
        string[] duplicates =
        [
            .. roots
                .GroupBy(static root => root, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .Order(StringComparer.Ordinal),
        ];

        Assert.NotEmpty(roots);
        Assert.Empty(duplicates);
    }

    [GeneratedRegex(@"\[JsonSerializable\(\s*typeof\((?<type>[^)]+)\)")]
    private static partial Regex JsonSerializableRootRegex();
}
