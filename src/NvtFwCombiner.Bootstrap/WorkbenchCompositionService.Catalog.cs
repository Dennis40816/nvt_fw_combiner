using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string ProfileSummaryFallbackOutputFileName = "nvt-fw-combiner-output.bin";

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

    /// <summary>Returns true when the IC uses the DP Perspective family policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return IcMetadataFacade.IsDpPerspectiveIc(icId);
    }

    /// <summary>Gets a compact policy summary for the selected DP Replace IC.</summary>
    public static string GetDpReplacePolicySummary(string icId)
    {
        return TryGetV2DpReplacePolicySummary(icId, out string v2Summary)
            ? v2Summary
            : IcMetadataFacade.IsDpPerspectiveIc(icId)
            ? $"DP replacement follows the selected base BIN length: {DpPerspectiveCatalog.FormatSupportedLengths()}; original TP range {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)} is restored from base."
            : "Build stays gated until this IC has approved DP Replace source mapping evidence.";
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        IReadOnlyList<string> toolBindingIds =
        [
            .. IcMetadataFacade.All
                .SelectMany(metadata => IcMetadataFacade.GetPostbuildProfiles(metadata.IcId))
                .Select(profile => profile.ToolBindingId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        return new WorkbenchSettingsSnapshot(
            StandardMergeProfileSummaries.Count,
            ReplaceProfileSummaries.Count,
            IcMetadataFacade.All.Count,
            IcMetadataFacade.All.Count(metadata => metadata.HasPostbuild),
            string.Join(", ", toolBindingIds),
            "external-tools/legacy-combiner/1.13.0/manifest.json");
    }

    internal static WorkbenchProfileSummary CreateProfileSummary(CompositionProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return compile.CompiledComposition is not { } composition
            ? new WorkbenchProfileSummary(
                profile.ProfileId,
                profile.IcId,
                profile.CompositionKind,
                [],
                profile.DefaultOutputFileName,
                null,
                CompileSucceeded: false,
                Array.AsReadOnly(compile.Issues.Select(static issue => issue.Code).ToArray()))
            : CreateProfileSummary(composition);
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
            .. BuiltInReplaceProfiles.All.Select(CreateProfileSummary),
            .. s_builtInV2DpReplaceByIc.Value.Values.Select(static registration => registration.CreateProfileSummary()),
        ];
        return Array.AsReadOnly(
            summaries
                .OrderBy(static profile => profile.ProfileId, StringComparer.Ordinal)
                .ToArray());
    }

    private static WorkbenchProfileSummary CreateProfileSummary(CompiledComposition composition)
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
