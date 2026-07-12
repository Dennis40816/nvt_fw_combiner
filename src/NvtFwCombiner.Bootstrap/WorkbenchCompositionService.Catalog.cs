using System.Collections.ObjectModel;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static ReadOnlyCollection<WorkbenchProfileSummary> StandardMergeProfileSummaries { get; } =
        Array.AsReadOnly(BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
            .OrderBy(static profile => profile.IcId, StringComparer.Ordinal)
            .Select(CreateProfileSummary)
            .ToArray());

    private static ReadOnlyCollection<WorkbenchProfileSummary> ReplaceProfileSummaries { get; } =
        Array.AsReadOnly(BuiltInReplaceProfiles.All
            .OrderBy(static profile => profile.ProfileId, StringComparer.Ordinal)
            .Select(CreateProfileSummary)
            .ToArray());

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

    /// <summary>Returns true when the IC uses the DP Perspective family policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return IcMetadataFacade.IsDpPerspectiveIc(icId);
    }

    /// <summary>Gets a compact, catalog-backed policy summary for the selected DP Replace IC.</summary>
    public static string GetDpReplacePolicySummary(string icId)
    {
        return IcMetadataFacade.IsDpPerspectiveIc(icId)
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
            : new WorkbenchProfileSummary(
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
