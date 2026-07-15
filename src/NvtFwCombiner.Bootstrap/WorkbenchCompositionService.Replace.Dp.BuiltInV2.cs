using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly Lazy<ReadOnlyDictionary<string, BuiltInV2DpReplaceRegistration>> s_builtInV2DpReplaceByIc =
        new(CreateBuiltInV2DpReplaceRegistrations);

    /// <summary>Compiles one supported DP Perspective Replace profile from its hash-anchored V2 bundle.</summary>
    internal static bool TryCompileDpPerspectiveDpReplace(
        string icId,
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!s_builtInV2DpReplaceByIc.Value.TryGetValue(icId, out BuiltInV2DpReplaceRegistration? registration))
        {
            composition = null;
            issues = [];
            return false;
        }

        registration.TryCompile(baseCapacity, out composition, out issues);
        return true;
    }

    /// <summary>Resolves the V2 DP Replace facts needed by the workbench display without consulting legacy maps.</summary>
    internal static bool TryResolveDpPerspectiveDpReplaceDisplay(
        string icId,
        long? baseCapacity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DpPerspectiveDpReplaceDisplay? display)
    {
        if (!s_builtInV2DpReplaceByIc.Value.TryGetValue(icId, out BuiltInV2DpReplaceRegistration? registration))
        {
            display = null;
            return false;
        }

        IReadOnlyList<long> capacities = registration.GetMapCapacities(out IReadOnlyList<CompositionIssue> capacityIssues);
        if (capacityIssues.Count != 0 || baseCapacity is null)
        {
            display = new DpPerspectiveDpReplaceDisplay(baseCapacity, capacities, Composition: null, capacityIssues);
            return true;
        }

        registration.TryCompile(baseCapacity.Value, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        display = new DpPerspectiveDpReplaceDisplay(baseCapacity, capacities, composition, issues);
        return true;
    }

    /// <summary>Resolves a DP Replace CLI selector from the supported V2 profile registrations.</summary>
    internal static bool TryResolveDpPerspectiveDpReplaceSelector(
        string selector,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        foreach (BuiltInV2DpReplaceRegistration registration in s_builtInV2DpReplaceByIc.Value.Values)
        {
            if (registration.MatchesSelector(normalized))
            {
                icId = registration.IcId;
                return true;
            }
        }

        icId = null;
        return false;
    }

    internal static string FormatBuiltInV2DpReplaceIcIds()
    {
        return string.Join(
            "/",
            s_builtInV2DpReplaceByIc.Value.Keys.Order(StringComparer.Ordinal));
    }

    private static bool IsBuiltInV2DpReplaceIc(string icId)
    {
        if (string.IsNullOrWhiteSpace(icId))
        {
            return false;
        }

        string trimmed = icId.Trim();
        string normalized = trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
        return s_builtInV2DpReplaceByIc.Value.ContainsKey(normalized);
    }

    private static ReadOnlyDictionary<string, BuiltInV2DpReplaceRegistration> CreateBuiltInV2DpReplaceRegistrations()
    {
        return new ReadOnlyDictionary<string, BuiltInV2DpReplaceRegistration>(
            new Dictionary<string, BuiltInV2DpReplaceRegistration>(StringComparer.Ordinal)
            {
                ["NT51950"] = new(
                    "NT51950",
                    "nt51950-dp-replace-dp-perspective",
                    "0.6.1",
                    s_nt51950Nt51951V2Bundle),
                ["NT51951"] = new(
                    "NT51951",
                    "nt51951-dp-replace-dp-perspective",
                    "0.6.1",
                    s_nt51950Nt51951V2Bundle),
            });
    }

    private sealed class BuiltInV2DpReplaceRegistration
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

            V2CompositionPlanCompileResult compilation = Bundle.Compile(
                ProfileId,
                ProfileVersion,
                IcId,
                IcWorkflowIds.DpReplace,
                baseCapacity);
            composition = compilation.CompiledComposition;
            issues = compilation.Issues;
            if (composition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable })
            {
                return;
            }

            composition = null;
            if (issues.Count == 0)
            {
                issues =
                [
                    new CompositionIssue(
                        BuiltInV2CompilationFailed,
                        $"The built-in V2 DP Replace profile for {IcId} did not produce an executable composition."),
                ];
            }
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
            if (capacityIssues.Count != 0)
            {
                return V2CompositionPlanCompileResult.Failed(capacityIssues);
            }

            if (capacities.Count == 0)
            {
                return V2CompositionPlanCompileResult.Failed(
                    [new CompositionIssue(
                        BuiltInV2CompilationFailed,
                        $"The built-in V2 DP Replace profile for {IcId} has no declared base capacities.")]);
            }

            V2CompositionPlanCompileResult[] compilations =
            [
                .. capacities.Select(capacity => Bundle.Compile(
                    ProfileId,
                    ProfileVersion,
                    IcId,
                    IcWorkflowIds.DpReplace,
                    capacity)),
            ];
            V2CompositionPlanCompileResult? failure = compilations.FirstOrDefault(compilation =>
                compilation.CompiledComposition is not { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable });
            if (failure is not null)
            {
                return failure.Issues.Count == 0
                    ? V2CompositionPlanCompileResult.Failed(
                        [new CompositionIssue(
                            BuiltInV2CompilationFailed,
                            $"The built-in V2 DP Replace profile for {IcId} did not produce an executable composition.")])
                    : V2CompositionPlanCompileResult.Failed(failure.Issues);
            }

            CompiledComposition first = compilations[0].CompiledComposition!;
            return compilations.Skip(1).Any(compilation =>
                    compilation.CompiledComposition is not { } candidate ||
                    candidate.ProfileId != first.ProfileId ||
                    candidate.CompositionKind != first.CompositionKind ||
                    candidate.DefaultOutputFileName != first.DefaultOutputFileName ||
                    candidate.IcNumberPolicy != first.IcNumberPolicy ||
                    !candidate.Plan.RequiredInputAddressSpaceIds.SequenceEqual(
                        first.Plan.RequiredInputAddressSpaceIds,
                        StringComparer.Ordinal))
                ? V2CompositionPlanCompileResult.Failed(
                    [new CompositionIssue(
                        BuiltInV2CompilationFailed,
                        $"The capacity variants for built-in V2 DP Replace profile {IcId} do not share one stable workbench summary.")])
                : compilations[0];
        }
    }
}
