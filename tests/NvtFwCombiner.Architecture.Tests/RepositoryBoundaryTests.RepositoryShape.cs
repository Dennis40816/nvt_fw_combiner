namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private const int LargeFileLineThreshold = 700;

    /// <summary>Verifies repo structure cleanup does not regress back into very large mixed-concern files.</summary>
    [Fact]
    public void RepositoryTextFilesStayBelowLargeFileThreshold()
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
