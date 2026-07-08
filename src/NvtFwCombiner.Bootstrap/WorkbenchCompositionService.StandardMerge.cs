using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly Dictionary<string, CompositionProfileDefinition> StandardMergeProfilesByIc =
        BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.ToDictionary(
            profile => profile.IcId,
            StringComparer.Ordinal);

    /// <summary>Returns true when the selected IC has a built-in standard merge profile.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return StandardMergeProfilesByIc.ContainsKey(icId);
    }

    /// <summary>Gets the built-in standard merge profile id for the selected IC, if any.</summary>
    public static string? GetStandardMergeProfileId(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? profile.ProfileId
            : null;
    }

    /// <summary>Gets required standard merge input address spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? GetRequiredAddressSpaces(profile)
            : [];
    }

    /// <summary>Gets the profile-owned default Standard Merge output file name for the selected IC.</summary>
    public static string GetStandardMergeDefaultOutputFileName(string icId)
    {
        return StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? profile.DefaultOutputFileName
            : "nvt-fw-combiner-output.bin";
    }

    /// <summary>Gets a compact, catalog-backed policy summary for the selected Standard Merge IC.</summary>
    public static string GetStandardMergePolicySummary(string icId)
    {
        return DpPerspectiveCatalog.IsSupportedIc(icId)
            ? $"TP paste range: {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)}; {FormatDisplayRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} is preserved customer information."
            : "Address ranges come from the built-in Standard Merge profile.";
    }

    private static IReadOnlyList<string> GetRequiredAddressSpaces(CompositionProfileDefinition profile)
    {
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return compile.IsSuccess ? compile.Plan!.RequiredInputAddressSpaceIds : [];
    }
}
