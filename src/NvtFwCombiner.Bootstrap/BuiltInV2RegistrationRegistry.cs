using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2RegistrationRegistry
{
    internal static ReadOnlyCollection<BuiltInV2StandardMergeRegistration> StandardMerge { get; } =
        Array.AsReadOnly(
        [
            new BuiltInV2StandardMergeRegistration("NT51917", "nt51917-standard-merge-gen-flash-alias", "0.5.0", BuiltInV2BundleRegistry.All["nt51927-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51919", "nt51919-standard-merge-gen-flash-alias", "0.5.0", BuiltInV2BundleRegistry.All["nt51929-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51920", "nt51920-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51920-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51923", "nt51923-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51923-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51926", "nt51926-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51923-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51927", "nt51927-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51927-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51928", "nt51928-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51928-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51929", "nt51929-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51929-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51930", "nt51930-standard-merge-flashmap", "0.5.0", BuiltInV2BundleRegistry.All["nt51930-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51931", "nt51931-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51931-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51932", "nt51932-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51929-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51950", "nt51950-standard-merge-dp-perspective", "0.5.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"]),
            new BuiltInV2StandardMergeRegistration("NT51951", "nt51951-standard-merge-dp-perspective", "0.5.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"]),
        ]);

    internal static ReadOnlyDictionary<string, BuiltInV2StandardMergeRegistration> StandardMergeByIc { get; } =
        new(StandardMerge.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static Lazy<ReadOnlyDictionary<string, BuiltInV2DpReplaceRegistration>> DpReplaceByIc { get; } =
        new(CreateDpReplaceRegistrations);

    internal static ReadOnlyDictionary<string, GeneralMergeV2CandidateRegistration> GeneralMergeByIc { get; } = new(
        new GeneralMergeV2CandidateRegistration[]
        {
            new("NT51917", "nt51927", "nt51917-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51917-nt51927-general-merge-logical-candidate"]),
            new("NT51919", "nt51929-nt51932", "nt51919-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51919-nt51929-nt51932-general-merge-logical-candidate"]),
            new("NT51920", "nt51920", "nt51920-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51920-general-merge-logical-candidate"]),
            new("NT51923", "nt51923-nt51926", "nt51923-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51923-nt51926-general-merge-logical-candidate"]),
            new("NT51926", "nt51923-nt51926", "nt51926-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51923-nt51926-general-merge-logical-candidate"]),
            new("NT51927", "nt51927", "nt51927-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51917-nt51927-general-merge-logical-candidate"]),
            new("NT51928", "nt51928", "nt51928-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51928-general-merge-logical-candidate"]),
            new("NT51929", "nt51929-nt51932", "nt51929-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51919-nt51929-nt51932-general-merge-logical-candidate"]),
            new("NT51930", "nt51930", "nt51930-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51930-general-merge-logical-candidate"]),
            new("NT51931", "nt51931", "nt51931-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51931-general-merge-logical-candidate"]),
            new("NT51932", "nt51929-nt51932", "nt51932-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51919-nt51929-nt51932-general-merge-logical-candidate"]),
            new("NT51950", "nt51950-nt51951-dp-perspective", "nt51950-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51950-nt51951-general-merge-logical-candidate"]),
            new("NT51951", "nt51950-nt51951-dp-perspective", "nt51951-general-merge-logical-candidate", BuiltInV2BundleRegistry.All["nt51950-nt51951-general-merge-logical-candidate"]),
        }.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    private static ReadOnlyDictionary<string, BuiltInV2DpReplaceRegistration> CreateDpReplaceRegistrations()
    {
        return new ReadOnlyDictionary<string, BuiltInV2DpReplaceRegistration>(
            new BuiltInV2DpReplaceRegistration[]
            {
                new("NT51950", "nt51950-dp-replace-dp-perspective", "0.6.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"]),
                new("NT51951", "nt51951-dp-replace-dp-perspective", "0.6.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"]),
            }.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));
    }
}

internal sealed class BuiltInV2StandardMergeRegistration
{
    private readonly Lazy<V2CompositionPlanCompileResult> _summaryCompilation;

    internal BuiltInV2StandardMergeRegistration(
        string icId,
        string profileId,
        string profileVersion,
        BuiltInV2Bundle bundle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(bundle);
        IcId = icId;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        Bundle = bundle;
        _summaryCompilation = new Lazy<V2CompositionPlanCompileResult>(LoadSummaryCompilation);
    }

    internal string IcId { get; }

    internal string ProfileId { get; }

    internal string ProfileVersion { get; }

    internal BuiltInV2Bundle Bundle { get; }

    internal bool HasMultipleMapCapacities
    {
        get
        {
            IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            return issues.Count == 0 && capacities.Count > 1;
        }
    }

    internal bool TryGetContainerPolicy(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out V2StandardMergeContainerPolicy? policy)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition? composition = _summaryCompilation.Value.CompiledComposition;
        if (issues.Count != 0 || capacities.Count <= 1 || composition?.V2Details is not { } details)
        {
            policy = null;
            return false;
        }

        FirmwareImageMap map = details.Provenance.ResolvedMap.ImageMap;
        FirmwareRegion? tpOverlay = map.Regions.SingleOrDefault(static region => region.RegionId == "tp-overlay");
        FirmwareRegion? customerInfo = map.Regions.SingleOrDefault(static region => region.RegionId == "customer-info");
        if (tpOverlay is null || customerInfo is null)
        {
            policy = null;
            return false;
        }

        policy = new V2StandardMergeContainerPolicy(
            capacities,
            tpOverlay.Range,
            customerInfo.Range);
        return true;
    }

    internal void TryCompile(
        long? dpInputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (issues.Count != 0)
        {
            composition = null;
            return;
        }

        long? requestedMapCapacity = null;
        if (capacities.Count > 1)
        {
            if (dpInputLength is null)
            {
                composition = null;
                issues = [];
                return;
            }

            if (!capacities.Contains(dpInputLength.Value))
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.StandardMergeDpLengthUnsupported,
                        $"Selected DP BIN length 0x{dpInputLength.Value:X} is unsupported; {IcId} Standard Merge accepts DP input lengths {BuiltInV2Bundle.FormatCapacities(capacities)}."),
                ];
                return;
            }

            requestedMapCapacity = dpInputLength.Value;
        }

        V2CompositionPlanCompileResult compilation = Bundle.CompileExecutable(
            ProfileId,
            ProfileVersion,
            IcId,
            IcWorkflowIds.StandardMerge,
            requestedMapCapacity,
            $"The built-in V2 profile for {IcId} did not produce an executable composition.");
        composition = compilation.CompiledComposition;
        issues = compilation.Issues;
    }

    internal bool TryGetAuthoringDefaultCapacity(
        out long capacity,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (issues.Count != 0 || capacities.Count <= 1)
        {
            capacity = 0;
            return false;
        }

        capacity = capacities[^1];
        return true;
    }

    internal WorkbenchProfileSummary CreateProfileSummary()
    {
        V2CompositionPlanCompileResult compilation = _summaryCompilation.Value;
        return compilation.CompiledComposition is { } composition
            ? WorkbenchCompositionService.CreateProfileSummary(composition)
            : new WorkbenchProfileSummary(
                ProfileId,
                IcId,
                CompositionKind.Merge,
                [],
                WorkbenchCompositionService.StandardMergeFallbackOutputFileName,
                null,
                CompileSucceeded: false,
                Array.AsReadOnly(compilation.Issues.Select(static issue => issue.Code).ToArray()));
    }

    private V2CompositionPlanCompileResult LoadSummaryCompilation()
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        return (issues.Count, capacities.Count) switch
        {
            ( > 0, _) => V2CompositionPlanCompileResult.Failed(issues),
            (_, 0) => V2CompositionPlanCompileResult.Failed(
                [new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 profile for {IcId} has no declared map capacities.")]),
            _ => Bundle.CompileExecutable(
                ProfileId,
                ProfileVersion,
                IcId,
                IcWorkflowIds.StandardMerge,
                capacities.Count > 1 ? capacities[0] : null,
                $"The built-in V2 profile for {IcId} did not produce an executable composition."),
        };
    }

    private IReadOnlyList<long> GetMapCapacities(out IReadOnlyList<CompositionIssue> issues)
    {
        return Bundle.GetMapCapacities(
            ProfileId,
            ProfileVersion,
            IcId,
            IcWorkflowIds.StandardMerge,
            out issues);
    }
}

internal sealed class BuiltInV2DpReplaceRegistration
{
    private readonly Lazy<V2CompositionPlanCompileResult> _summaryCompilation;

    internal BuiltInV2DpReplaceRegistration(
        string icId,
        string profileId,
        string profileVersion,
        BuiltInV2Bundle bundle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(bundle);
        IcId = icId;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        Bundle = bundle;
        _summaryCompilation = new(CompileSummary);
    }

    internal string IcId { get; }

    private string ProfileId { get; }

    private string ProfileVersion { get; }

    private BuiltInV2Bundle Bundle { get; }

    internal bool MatchesSelector(string selector)
    {
        return string.Equals(ProfileId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(IcId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(IcId), selector, StringComparison.OrdinalIgnoreCase);
    }

    internal IReadOnlyList<long> GetMapCapacities(out IReadOnlyList<CompositionIssue> issues)
    {
        return Bundle.GetMapCapacities(
            ProfileId,
            ProfileVersion,
            IcId,
            IcWorkflowIds.DpReplace,
            out issues);
    }

    internal void TryCompile(
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (issues.Count != 0)
        {
            composition = null;
            return;
        }

        if (!capacities.Contains(baseCapacity))
        {
            composition = null;
            issues =
            [
                new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"{IcId} DP Replace base flash BIN length must be one of {BuiltInV2Bundle.FormatCapacities(capacities)} (actual 0x{baseCapacity:X})."),
            ];
            return;
        }

        V2CompositionPlanCompileResult compilation = Bundle.CompileExecutable(
            ProfileId,
            ProfileVersion,
            IcId,
            IcWorkflowIds.DpReplace,
            baseCapacity,
            $"The built-in V2 DP Replace profile for {IcId} did not produce an executable composition.");
        composition = compilation.CompiledComposition;
        issues = compilation.Issues;
    }

    internal WorkbenchProfileSummary CreateProfileSummary()
    {
        V2CompositionPlanCompileResult compilation = _summaryCompilation.Value;
        return compilation.CompiledComposition is { } composition
            ? WorkbenchCompositionService.CreateProfileSummary(composition)
            : new WorkbenchProfileSummary(
                ProfileId,
                IcId,
                CompositionKind.Replace,
                [],
                $"nt{IcId[2..].ToLowerInvariant()}-dp-replace.bin",
                CompiledIcNumberPolicy.SingleSelector,
                CompileSucceeded: false,
                Array.AsReadOnly(compilation.Issues.Select(static issue => issue.Code).ToArray()));
    }

    private V2CompositionPlanCompileResult CompileSummary()
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> capacityIssues);
        return (capacityIssues.Count, capacities.Count) switch
        {
            ( > 0, _) => V2CompositionPlanCompileResult.Failed(capacityIssues),
            (_, 0) => V2CompositionPlanCompileResult.Failed(
                [new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 DP Replace profile for {IcId} has no declared base capacities.")]),
            _ => Bundle.CompileExecutable(
                ProfileId,
                ProfileVersion,
                IcId,
                IcWorkflowIds.DpReplace,
                capacities[0],
                $"The built-in V2 DP Replace profile for {IcId} did not produce an executable composition."),
        };
    }
}

internal sealed record GeneralMergeV2CandidateRegistration(
    string IcId,
    string FamilyId,
    string ProfileId,
    BuiltInV2Bundle Bundle);
