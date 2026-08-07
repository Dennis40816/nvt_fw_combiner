using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Application-owned proof that one reviewed postbuild plan is bound to the
/// exact runtime-reference compilation which selected it.
/// </summary>
public sealed class RuntimeReferenceCompilationProof
{
    private readonly ByteRange[] _allowedWriteRanges;
    private readonly WriteRangeSectionIdentity[] _allowedWriteRangeSections;
    private readonly string[] _processorWriteViewIds;
    private readonly string _compilationFingerprint;
    private readonly string _processorId;
    private readonly string _toolBindingId;

    private RuntimeReferenceCompilationProof(
        string compilationFingerprint,
        string processorId,
        string toolBindingId,
        string selectorToken,
        string planFingerprint,
        ExternalProcessorInvocation invocation,
        IEnumerable<string> processorWriteViewIds)
    {
        _allowedWriteRanges = [.. invocation.AllowedWriteRanges];
        _allowedWriteRangeSections =
        [
            .. invocation.AllowedWriteRangeSections.Select(static section =>
                new WriteRangeSectionIdentity(
                    section.SectionId,
                    section.Range,
                    section.SourceRange)),
        ];
        _processorWriteViewIds = [.. processorWriteViewIds];
        _compilationFingerprint = compilationFingerprint;
        _processorId = processorId;
        _toolBindingId = toolBindingId;
        SelectorToken = selectorToken;
        PlanFingerprint = planFingerprint;
    }

    /// <summary>
    /// Creates a proof from one exact compiled composition and the typed
    /// postbuild plan selected for that same run.
    /// </summary>
    public static RuntimeReferenceCompilationProof CreateLegacyPostbuild(
        CompiledComposition composition,
        LegacyCombinerPostbuildCommandPlan plan)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(plan);
        RuntimeReferenceReplaceV2CompilationContext context =
            composition.V2Details.Provenance.Context as
                RuntimeReferenceReplaceV2CompilationContext ??
            throw new ArgumentException(
                "A runtime-reference proof requires an exact runtime-reference compilation.",
                nameof(composition));
        ExternalProcessorInvocation invocation = GetSingleProcessor(composition);
        if (!StringComparer.Ordinal.Equals(
                composition.V2Details.Provenance.Context.MemberId,
                plan.Profile.IcId) ||
            !StringComparer.Ordinal.Equals(
                invocation.ProcessorId,
                plan.Profile.ProcessorId) ||
            !StringComparer.Ordinal.Equals(
                invocation.ToolBindingId,
                plan.Profile.ToolBindingId))
        {
            throw new ArgumentException(
                "The selected postbuild plan does not match the compiled processor and tool binding.",
                nameof(plan));
        }

        if (context.ResolvedMap.TopologySelection is { } topology &&
            topology.ChipCount != plan.TopologyCount)
        {
            throw new ArgumentException(
                "The selected postbuild plan topology does not match the compiled resolved map.",
                nameof(plan));
        }

        long capacity = composition.Plan.OutputInitialization.Capacity;
        LegacyCombinerPostbuildCommandPlan resolvedPlan =
            LegacyCombinerPostbuildPlanner.CreatePlan(
                plan.Profile,
                plan.Selector,
                plan.TopologyCount);
        if (!StringComparer.Ordinal.Equals(
                LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                    plan,
                    capacity),
                LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                    resolvedPlan,
                    capacity)))
        {
            throw new ArgumentException(
                "The supplied postbuild command plan is not the exact planner-owned topology expansion.",
                nameof(plan));
        }

        ValidateProcessorWriteAuthority(
            composition,
            invocation,
            resolvedPlan,
            capacity);

        LegacyCombinerPostbuildCommandPlan reviewedPlan =
            LegacyCombinerPostbuildPlanner.CreatePlan(
                plan.Profile,
                plan.Selector);
        return new RuntimeReferenceCompilationProof(
            composition.CompilationFingerprint,
            invocation.ProcessorId,
            invocation.ToolBindingId,
            plan.Selector.Token,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                reviewedPlan,
                capacity),
            invocation,
            context.ProcessorWriteViewIds);
    }

    internal IReadOnlyList<string> ValidateAndGetSemanticBindings(
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ExternalProcessorInvocation invocation = GetSingleProcessor(composition);
        _ = StringComparer.Ordinal.Equals(
                _compilationFingerprint,
                composition.CompilationFingerprint) &&
            StringComparer.Ordinal.Equals(
                _processorId,
                invocation.ProcessorId) &&
            StringComparer.Ordinal.Equals(
                _toolBindingId,
                invocation.ToolBindingId) &&
            WriteAuthorityMatches(composition, invocation)
            ? true
            : throw new ArgumentException(
                "The runtime-reference proof is not bound to this exact compiled processor plan.",
                nameof(composition));

        return
        [
            $"postbuild-selector:{SelectorToken}",
            $"postbuild-plan:{PlanFingerprint}",
        ];
    }

    internal RuntimeReferenceCompilationProof BindCapabilityCompilation(
        CompiledComposition source,
        CompiledComposition bound)
    {
        _ = ValidateAndGetSemanticBindings(source);
        ExternalProcessorInvocation invocation = GetSingleProcessor(bound);
        _ = StringComparer.Ordinal.Equals(
                _processorId,
                invocation.ProcessorId) &&
            StringComparer.Ordinal.Equals(
                _toolBindingId,
                invocation.ToolBindingId) &&
            WriteAuthorityMatches(bound, invocation)
            ? true
            : throw new ArgumentException(
                "Capability binding changed the compiled processor plan.",
                nameof(bound));

        return new RuntimeReferenceCompilationProof(
            bound.CompilationFingerprint,
            _processorId,
            _toolBindingId,
            SelectorToken,
            PlanFingerprint,
            invocation,
            ((RuntimeReferenceReplaceV2CompilationContext)
                bound.V2Details.Provenance.Context).ProcessorWriteViewIds);
    }

    internal string SelectorToken { get; }

    internal string PlanFingerprint { get; }

    private bool WriteAuthorityMatches(
        CompiledComposition composition,
        ExternalProcessorInvocation invocation)
    {
        var context = (RuntimeReferenceReplaceV2CompilationContext)
            composition.V2Details.Provenance.Context;
        return _processorWriteViewIds.SequenceEqual(
                context.ProcessorWriteViewIds) &&
            _allowedWriteRanges.SequenceEqual(invocation.AllowedWriteRanges) &&
            _allowedWriteRangeSections.SequenceEqual(
                invocation.AllowedWriteRangeSections.Select(static section =>
                    new WriteRangeSectionIdentity(
                        section.SectionId,
                        section.Range,
                        section.SourceRange)));
    }

    private static void ValidateProcessorWriteAuthority(
        CompiledComposition composition,
        ExternalProcessorInvocation invocation,
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        ByteRange[] stagedTargetRanges =
        [
            .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan)
                .Select(static block => block.FirmwareRange)
                .Distinct()
                .OrderBy(static range => range.Start)
                .ThenBy(static range => range.Length),
        ];
        ExternalProcessorWriteRangeSection[] plannerStagedSections =
        [
            .. LegacyCombinerPostbuildPlanner
                .GetAllowedWriteRangeSectionsForStagedSources(
                    plan,
                    capacity,
                    stagedTargetRanges,
                    stagedTargetRanges),
        ];
        WriteRangeSectionIdentity[] expectedSectionIdentities =
        [
            .. plannerStagedSections
                .Where(section => invocation.AllowedWriteRanges.Any(range =>
                    range.Contains(section.Range)))
                .Select(static section => new WriteRangeSectionIdentity(
                    section.SectionId,
                    section.Range,
                    section.SourceRange)),
        ];
        WriteRangeSectionIdentity[] actualSectionIdentities =
        [
            .. invocation.AllowedWriteRangeSections.Select(static section =>
                new WriteRangeSectionIdentity(
                    section.SectionId,
                    section.Range,
                    section.SourceRange)),
        ];
        ByteRange[] expectedRanges =
        [
            .. actualSectionIdentities
                .Select(static section => section.Range)
                .Concat(GetCompleteMappingWriteRanges(composition))
                .Concat(GetCompiledPostbuildAuthorityRanges(composition))
                .Concat(GetFirmwareVersionBackupWriteRanges(composition))
                .Concat(GetProcessorWriteViewRanges(composition, invocation))
                .Distinct()
                .OrderBy(static range => range.Start)
                .ThenBy(static range => range.Length),
        ];
        if (!expectedRanges.SequenceEqual(invocation.AllowedWriteRanges) ||
            !expectedSectionIdentities.SequenceEqual(actualSectionIdentities))
        {
            throw new ArgumentException(
                "The compiled processor write ranges and sections do not match the exact planner-owned postbuild authority. " +
                $"Expected ranges [{string.Join(", ", expectedRanges)}], actual [{string.Join(", ", invocation.AllowedWriteRanges)}]; " +
                $"expected sections [{string.Join(", ", expectedSectionIdentities)}], actual [{string.Join(", ", actualSectionIdentities)}].",
                nameof(composition));
        }
    }

    private static IEnumerable<ByteRange> GetCompleteMappingWriteRanges(
        CompiledComposition composition)
    {
        var spaces =
            composition.Plan.AddressSpaces.ToDictionary(
                static space => space.AddressSpaceId,
                StringComparer.Ordinal);
        return composition.Plan.OrderedOperations
            .Where(static operation =>
                operation.Kind == CompositionOperationKind.ReplaceRange)
            .GroupBy(
                static operation => operation.SourceSpaceId!,
                StringComparer.Ordinal)
            .Where(group =>
                CoversCompleteSource(
                    group.Select(static operation =>
                        operation.SourceRange!.Value),
                    spaces[group.Key].Length))
            .SelectMany(static group =>
                group.Select(static operation => operation.TargetRange));
    }

    private static IEnumerable<ByteRange> GetCompiledPostbuildAuthorityRanges(
        CompiledComposition composition)
    {
        return composition.V2Details.Provenance.ValidationRequirements
            .OfType<CompiledFirmwareConfigBackupPlacementAuthorityValidation>()
            .Select(static requirement => requirement.AuthorityRange);
    }

    private static IEnumerable<ByteRange> GetProcessorWriteViewRanges(
        CompiledComposition composition,
        ExternalProcessorInvocation invocation)
    {
        var context = (RuntimeReferenceReplaceV2CompilationContext)
            composition.V2Details.Provenance.Context;
        var resolvedViews = composition.V2Details.RegionAccessContract.ResolvedViews
            .ToDictionary(static view => view.ViewId, StringComparer.Ordinal);
        return context.ProcessorWriteViewIds
            .Select(viewId => resolvedViews[viewId].Range)
            .Where(invocation.AllowedWriteRanges.Contains);
    }

    private static ByteRange[] GetFirmwareVersionBackupWriteRanges(
        CompiledComposition composition)
    {
        CompositionOperation[] patches =
        [
            .. composition.Plan.OrderedOperations.Where(static operation =>
                operation.Kind == CompositionOperationKind.PatchScalar),
        ];
        if (patches.Length == 0)
        {
            return [];
        }

        CompositionOperation version = patches.Single(static patch =>
            patch.TargetRange.Length == 2);
        CompositionOperation subVersion = patches.Single(static patch =>
            patch.TargetRange.Length == 1);
        var context =
            (RuntimeReferenceReplaceV2CompilationContext)
                composition.V2Details.Provenance.Context;
        FirmwareRegion sourceRegion = context.ResolvedMap.ImageMap.Regions.Single(
            region =>
                region.Owner == FirmwareRegionOwner.Tp &&
                region.Kind == FirmwareRegionKind.FirmwareConfig &&
                region.Range.Contains(version.TargetRange) &&
                region.Range.Contains(subVersion.TargetRange));
        long versionOffset = checked(
            version.TargetRange.Start - sourceRegion.Range.Start);
        long subVersionOffset = checked(
            subVersion.TargetRange.Start - sourceRegion.Range.Start);
        return
        [
            .. context.ResolvedMap.ResolvedMetadataStructures.SelectMany(
                structure =>
                {
                    ByteRange envelope = structure.LocatorOutcome.ResolvedRange.Range;
                    ByteRange backupVersion = new(
                        checked(envelope.Start + versionOffset),
                        version.TargetRange.Length);
                    ByteRange backupSubVersion = new(
                        checked(envelope.Start + subVersionOffset),
                        subVersion.TargetRange.Length);
                    return envelope.Contains(backupVersion) &&
                        envelope.Contains(backupSubVersion)
                            ? [backupVersion, backupSubVersion]
                            : Array.Empty<ByteRange>();
                }),
        ];
    }

    private static bool CoversCompleteSource(
        IEnumerable<ByteRange> ranges,
        long capacity)
    {
        long coveredEnd = 0;
        foreach (ByteRange range in ranges
                     .OrderBy(static range => range.Start)
                     .ThenBy(static range => range.Length))
        {
            if (range.Start > coveredEnd)
            {
                return false;
            }

            coveredEnd = Math.Max(coveredEnd, range.EndExclusive);
        }

        return coveredEnd == capacity;
    }

    private static ExternalProcessorInvocation GetSingleProcessor(
        CompiledComposition composition)
    {
        if (composition.V2Details.Provenance.Context is not
                RuntimeReferenceReplaceV2CompilationContext)
        {
            throw new ArgumentException(
                "A runtime-reference proof cannot bind another compiler context.",
                nameof(composition));
        }

        CompositionOperation[] processorOperations =
        [
            .. composition.Plan.OrderedOperations.Where(static operation =>
                operation.Kind ==
                    CompositionOperationKind.RunExternalProcessor),
        ];
        return processorOperations.Length == 1
            ? processorOperations[0].ExternalProcessorInvocation!
            : throw new ArgumentException(
                "A postbuild runtime-reference proof requires exactly one compiled external processor.",
                nameof(composition));
    }

    private sealed record WriteRangeSectionIdentity(
        string SectionId,
        ByteRange Range,
        ByteRange? SourceRange);
}
