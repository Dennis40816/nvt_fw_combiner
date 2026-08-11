using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Adapts trusted CtrlRAM profiles and maps to the Application authoring use case.</summary>
internal sealed partial class BuiltInCtrlRamAuthoringAdapter(
    ICanonicalCapabilityQuery catalog,
    CanonicalCapabilityExperience projection) : ICtrlRamAuthoringAdapter
{
    private readonly ICanonicalCapabilityQuery _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly CanonicalCapabilityExperience _projection =
        projection ?? throw new ArgumentNullException(nameof(projection));

    public CtrlRamInspectionDisplay GetDiscoveryDisplay(
        string icId,
        string number,
        string? basePath)
    {
        return ResolveDisplay(icId, number, basePath);
    }

    private CtrlRamInspectionDisplay ResolveDisplay(
        string icId,
        string number,
        string? basePath)
    {
        LegacyCombinerPostbuildProfile? postbuildProfile =
            BuiltInFirmwareInspection.TryResolvePostbuildProfileFromBasePathForDisplay(
                _projection,
                icId,
                basePath,
                out LegacyCombinerPostbuildProfile? profile)
                    ? profile
                    : null;
        return CreateDisplay(
            icId,
            number,
            postbuildProfile,
            !string.IsNullOrWhiteSpace(basePath) && File.Exists(basePath));
    }

    internal static CtrlRamInspectionDisplay CreateDisplay(
        string icId,
        string number,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        bool hasReadableBase)
    {
        var selection = IcNumberSelection.FromToken(number);
        LegacyCombinerPostbuildCommandPlan? commandPlan = postbuildProfile?.ResolvePlan(selection);
        return MemoryLayoutProjector.ProjectCtrlRamDiscovery(
            number,
            commandPlan,
            BuiltInTpFlashMapCatalog.GetRegions(
                icId,
                selection,
                postbuildProfile,
                TpFlashMapRegionKind.CtrlRam),
            BuiltInTpFlashMapCatalog.GetPostbuildCtrlRamSources(
                icId,
                selection,
                postbuildProfile),
            hasReadableBase);
    }

    public CtrlRamAuthoringCompilation Resolve(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IReadOnlyDictionary<string, byte[]>? selectedInputBytes = null)
    {
        CtrlRamReplaceRunContext context =
            CreateCtrlRamReplaceRunContext(
                _projection,
                icId,
                number,
                slotPaths,
                firmwareVersionEdit,
                selectedInputBytes);
        IReadOnlyDictionary<string, string> expectedPaths =
            CreateExpectedPaths(context, slotPaths);
        return TryResolveCtrlRamCapability(
                _catalog,
                context,
                out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues)
            ? new CtrlRamAuthoringCompilation(capability, expectedPaths, [])
            : new CtrlRamAuthoringCompilation(null, expectedPaths, issues);
    }

    public bool IsAcceptedCapability(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IReadOnlyDictionary<string, byte[]>? selectedInputBytes,
        ResolvedCapability capability,
        out IReadOnlyDictionary<string, string> expectedPaths,
        out IReadOnlyList<CompositionIssue> issues)
    {
        CtrlRamReplaceRunContext context =
            CreateCtrlRamReplaceRunContext(
                _projection,
                icId,
                number,
                slotPaths,
                firmwareVersionEdit,
                selectedInputBytes);
        expectedPaths = CreateExpectedPaths(context, slotPaths);
        issues = context.ValidationIssues;
        return IsAcceptedCtrlRamCapability(
            context,
            capability);
    }

    private static Dictionary<string, string> CreateExpectedPaths(
        CtrlRamReplaceRunContext context,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.BasePath is not null)
        {
            expected.Add(CompositionAddressSpaceIds.ReferenceBase, context.BasePath);
        }

        foreach (TpCtrlRamPostbuildSource source in context.SelectedSources)
        {
            string slotId = DynamicCtrlRamReplacementIds.Create(source.SourceId);
            if (slotPaths.TryGetValue(slotId, out string? path))
            {
                expected.Add(slotId, path);
            }
        }

        return expected;
    }
}
