using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2RegistrationRegistry
{
    internal static ReadOnlyCollection<BuiltInV2Registration> StandardMerge { get; } =
        Array.AsReadOnly(
        [
            new BuiltInV2Registration("NT51917", "nt51917-standard-merge-gen-flash-alias", "0.5.0", BuiltInV2BundleRegistry.All["nt51927-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51919", "nt51919-standard-merge-gen-flash-alias", "0.5.0", BuiltInV2BundleRegistry.All["nt51929-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51920", "nt51920-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51920-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51923", "nt51923-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51923-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51926", "nt51926-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51923-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51927", "nt51927-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51927-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51928", "nt51928-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51928-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51929", "nt51929-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51929-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51930", "nt51930-standard-merge-flashmap", "0.5.1", BuiltInV2BundleRegistry.All["nt51930-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51931", "nt51931-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51931-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51932", "nt51932-standard-merge-gen-flash", "0.5.0", BuiltInV2BundleRegistry.All["nt51929-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51950", "nt51950-standard-merge-dp-perspective", "0.5.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"], CompositionKind.Merge),
            new BuiltInV2Registration("NT51951", "nt51951-standard-merge-dp-perspective", "0.5.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"], CompositionKind.Merge),
        ]);

    internal static ReadOnlyDictionary<string, BuiltInV2Registration> StandardMergeByIc { get; } =
        new(StandardMerge.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static Lazy<ReadOnlyDictionary<string, BuiltInV2Registration>> DpReplaceByIc { get; } =
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

    private static ReadOnlyDictionary<string, BuiltInV2Registration> CreateDpReplaceRegistrations()
    {
        return new ReadOnlyDictionary<string, BuiltInV2Registration>(
            new BuiltInV2Registration[]
            {
                new("NT51930", "nt51930-dp-replace-flashmap", "0.1.0", BuiltInV2BundleRegistry.All["nt51930-standard-merge"], CompositionKind.Replace),
                new("NT51950", "nt51950-dp-replace-dp-perspective", "0.6.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"], CompositionKind.Replace),
                new("NT51951", "nt51951-dp-replace-dp-perspective", "0.6.1", BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"], CompositionKind.Replace),
            }.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));
    }
}

internal sealed class BuiltInV2Registration
{
    private readonly Lazy<V2CompositionPlanCompileResult> _summaryCompilation;
    private readonly BuiltInV2Bundle _bundle;
    private readonly string _profileVersion;

    internal BuiltInV2Registration(
        string icId,
        string profileId,
        string profileVersion,
        BuiltInV2Bundle bundle,
        CompositionKind compositionKind)
    {
        if (compositionKind is not (CompositionKind.Merge or CompositionKind.Replace))
        {
            throw new ArgumentOutOfRangeException(nameof(compositionKind));
        }

        IcId = icId;
        ProfileId = profileId;
        _profileVersion = profileVersion;
        _bundle = bundle;
        CompositionKind = compositionKind;
        _summaryCompilation = new(CompileSummary);
    }

    internal string IcId { get; }

    internal string ProfileId { get; }

    private CompositionKind CompositionKind { get; }

    private bool IsStandardMerge => CompositionKind == CompositionKind.Merge;

    private string WorkflowId => IsStandardMerge ? IcWorkflowIds.StandardMerge : IcWorkflowIds.DpReplace;

    private string ProfileLabel => IsStandardMerge ? "profile" : "DP Replace profile";

    internal bool HasMultipleMapCapacities
    {
        get
        {
            IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            return issues.Count == 0 && capacities.Count > 1;
        }
    }

    internal bool MatchesSelector(string selector)
    {
        return string.Equals(ProfileId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(IcId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(IcId), selector, StringComparison.OrdinalIgnoreCase);
    }

    internal IReadOnlyList<long> GetMapCapacities(out IReadOnlyList<CompositionIssue> issues)
    {
        return _bundle.GetMapCapacities(ProfileId, _profileVersion, IcId, WorkflowId, out issues);
    }

    internal bool TryGetContainerPolicy(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out V2StandardMergeContainerPolicy? policy)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition? composition = _summaryCompilation.Value.CompiledComposition;
        FirmwareImageMap? map = composition?.V2Details?.Provenance.ResolvedMap.ImageMap;
        FirmwareRegion? tpOverlay = map?.Regions.SingleOrDefault(static region => region.RegionId == "tp-overlay");
        FirmwareRegion? customerInfo = map?.Regions.SingleOrDefault(static region => region.RegionId == "customer-info");
        if (!IsStandardMerge || issues.Count != 0 || capacities.Count <= 1 || tpOverlay is null || customerInfo is null)
        {
            policy = null;
            return false;
        }

        policy = new V2StandardMergeContainerPolicy(capacities, tpOverlay.Range, customerInfo.Range);
        return true;
    }

    internal void TryCompile(
        long? inputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (issues.Count != 0)
        {
            composition = null;
            return;
        }

        long? requestedCapacity = null;
        if (IsStandardMerge && capacities.Count > 1)
        {
            if (inputLength is null)
            {
                composition = null;
                issues = [];
                return;
            }

            if (!capacities.Contains(inputLength.Value))
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.StandardMergeDpLengthUnsupported,
                        $"Selected DP BIN length 0x{inputLength.Value:X} is unsupported; {IcId} Standard Merge accepts DP input lengths {BuiltInV2Bundle.FormatCapacities(capacities)}."),
                ];
                return;
            }

            requestedCapacity = inputLength;
        }
        else if (!IsStandardMerge)
        {
            if (inputLength is null || !capacities.Contains(inputLength.Value))
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"{IcId} DP Replace base flash BIN length must be one of {BuiltInV2Bundle.FormatCapacities(capacities)} (actual 0x{inputLength.GetValueOrDefault():X})."),
                ];
                return;
            }

            requestedCapacity = inputLength;
        }

        V2CompositionPlanCompileResult compilation = CompileExecutable(requestedCapacity);
        composition = compilation.CompiledComposition;
        issues = compilation.Issues;
    }

    internal bool TryGetAuthoringDefaultCapacity(
        out long capacity,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (!IsStandardMerge || issues.Count != 0 || capacities.Count <= 1)
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
                CompositionKind,
                [],
                IsStandardMerge
                    ? WorkbenchCompositionService.StandardMergeFallbackOutputFileName
                    : $"nt{IcId[2..].ToLowerInvariant()}-dp-replace.bin",
                IsStandardMerge ? null : CompiledIcNumberPolicy.SingleSelector,
                CompileSucceeded: false,
                Array.AsReadOnly(compilation.Issues.Select(static issue => issue.Code).ToArray()));
    }

    private V2CompositionPlanCompileResult CompileSummary()
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        return (issues.Count, capacities.Count) switch
        {
            ( > 0, _) => V2CompositionPlanCompileResult.Failed(issues),
            (_, 0) => V2CompositionPlanCompileResult.Failed(
                [new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 {ProfileLabel} for {IcId} has no declared {(IsStandardMerge ? "map" : "base")} capacities.")]),
            _ => CompileExecutable(!IsStandardMerge || capacities.Count > 1 ? capacities[0] : null),
        };
    }

    private V2CompositionPlanCompileResult CompileExecutable(long? requestedMapCapacity)
    {
        return _bundle.CompileExecutable(
            ProfileId,
            _profileVersion,
            IcId,
            WorkflowId,
            requestedMapCapacity,
            $"The built-in V2 {ProfileLabel} for {IcId} did not produce an executable composition.");
    }
}

internal sealed record GeneralMergeV2CandidateRegistration(
    string IcId,
    string FamilyId,
    string ProfileId,
    BuiltInV2Bundle Bundle);
