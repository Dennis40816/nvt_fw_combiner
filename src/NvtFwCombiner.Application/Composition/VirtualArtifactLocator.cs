namespace NvtFwCombiner.Application.Composition;

/// <summary>Creates opaque locators for host-owned in-memory input artifacts.</summary>
public static class VirtualArtifactLocator
{
    private const string Prefix = "nvt-memory://";

    /// <summary>Creates one stable locator for an in-memory General Replace patch.</summary>
    public static string CreateGeneralReplacePatch(string patchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchId);
        return $"{Prefix}general-replace-patch/{patchId}";
    }

    /// <summary>Returns whether the locator represents a host-owned in-memory artifact.</summary>
    public static bool IsVirtual(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        return artifactId.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
