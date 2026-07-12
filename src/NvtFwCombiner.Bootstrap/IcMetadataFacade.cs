using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Read-only projection of the canonical IC onboarding, TP flash-map, TP header, and postbuild catalogs.
/// This type owns no firmware facts; it prevents UI and CLI callers from joining catalog categories themselves.
/// </summary>
internal static class IcMetadataFacade
{
    private static IReadOnlyList<IcMetadata> Metadata { get; } = BuildMetadata().AsReadOnly();
    private static readonly Dictionary<string, IcMetadata> MetadataByIc = Metadata
        .ToDictionary(metadata => metadata.IcId, StringComparer.Ordinal);

    /// <summary>All selectable IC metadata rows in stable display order.</summary>
    public static IReadOnlyList<IcMetadata> All => Metadata;

    /// <summary>All selectable IC identifiers in stable display order.</summary>
    public static IReadOnlyList<string> IcIds { get; } =
        Array.AsReadOnly(Metadata.Select(static metadata => metadata.IcId).ToArray());

    /// <summary>Catalog-owned initial IC identifier for workbench surfaces.</summary>
    public static string DefaultIcId => IcSupportCatalog.DefaultIcId;

    /// <summary>Shared TP header/write section taxonomy used by postbuild planning and run-report classification.</summary>
    public static IReadOnlyList<TpHeaderSection> TpHeaderSections => TpHeaderCatalog.All;

    /// <summary>Finds normalized IC metadata without exposing the underlying catalog joins.</summary>
    public static bool TryFind(string icId, out IcMetadata? metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return MetadataByIc.TryGetValue(NormalizeIcId(icId), out metadata);
    }

    /// <summary>Returns whether a selectable IC exposes an approved workbench workflow.</summary>
    public static bool SupportsWorkflow(string icId, string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return TryFind(icId, out IcMetadata? metadata) &&
            metadata is not null &&
            metadata.SupportsWorkflow(workflowId);
    }

    /// <summary>Gets the profile-declared IC-number choices for a selectable IC.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return TryFind(icId, out IcMetadata? metadata) && metadata is not null
            ? metadata.NumberChoices
            : [];
    }

    /// <summary>Gets grouped UI choices without exposing every legacy branch alias.</summary>
    public static IReadOnlyList<IcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        return TryFind(icId, out IcMetadata? metadata) && metadata is not null
            ? metadata.NumberSelectionChoices
            : [];
    }

    /// <summary>Gets the TP Overview FWConfig start for a selectable IC.</summary>
    public static bool TryGetFirmwareConfigStart(string icId, out long firmwareConfigStart)
    {
        if (TryFind(icId, out IcMetadata? metadata) && metadata is not null)
        {
            firmwareConfigStart = metadata.FirmwareConfigStart;
            return true;
        }

        firmwareConfigStart = 0;
        return false;
    }

    /// <summary>Returns true when the IC uses the profile-owned DP Perspective container policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return TryFind(icId, out IcMetadata? metadata) &&
            metadata is not null &&
            metadata.UsesDpPerspective;
    }

    /// <summary>Gets the approved postbuild category variants for a selectable IC.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> GetPostbuildProfiles(string icId)
    {
        return TryFind(icId, out _) ? LegacyCombinerPostbuildCatalog.GetProfiles(NormalizeIcId(icId)) : [];
    }

    /// <summary>Selects the approved postbuild category for a base image Common FW version.</summary>
    public static bool TrySelectPostbuildProfile(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        if (!TryFind(icId, out _))
        {
            postbuildProfile = null;
            issue = $"No IC metadata is registered for {icId}.";
            return false;
        }

        return LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            NormalizeIcId(icId),
            commonFwVersion,
            out postbuildProfile,
            out issue);
    }

    /// <summary>Gets the catalog display fallback when a base image has not been selected yet.</summary>
    public static bool TryGetDefaultPostbuildProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        return TryFind(icId, out _) && LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(
            NormalizeIcId(icId),
            out postbuildProfile);
    }

    private static List<IcMetadata> BuildMetadata()
    {
        List<IcMetadata> metadata = [];
        foreach (IcSupportEntry support in IcSupportCatalog.All)
        {
            if (!TpFlashMapCatalog.TryFind(support.IcId, out TpFlashMapProfile? flashMap))
            {
                throw new InvalidOperationException(
                    $"IC metadata requires a TP flash-map profile for {support.IcId}.");
            }

            IReadOnlyList<LegacyCombinerPostbuildProfile> postbuildProfiles =
                LegacyCombinerPostbuildCatalog.GetProfiles(support.IcId);
            metadata.Add(new IcMetadata(
                support.IcId,
                Array.AsReadOnly(support.WorkflowIds.ToArray()),
                support.StandardMergeSourceIcId,
                support.CtrlRamPostbuildSourceIcId,
                support.Notes,
                flashMap!.OverviewSource,
                flashMap.FirmwareConfigStart,
                Array.AsReadOnly(TpFlashMapCatalog.GetNumberChoices(support.IcId).ToArray()),
                Array.AsReadOnly(TpFlashMapCatalog.GetNumberSelectionChoices(support.IcId).ToArray()),
                Array.AsReadOnly(postbuildProfiles
                    .Select(profile => profile.DisplayCategory)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()),
                DpPerspectiveCatalog.IsSupportedIc(support.IcId)));
        }

        return metadata;
    }

    private static string NormalizeIcId(string icId)
    {
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}

/// <summary>One read-only joined IC metadata row. Firmware semantics remain owned by the source catalogs.</summary>
internal sealed record IcMetadata(
    string IcId,
    IReadOnlyList<string> WorkflowIds,
    string? StandardMergeSourceIcId,
    string? CtrlRamPostbuildSourceIcId,
    string? Notes,
    string TpOverviewSource,
    long FirmwareConfigStart,
    IReadOnlyList<string> NumberChoices,
    IReadOnlyList<IcNumberChoice> NumberSelectionChoices,
    IReadOnlyList<string> PostbuildCategories,
    bool UsesDpPerspective)
{
    /// <summary>Returns true when this IC exposes the selected workflow.</summary>
    public bool SupportsWorkflow(string workflowId)
    {
        return WorkflowIds.Contains(workflowId, StringComparer.Ordinal);
    }

    /// <summary>Returns true when one or more approved postbuild categories are declared.</summary>
    public bool HasPostbuild => PostbuildCategories.Count > 0;
}
