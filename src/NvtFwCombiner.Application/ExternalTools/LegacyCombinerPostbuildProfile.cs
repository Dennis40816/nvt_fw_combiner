using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Postbuild command profile for one IC family.</summary>
public sealed class LegacyCombinerPostbuildProfile
{
    private readonly LegacyCombinerPostbuildCommand[] _singleCommands;
    private readonly LegacyCombinerPostbuildCommand[] _cascadeCommands;
    private readonly LegacyCombinerPostbuildCommand[]? _twoChipCommands;
    private readonly LegacyCombinerPostbuildCommand[]? _threeChipCommands;
    private readonly LegacyCombinerPostbuildPlanSelector[] _planSelectors;
    private readonly CompiledPlanTemplate[] _compiledPlans;

    /// <summary>Creates a postbuild command profile.</summary>
    public LegacyCombinerPostbuildProfile(
        string processorId,
        string icId,
        string toolBindingId,
        string firmwareFileName,
        IEnumerable<LegacyCombinerPostbuildCommand> singleCommands,
        IEnumerable<LegacyCombinerPostbuildCommand> cascadeCommands,
        string evidence,
        IEnumerable<LegacyCombinerPostbuildCommand>? twoChipCommands = null,
        IEnumerable<LegacyCombinerPostbuildCommand>? threeChipCommands = null,
        IEnumerable<LegacyCombinerPostbuildPlanSelector>? planSelectors = null,
        LegacyCombinerPostbuildAssemblyKind assemblyKind = LegacyCombinerPostbuildAssemblyKind.InPlaceFirmwareImage,
        LegacyCombinerCommonFwVersion? effectiveCommonFwVersion = null,
        LegacyCombinerFirmwareConfigWriteRoute firmwareConfigWriteRoute =
            LegacyCombinerFirmwareConfigWriteRoute.Unavailable,
        LegacyCombinerDiffDlmPolicy? diffDlmPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(firmwareFileName);
        ArgumentNullException.ThrowIfNull(singleCommands);
        ArgumentNullException.ThrowIfNull(cascadeCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (firmwareFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            firmwareFileName is "." or ".." ||
            Path.GetFileName(firmwareFileName) != firmwareFileName)
        {
            throw new ArgumentException("Firmware file name must be a plain file name.", nameof(firmwareFileName));
        }

        _singleCommands = [.. singleCommands];
        _cascadeCommands = [.. cascadeCommands];
        _twoChipCommands = twoChipCommands is null ? null : [.. twoChipCommands];
        _threeChipCommands = threeChipCommands is null ? null : [.. threeChipCommands];
        _planSelectors = BuildPlanSelectors(planSelectors);
        if (_singleCommands.Length == 0 || _cascadeCommands.Length == 0)
        {
            throw new ArgumentException("Postbuild profile must declare both single and cascade command branches.");
        }

        if (_twoChipCommands is { Length: 0 } ||
            _threeChipCommands is { Length: 0 })
        {
            throw new ArgumentException("Explicit IC-count command branches cannot be empty.");
        }

        ValidateSelectorBranches(_planSelectors, _twoChipCommands, _threeChipCommands);

        ProcessorId = processorId;
        IcId = icId;
        ToolBindingId = toolBindingId;
        FirmwareFileName = firmwareFileName;
        Evidence = evidence;
        DisplayCategory = CreateDisplayCategory(evidence, processorId);
        AssemblyKind = assemblyKind;
        EffectiveCommonFwVersion = effectiveCommonFwVersion ?? LegacyCombinerCommonFwVersion.MinimumSupported;
        FirmwareConfigWriteRoute = firmwareConfigWriteRoute;
        DiffDlmPolicy = diffDlmPolicy;
        _compiledPlans =
        [
            .. _planSelectors.Select(selector => new CompiledPlanTemplate(this, selector)),
        ];
    }

    /// <summary>Processor id referenced by composition profiles.</summary>
    public string ProcessorId { get; }

    /// <summary>IC id covered by this postbuild profile.</summary>
    public string IcId { get; }

    /// <summary>Approved external tool binding id.</summary>
    public string ToolBindingId { get; }

    /// <summary>Firmware file name used inside the staging output directory.</summary>
    public string FirmwareFileName { get; }

    /// <summary>Single-chip command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> SingleCommands => _singleCommands;

    /// <summary>Cascade command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand> CascadeCommands => _cascadeCommands;

    /// <summary>Optional explicit two-chip command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand>? TwoChipCommands => _twoChipCommands;

    /// <summary>Optional explicit three-chip command branch.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildCommand>? ThreeChipCommands => _threeChipCommands;

    /// <summary>Typed, non-overlapping IC-count selectors exposed by this command profile.</summary>
    public IReadOnlyList<LegacyCombinerPostbuildPlanSelector> PlanSelectors => _planSelectors;

    /// <summary>Reference files that justify this command profile.</summary>
    public string Evidence { get; }

    /// <summary>Short display category derived from the primary postbuild evidence source.</summary>
    public string DisplayCategory { get; }

    /// <summary>Declares whether postbuild output is final flash or refreshed TP_FW requiring assembly.</summary>
    public LegacyCombinerPostbuildAssemblyKind AssemblyKind { get; }

    /// <summary>Inclusive Common FW version at which this runtime profile becomes effective.</summary>
    public LegacyCombinerCommonFwVersion EffectiveCommonFwVersion { get; }

    /// <summary>Reviewed pre-postbuild FWConfig source route whose result must reach canonical Backup.</summary>
    public LegacyCombinerFirmwareConfigWriteRoute FirmwareConfigWriteRoute { get; }

    /// <summary>Optional canonical count-dependent DiffDLM preservation policy.</summary>
    public LegacyCombinerDiffDlmPolicy? DiffDlmPolicy { get; }

    /// <summary>Resolves one already-compiled command shape from IC-number context.</summary>
    public LegacyCombinerPostbuildCommandPlan ResolvePlan(
        IcNumberSelection? icNumberSelection,
        int? reportedChipCount = null)
    {
        LegacyCombinerPostbuildPlanSelector selector = ResolveSelector(icNumberSelection);
        int topologyCount = icNumberSelection is null
            ? 1
            : selector.ResolveTopologyCount(icNumberSelection, reportedChipCount);
        return ResolvePlan(selector, topologyCount);
    }

    /// <summary>Returns the reviewed minimum-count binding of one owned compiled selector.</summary>
    public LegacyCombinerPostbuildCommandPlan ResolvePlan(LegacyCombinerPostbuildPlanSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return ResolvePlan(selector, selector.MinimumCount);
    }

    /// <summary>Binds an exact topology count to an owned, already-compiled command shape.</summary>
    public LegacyCombinerPostbuildCommandPlan ResolvePlan(
        LegacyCombinerPostbuildPlanSelector selector,
        int topologyCount)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return _compiledPlans.SingleOrDefault(candidate =>
                ReferenceEquals(candidate.Selector, selector))?.Bind(topologyCount) ??
            throw new ArgumentException(
                "The selected postbuild plan selector must belong to the supplied profile.",
                nameof(selector));
    }

    internal bool Owns(LegacyCombinerPostbuildCommandPlan plan)
    {
        return ReferenceEquals(plan.Profile, this) &&
            _compiledPlans.Any(template => template.Owns(plan));
    }

    private LegacyCombinerPostbuildPlanSelector ResolveSelector(IcNumberSelection? selection)
    {
        if (selection is null)
        {
            return _planSelectors.Single(static selector =>
                selector.Kind == LegacyCombinerPostbuildPlanSelectorKind.SingleChip);
        }

        LegacyCombinerPostbuildPlanSelector[] matches =
        [
            .. _planSelectors.Where(selector => selector.Matches(selection)),
        ];
        return matches.Length == 1
            ? matches[0]
            : throw new ArgumentException(
                $"IC number selection '{(selection.Parts.Count == 0 ? "<empty>" : selection.Parts[^1])}' is not supported by postbuild profile '{ProcessorId}'.");
    }

    internal sealed class CompiledPlanTemplate
    {
        private readonly CompiledCommandShape _invariantShape;
        private readonly Dictionary<int, CompiledCommandShape>? _countedShapes;
        private readonly LegacyCombinerPostbuildProfile _profile;

        internal CompiledPlanTemplate(
            LegacyCombinerPostbuildProfile profile,
            LegacyCombinerPostbuildPlanSelector selector)
        {
            _profile = profile;
            Selector = selector;
            if (selector.Branch == LegacyCombinerPostbuildBranch.Cascade &&
                profile.DiffDlmPolicy is { } policy)
            {
                _countedShapes = Enumerable.Range(
                        Math.Max(selector.MinimumCount, policy.MinimumIcCount),
                        Math.Min(selector.MaximumCount, policy.MaximumIcCount) -
                        Math.Max(selector.MinimumCount, policy.MinimumIcCount) + 1)
                    .ToDictionary(
                        static count => count,
                        count => CompileShape(profile, selector, count));
                _invariantShape = _countedShapes[selector.MinimumCount];
            }
            else
            {
                _invariantShape = CompileShape(profile, selector, selector.MinimumCount);
            }
        }

        internal LegacyCombinerPostbuildPlanSelector Selector { get; }

        internal LegacyCombinerPostbuildCommandPlan Bind(int topologyCount)
        {
            if (!Selector.MatchesReportedChipCount(topologyCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(topologyCount),
                    topologyCount,
                    "Topology count is outside the selected postbuild plan.");
            }

            CompiledCommandShape shape = _countedShapes is null
                ? _invariantShape
                : _countedShapes.TryGetValue(topologyCount, out CompiledCommandShape? counted)
                    ? counted
                    : throw CreateUnsupportedCount(topologyCount);
            return new LegacyCombinerPostbuildCommandPlan(
                _profile,
                this,
                topologyCount,
                shape.Commands,
                shape.ProtocolPlan);
        }

        internal bool Owns(LegacyCombinerPostbuildCommandPlan plan)
        {
            if (!ReferenceEquals(plan.Template, this))
            {
                return false;
            }

            CompiledCommandShape shape = _countedShapes is null
                ? _invariantShape
                : _countedShapes[plan.TopologyCount];
            return ReferenceEquals(plan.ProtocolPlan, shape.ProtocolPlan) &&
                ReferenceEquals(plan.Commands, shape.Commands);
        }

        private static CompiledCommandShape CompileShape(
            LegacyCombinerPostbuildProfile profile,
            LegacyCombinerPostbuildPlanSelector selector,
            int topologyCount)
        {
            IReadOnlyList<LegacyCombinerPostbuildCommand> commands =
                LegacyCombinerPostbuildPlanCompiler.ResolveCommands(profile, selector, topologyCount);
            return new CompiledCommandShape(
                commands,
                LegacyCombinerPostbuildPlanCompiler.CompileProtocol(profile, commands));
        }

        private ArgumentOutOfRangeException CreateUnsupportedCount(int topologyCount)
        {
            _ = _profile.DiffDlmPolicy!.GetActiveRecordCount(topologyCount);
            throw new System.Diagnostics.UnreachableException();
        }

        private sealed record CompiledCommandShape(
            IReadOnlyList<LegacyCombinerPostbuildCommand> Commands,
            ExternalProcessorProtocolPlan ProtocolPlan);
    }

    private static LegacyCombinerPostbuildPlanSelector[] BuildPlanSelectors(
        IEnumerable<LegacyCombinerPostbuildPlanSelector>? planSelectors)
    {
        LegacyCombinerPostbuildPlanSelector[] selectors = planSelectors is null
            ? [
                new LegacyCombinerPostbuildPlanSelector(
                    LegacyCombinerPostbuildPlanSelectorKind.SingleChip,
                    LegacyCombinerPostbuildBranch.SingleChip),
                new LegacyCombinerPostbuildPlanSelector(
                    LegacyCombinerPostbuildPlanSelectorKind.GenericCascade,
                    LegacyCombinerPostbuildBranch.Cascade),
            ]
            : [.. planSelectors];
        if (selectors.Length == 0)
        {
            throw new ArgumentException("Postbuild profile must declare at least one plan selector.", nameof(planSelectors));
        }

        for (int left = 0; left < selectors.Length; left++)
        {
            for (int right = left + 1; right < selectors.Length; right++)
            {
                if (selectors[left].MinimumCount <= selectors[right].MaximumCount &&
                    selectors[right].MinimumCount <= selectors[left].MaximumCount)
                {
                    throw new ArgumentException("Postbuild plan selector count ranges cannot overlap.", nameof(planSelectors));
                }
            }
        }

        return !selectors.Any(static selector => selector.Kind == LegacyCombinerPostbuildPlanSelectorKind.SingleChip)
            ? throw new ArgumentException("Postbuild profile must declare one single-chip selector.", nameof(planSelectors))
            : selectors;
    }

    private static void ValidateSelectorBranches(
        IEnumerable<LegacyCombinerPostbuildPlanSelector> selectors,
        IReadOnlyList<LegacyCombinerPostbuildCommand>? twoChipCommands,
        IReadOnlyList<LegacyCombinerPostbuildCommand>? threeChipCommands)
    {
        foreach (LegacyCombinerPostbuildPlanSelector selector in selectors)
        {
            if ((selector.Branch == LegacyCombinerPostbuildBranch.TwoChip && twoChipCommands is null) ||
                (selector.Branch == LegacyCombinerPostbuildBranch.ThreeChip && threeChipCommands is null))
            {
                throw new ArgumentException($"Postbuild selector '{selector.Token}' has no distinct command branch.");
            }
        }
    }

    private static string CreateDisplayCategory(string evidence, string processorId)
    {
        string primaryEvidence = evidence.Split(';', StringSplitOptions.TrimEntries)[0];
        string fileName = Path.GetFileName(primaryEvidence.Replace('\\', '/'));
        string category = string.IsNullOrWhiteSpace(fileName)
            ? processorId
            : Path.GetFileNameWithoutExtension(fileName);
        const string prefix = "PostbuildSetup_";
        return category.StartsWith(prefix, StringComparison.Ordinal)
            ? category[prefix.Length..]
            : category;
    }
}

/// <summary>Typed pre-postbuild firmware-config write route of a legacy postbuild profile.</summary>
public enum LegacyCombinerFirmwareConfigWriteRoute
{
    /// <summary>No reviewed source-to-Backup route is available; version authoring must fail closed.</summary>
    Unavailable,

    /// <summary>The selected command plan explicitly copies one firmware-image source to canonical Backup.</summary>
    CommandSourceToCanonicalBackup,

    /// <summary>The legacy mode copies the TP flash-map primary FWConfig into the canonical NVT Backup.</summary>
    PrimaryToCanonicalBackup,
}
