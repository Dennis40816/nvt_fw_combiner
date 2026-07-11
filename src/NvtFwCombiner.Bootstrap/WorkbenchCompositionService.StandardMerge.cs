using NvtFwCombiner.Domain.Composition;
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
        return IcMetadataFacade.SupportsWorkflow(icId, IcWorkflowIds.StandardMerge) &&
            StandardMergeProfilesByIc.ContainsKey(icId);
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
        return IcMetadataFacade.IsDpPerspectiveIc(icId)
            ? $"TP paste range: {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)}; {FormatDisplayRange(DpPerspectiveCatalog.CustomerInfoPreserveRange)} is preserved customer information."
            : "Address ranges come from the built-in Standard Merge profile.";
    }

    /// <summary>Gets normal Standard Merge DP range-extraction size facts when the selected IC permits nonstandard source lengths.</summary>
    public static bool TryGetStandardMergeDpInputLengthPolicy(
        string icId,
        out WorkbenchStandardMergeDpInputLengthPolicy policy)
    {
        policy = default!;
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
        {
            return false;
        }

        AddressSpace? dpInput = profile.AddressSpaces.SingleOrDefault(space =>
            string.Equals(space.AddressSpaceId, CompositionAddressSpaceIds.DpInput, StringComparison.Ordinal));
        if (dpInput is null ||
            dpInput.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
            dpInput.ExpectedInputLengths.Count == 0)
        {
            return false;
        }

        policy = new WorkbenchStandardMergeDpInputLengthPolicy(
            dpInput.Length,
            dpInput.ExpectedInputLengths);
        return true;
    }

    private static IReadOnlyList<string> GetRequiredAddressSpaces(CompositionProfileDefinition profile)
    {
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return compile.IsSuccess ? compile.CompiledComposition!.Plan.RequiredInputAddressSpaceIds : [];
    }
}
