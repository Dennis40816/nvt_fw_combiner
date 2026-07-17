using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Typed facade used by the desktop shell to query catalogs and run application services.</summary>
public static partial class WorkbenchCompositionService
{
    internal const string StandardMergeFallbackOutputFileName = "nvt-fw-combiner-output.bin";

    private static readonly Lazy<ReadOnlyCollection<WorkbenchProfileSummary>> s_standardMergeProfileSummaries = new(
        CreateStandardMergeProfileSummaries);

    private static ReadOnlyCollection<WorkbenchProfileSummary> StandardMergeProfileSummaries =>
        s_standardMergeProfileSummaries.Value;

    private static readonly Lazy<ReadOnlyCollection<WorkbenchProfileSummary>> s_replaceProfileSummaries = new(
        CreateReplaceProfileSummaries);

    private static ReadOnlyCollection<WorkbenchProfileSummary> ReplaceProfileSummaries =>
        s_replaceProfileSummaries.Value;

    /// <summary>Gets selectable IC ids from the IC support catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return IcMetadataFacade.IcIds;
    }

    /// <summary>Gets the catalog-owned initial IC id for shell/workbench surfaces.</summary>
    public static string GetDefaultIcId()
    {
        return IcMetadataFacade.DefaultIcId;
    }

    /// <summary>Gets supported IC-number choices from the TP flash-map/postbuild catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return IcMetadataFacade.GetNumberChoices(icId);
    }

    /// <summary>Gets concise grouped IC-number choices for workbench selection controls.</summary>
    public static IReadOnlyList<WorkbenchIcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        return Array.AsReadOnly(IcMetadataFacade.GetNumberSelectionChoices(icId)
            .Select(static choice => new WorkbenchIcNumberChoice(choice.Token, choice.DisplayLabel))
            .ToArray());
    }

    /// <summary>Gets compiled Standard Merge profile summaries in stable CLI/display order.</summary>
    public static IReadOnlyList<WorkbenchProfileSummary> GetStandardMergeProfileSummaries()
    {
        return StandardMergeProfileSummaries;
    }

    /// <summary>Gets compiled Replace profile summaries in stable CLI/display order.</summary>
    public static IReadOnlyList<WorkbenchProfileSummary> GetReplaceProfileSummaries()
    {
        return ReplaceProfileSummaries;
    }

    private static WorkbenchProfileSummary? FindStandardMergeProfileSummaryByIc(string icId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        return StandardMergeProfileSummaries.FirstOrDefault(profile =>
            string.Equals(profile.IcId, icId, StringComparison.Ordinal));
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        int externalToolBindingCount = IcMetadataFacade.IcIds
            .SelectMany(IcMetadataFacade.GetPostbuildProfiles)
            .Select(static profile => profile.ToolBindingId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new WorkbenchSettingsSnapshot(
            IcMetadataFacade.IcIds.Count,
            StandardMergeProfileSummaries.Count,
            ReplaceProfileSummaries.Count,
            IcMetadataFacade.IcIds.Count(IcMetadataFacade.SupportsCtrlRamReplace),
            externalToolBindingCount);
    }

    private static ReadOnlyCollection<WorkbenchProfileSummary> CreateStandardMergeProfileSummaries()
    {
        WorkbenchProfileSummary[] summaries =
        [
            .. BuiltInV2StandardMergeRegistrations.Select(static registration => registration.CreateProfileSummary()),
        ];
        return Array.AsReadOnly(
            summaries
                .OrderBy(static profile => profile.IcId, StringComparer.Ordinal)
                .ToArray());
    }

    private static ReadOnlyCollection<WorkbenchProfileSummary> CreateReplaceProfileSummaries()
    {
        WorkbenchProfileSummary[] summaries =
        [
            .. BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values.Select(static registration => registration.CreateProfileSummary()),
        ];
        return Array.AsReadOnly(
            summaries
                .OrderBy(static profile => profile.ProfileId, StringComparer.Ordinal)
                .ToArray());
    }

    internal static WorkbenchProfileSummary CreateProfileSummary(CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        return new WorkbenchProfileSummary(
            composition.ProfileId,
            composition.IcId,
            composition.CompositionKind,
            Array.AsReadOnly(composition.Plan.RequiredInputAddressSpaceIds.ToArray()),
            composition.DefaultOutputFileName,
            composition.IcNumberPolicy,
            CompileSucceeded: true,
            []);
    }
}
