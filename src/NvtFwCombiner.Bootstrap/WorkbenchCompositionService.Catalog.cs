using System.Collections.ObjectModel;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Typed facade used by the desktop shell to query catalogs and run application services.</summary>
public static partial class WorkbenchCompositionService
{
    internal const string StandardMergeFallbackOutputFileName = "nvt-fw-combiner-output.bin";

    private static readonly Lazy<ReadOnlyCollection<WorkbenchProfileSummary>> s_standardMergeProfileSummaries = new(
        CreateStandardMergeProfileSummaries);

    private static readonly Lazy<ReadOnlyCollection<WorkbenchProfileSummary>> s_replaceProfileSummaries = new(
        CreateReplaceProfileSummaries);

    /// <summary>Gets selectable IC ids from the IC support catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return IcSupportCatalog.IcIds;
    }

    /// <summary>Gets the catalog-owned initial IC id for shell/workbench surfaces.</summary>
    public static string GetDefaultIcId()
    {
        return IcSupportCatalog.DefaultIcId;
    }

    /// <summary>Gets concise grouped IC-number choices for workbench selection controls.</summary>
    public static IReadOnlyList<WorkbenchIcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        return Array.AsReadOnly(IcNumberChoicePolicy.GetNumberSelectionChoices(GetPostbuildProfiles(icId))
            .Select(static choice => new WorkbenchIcNumberChoice(choice.Token, choice.DisplayLabel))
            .ToArray());
    }

    private static IReadOnlyList<LegacyCombinerPostbuildProfile> GetPostbuildProfiles(string icId)
    {
        return IcSupportCatalog.TryFind(icId, out _)
            ? BuiltInPostbuildProfileCatalog.GetProfiles(IcSupportCatalog.NormalizeIcId(icId))
            : [];
    }

    private static bool TryGetDefaultPostbuildProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetPostbuildProfiles(icId);
        postbuildProfile = profiles.Count == 0 ? null : profiles[0];
        return postbuildProfile is not null;
    }

    private static bool TrySelectPostbuildProfileByCommonFwVersion(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        return BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
            IcSupportCatalog.NormalizeIcId(icId),
            commonFwVersion,
            out postbuildProfile,
            out issue);
    }

    /// <summary>Gets compiled Standard Merge profile summaries in stable CLI/display order.</summary>
    public static IReadOnlyList<WorkbenchProfileSummary> GetStandardMergeProfileSummaries()
    {
        return s_standardMergeProfileSummaries.Value;
    }

    /// <summary>Gets compiled Replace profile summaries in stable CLI/display order.</summary>
    public static IReadOnlyList<WorkbenchProfileSummary> GetReplaceProfileSummaries()
    {
        return s_replaceProfileSummaries.Value;
    }

    private static WorkbenchProfileSummary? FindStandardMergeProfileSummaryByIc(string icId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        return s_standardMergeProfileSummaries.Value.FirstOrDefault(profile =>
            string.Equals(profile.IcId, icId, StringComparison.Ordinal));
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        IReadOnlyList<string> icIds = IcSupportCatalog.IcIds;
        return new WorkbenchSettingsSnapshot(
            icIds.Count,
            s_standardMergeProfileSummaries.Value.Count,
            s_replaceProfileSummaries.Value.Count,
            icIds.Count(icId => IcSupportCatalog.SupportsWorkflow(icId, IcWorkflowIds.CtrlRamReplace)));
    }

    private static ReadOnlyCollection<WorkbenchProfileSummary> CreateStandardMergeProfileSummaries()
    {
        return Array.AsReadOnly(
            BuiltInV2RegistrationRegistry.StandardMerge
                .Select(static registration => registration.CreateProfileSummary())
                .OrderBy(static profile => profile.IcId, StringComparer.Ordinal)
                .ToArray());
    }

    private static ReadOnlyCollection<WorkbenchProfileSummary> CreateReplaceProfileSummaries()
    {
        return Array.AsReadOnly(
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values
                .Select(static registration => registration.CreateProfileSummary())
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
