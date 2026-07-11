using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class BootstrapTestData
{
    internal static string GoldenPath(string relativePath)
    {
        string goldenRoot = RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash");
        return RepositoryPaths.PathFromRelative(goldenRoot, relativePath);
    }

    internal static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + index));
        }

        return bytes;
    }
}
