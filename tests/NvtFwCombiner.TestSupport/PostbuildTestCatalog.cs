using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.TestSupport;

/// <summary>Test compatibility projection over the hash-pinned production data catalog.</summary>
public static class PostbuildTestCatalog
{
    /// <summary>All deployed Postbuild profiles.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> All => BuiltInPostbuildProfileCatalog.All;

    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51917 => Find("nfc.nt51917.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51919 => Find("nfc.nt51919.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51920 => Find("nfc.nt51920.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51923 => Find("nfc.nt51923.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51926 => Find("nfc.nt51926.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51926CommonFw141 => Find("nfc.nt51926.ctrlram-postbuild-fw1.4.1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51927 => Find("nfc.nt51927.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51928 => Find("nfc.nt51928.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51929 => Find("nfc.nt51929.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51930CommonFw1x => Find("nfc.nt51930.ctrlram-postbuild-fw1.x");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51931 => Find("nfc.nt51931.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51932 => Find("nfc.nt51932.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51950 => Find("nfc.nt51950.ctrlram-postbuild-v1");
    /// <summary>Named fixture projection.</summary>
    public static LegacyCombinerPostbuildProfile Nt51951 => Find("nfc.nt51951.ctrlram-postbuild-v1");

    /// <summary>Gets all profiles for an IC.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> GetProfiles(string icId)
    {
        return BuiltInPostbuildProfileCatalog.GetProfiles(icId);
    }

    /// <summary>Gets the first profile for an IC.</summary>
    public static bool TryGetDefaultProfile(string icId, out LegacyCombinerPostbuildProfile? profile)
    {
        return BuiltInPostbuildProfileCatalog.TryGetDefaultProfile(icId, out profile);
    }

    /// <summary>Selects a versioned profile.</summary>
    public static bool TrySelectProfileForCommonFwVersion(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? profile,
        out string? issue)
    {
        return BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
            icId,
            commonFwVersion,
            out profile,
            out issue);
    }

    private static LegacyCombinerPostbuildProfile Find(string processorId)
    {
        return All.Single(profile => profile.ProcessorId == processorId);
    }
}
