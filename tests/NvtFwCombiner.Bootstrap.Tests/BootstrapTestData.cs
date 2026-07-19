using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class BootstrapTestData
{
    internal static string GoldenArtifactPath(string ic, string artifactId, string? variantOrVersion = null)
    {
        return CanonicalGoldenTestData.ArtifactPath(
            "standard-merge",
            ic,
            artifactId,
            variantOrVersion);
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
