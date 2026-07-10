namespace NvtFwCombiner.Bootstrap;

/// <summary>Creates opaque locators for host-owned in-memory input artifacts.</summary>
internal static class VirtualArtifactLocator
{
    private const string Prefix = "nvt-memory://";

    internal static string CreateGeneralReplacePatch(string patchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchId);
        return $"{Prefix}general-replace-patch/{patchId}";
    }

    internal static string CreateGeneralReplaceBaseSnapshot()
    {
        return $"{Prefix}general-replace-base/{Guid.NewGuid():N}";
    }

    internal static bool IsVirtual(string artifactId)
    {
        return artifactId.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
