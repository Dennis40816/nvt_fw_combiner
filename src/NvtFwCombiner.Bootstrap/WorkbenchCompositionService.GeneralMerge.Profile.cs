using System.Reflection;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const byte GeneralMergeFillByte = 0x00;

    private static string GeneralMergeProfileVersion =>
        typeof(WorkbenchCompositionService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

    /// <summary>Gets the default General Merge output length text for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputLength(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        long capacity = StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? profile.Initialization.Capacity
            : GetGeneralMergeCatalogFallbackCapacity(icId);
        return BootstrapRangeText.FormatHex(capacity);
    }

    /// <summary>Gets the profile-owned default General Merge output file name for the selected IC.</summary>
    public static string GetGeneralMergeDefaultOutputFileName(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge.bin";
    }

    /// <summary>Gets the profile id used by the General Merge workbench profile for the selected IC.</summary>
    public static string GetGeneralMergeWorkbenchProfileId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return $"{icId.ToLowerInvariant()}-general-merge-workbench";
    }

    private static bool TryParseGeneralMergeCapacity(
        string outputLength,
        out long capacity,
        out CompositionIssue? issue)
    {
        if (!BootstrapRangeText.TryParseNonNegativeLong(outputLength, out capacity) || capacity <= 0)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralMergeCapacityInvalid,
                "General Merge output length must be a positive byte count.",
                "output-length");
            return false;
        }

        if (capacity > int.MaxValue)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralMergeCapacityUnsupported,
                "General Merge output length exceeds the supported in-memory composition size.",
                "output-length");
            return false;
        }

        issue = null;
        return true;
    }

    private static CompositionProfileDefinition CreateGeneralMergeProfile(string icId, long capacity)
    {
        return new CompositionProfileDefinition(
            GetGeneralMergeWorkbenchProfileId(icId),
            GeneralMergeProfileVersion,
            icId,
            IcWorkflowIds.GeneralMerge,
            CompositionKind.Merge,
            IcWorkflowIds.GeneralMerge,
            GetGeneralMergeDefaultOutputFileName(icId),
            ImageInitialization.Blank(CompositionAddressSpaceIds.OutputImage, capacity, GeneralMergeFillByte),
            [
                new AddressSpace(CompositionAddressSpaceIds.OutputImage, capacity, AddressSpaceMutability.Mutable),
            ],
            [],
            [
                new ProfileRegion(
                    "general-output",
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, capacity),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: [IcWorkflowIds.GeneralMerge]),
            ],
            [
                new RegionAccessRule("general-output", RegionAccessKind.ExplicitRange, "General Merge explicit mapping output."),
            ],
            IcNumberInputMode.SingleSelector);
    }

    private static long GetGeneralMergeCatalogFallbackCapacity(string icId)
    {
        return TpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? profile) && profile is not null
            ? profile.Regions.Max(region => region.Range.EndExclusive)
            : throw new InvalidOperationException($"No Standard Merge profile or TP flash-map profile is available for '{icId}'.");
    }
}
