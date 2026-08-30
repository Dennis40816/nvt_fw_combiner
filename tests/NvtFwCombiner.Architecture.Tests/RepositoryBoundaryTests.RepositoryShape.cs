namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    // Catastrophic-growth alarm only. Cohesion and semantic ownership are enforced by
    // targeted boundary tests; physical line count is not an architecture proxy.
    private const int LargeFileLineThreshold = 2_500;

    /// <summary>Prevents catastrophic single-file growth without prescribing arbitrary splits.</summary>
    [Fact]
    public void RepositoryTextFilesStayBelowEmergencyCeiling()
    {
        string[] checkedRoots = ["src", "tests", "docs", "eng"];
        string[] checkedExtensions = [".cs", ".axaml", ".md", ".targets"];

        string[] oversizedFiles =
        [
            .. checkedRoots
                .Select(root => Path.Combine(Root.FullName, root))
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                .Where(path => checkedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
                .Select(path => new
                {
                    Path = Path.GetRelativePath(Root.FullName, path).Replace('\\', '/'),
                    Lines = File.ReadLines(path).Count(),
                })
                .Where(file => file.Lines > LargeFileLineThreshold)
                .OrderByDescending(file => file.Lines)
                .ThenBy(file => file.Path, StringComparer.Ordinal)
                .Select(file => $"{file.Lines}: {file.Path}"),
        ];

        Assert.Empty(oversizedFiles);
    }

    private static bool HasPathSegment(string path, string segment)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }
}
