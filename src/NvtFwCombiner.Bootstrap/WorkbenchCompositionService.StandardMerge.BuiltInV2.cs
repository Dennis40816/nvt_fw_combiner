using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string BuiltInV2BundleTrustAnchorBindingId = "built-in-profile-bundle-v2";
    private const string BuiltInV2BundleLoadFailed = "profile.v2.builtin-bundle-load-failed";
    private const string BuiltInV2CompilationFailed = "profile.v2.builtin-compilation-failed";

    private static readonly BuiltInV2StandardMergeBundle s_nt51920V2Bundle = new(
        "profiles\\built-in\\nt51920-standard-merge",
        "c58c9b68678bd314fa82c5563602001b6fa55d7176142c07067ef08f1b8d720a");
    private static readonly BuiltInV2StandardMergeBundle s_nt51929FamilyV2Bundle = new(
        "profiles\\built-in\\nt51929-standard-merge",
        "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8");
    private static readonly BuiltInV2StandardMergeBundle s_nt51923FamilyV2Bundle = new(
        "profiles\\built-in\\nt51923-standard-merge",
        "56bc8a3d68b0015461bc903fa1a17fdb172715b61e1fa879506ddcc3a71c9038");
    private static readonly BuiltInV2StandardMergeBundle s_nt51930V2Bundle = new(
        "profiles\\built-in\\nt51930-standard-merge",
        "3803b473fd0f133d33c66299199f6202a72e1c83eb8c9e6e910f191d1fadd00d");
    private static readonly BuiltInV2StandardMergeBundle s_nt51931V2Bundle = new(
        "profiles\\built-in\\nt51931-standard-merge",
        "94c36258a6d981a5fa7133811d38bae175b1ff82b67a2df3abcaf090e03ec0d4");
    private static readonly BuiltInV2StandardMergeBundle s_nt51927V2Bundle = new(
        "profiles\\built-in\\nt51927-standard-merge",
        "67a314a3763b81e348960bafb5e743e5fc1df553d8590544a6d8d52706038afe");
    private static readonly BuiltInV2StandardMergeBundle s_nt51928V2Bundle = new(
        "profiles\\built-in\\nt51928-standard-merge",
        "961224d53b236e851039d65765654674ff65ba75a7cedc7ee9e5d6c9a6165bb5");
    private static readonly BuiltInV2StandardMergeBundle s_nt51950Nt51951V2Bundle = new(
        "profiles\\built-in\\nt51950-nt51951-standard-merge",
        "a51258be9024c8366821bee9610f7c7326bce9e9ea046747da7361c72a75c76b");
    private static readonly ReadOnlyCollection<BuiltInV2StandardMergeRegistration> s_builtInV2StandardMergeRegistrations =
        Array.AsReadOnly(
        [
            new BuiltInV2StandardMergeRegistration(
                "NT51917",
                "nt51917-standard-merge-gen-flash-alias",
                "0.5.0",
                s_nt51927V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51919",
                "nt51919-standard-merge-gen-flash-alias",
                "0.5.0",
                s_nt51929FamilyV2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51920",
                "nt51920-standard-merge-gen-flash",
                "0.5.0",
                s_nt51920V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51923",
                "nt51923-standard-merge-gen-flash",
                "0.5.0",
                s_nt51923FamilyV2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51926",
                "nt51926-standard-merge-gen-flash",
                "0.5.0",
                s_nt51923FamilyV2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51927",
                "nt51927-standard-merge-gen-flash",
                "0.5.0",
                s_nt51927V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51928",
                "nt51928-standard-merge-gen-flash",
                "0.5.0",
                s_nt51928V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51929",
                "nt51929-standard-merge-gen-flash",
                "0.5.0",
                s_nt51929FamilyV2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51930",
                "nt51930-standard-merge-flashmap",
                "0.5.0",
                s_nt51930V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51931",
                "nt51931-standard-merge-gen-flash",
                "0.5.0",
                s_nt51931V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51932",
                "nt51932-standard-merge-gen-flash",
                "0.5.0",
                s_nt51929FamilyV2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51950",
                "nt51950-standard-merge-dp-perspective",
                "0.5.1",
                s_nt51950Nt51951V2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51951",
                "nt51951-standard-merge-dp-perspective",
                "0.5.1",
                s_nt51950Nt51951V2Bundle),
        ]);
    private static readonly ReadOnlyDictionary<string, BuiltInV2StandardMergeRegistration> s_builtInV2StandardMergeByIc =
        new(
            s_builtInV2StandardMergeRegistrations.ToDictionary(
                static registration => registration.IcId,
                StringComparer.Ordinal));

    private static IReadOnlyList<BuiltInV2StandardMergeRegistration> BuiltInV2StandardMergeRegistrations =>
        s_builtInV2StandardMergeRegistrations;

    private static bool IsBuiltInV2StandardMerge(string icId)
    {
        return s_builtInV2StandardMergeByIc.ContainsKey(icId);
    }

    private static bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!s_builtInV2StandardMergeByIc.TryGetValue(icId, out BuiltInV2StandardMergeRegistration? registration))
        {
            composition = null;
            issues = [];
            return false;
        }

        registration.TryCompile(dpInputLength, out composition, out issues);
        return true;
    }

    private static bool IsBuiltInV2StandardMergeMapCapacityPending(string icId)
    {
        return s_builtInV2StandardMergeByIc.TryGetValue(icId, out BuiltInV2StandardMergeRegistration? registration) &&
            registration.HasMultipleMapCapacities;
    }

    private static bool TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
        string icId,
        out long capacity,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!s_builtInV2StandardMergeByIc.TryGetValue(icId, out BuiltInV2StandardMergeRegistration? registration))
        {
            capacity = 0;
            issues = [];
            return false;
        }

        return registration.TryGetAuthoringDefaultCapacity(out capacity, out issues);
    }

    private sealed class BuiltInV2StandardMergeBundle
    {
        private readonly Lazy<TrustedProfileBundleCatalog> _catalog;

        internal BuiltInV2StandardMergeBundle(string relativeRoot, string contentHash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativeRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
            RelativeRoot = relativeRoot;
            ContentHash = contentHash;
            _catalog = new Lazy<TrustedProfileBundleCatalog>(LoadCatalog);
        }

        internal string RelativeRoot { get; }

        internal string ContentHash { get; }

        internal V2CompositionPlanCompileResult Compile(string profileId, string profileVersion, string icId)
        {
            return Compile(profileId, profileVersion, icId, requestedMapCapacity: null);
        }

        internal V2CompositionPlanCompileResult Compile(
            string profileId,
            string profileVersion,
            string icId,
            long? requestedMapCapacity)
        {
            try
            {
                return TrustedV2CompositionCompiler.Compile(
                    _catalog.Value,
                    profileId,
                    profileVersion,
                    icId,
                    IcWorkflowIds.StandardMerge,
                    requestedMapCapacity);
            }
            catch (Exception exception) when (exception is IOException or
                                             UnauthorizedAccessException or
                                             InvalidDataException or
                                             ProfileBundleManifestNormalizationException or
                                             CompositionProfileNormalizationException or
                                             TrustedProfileBundleCatalogException)
            {
                var issue = new CompositionIssue(
                    BuiltInV2BundleLoadFailed,
                    $"The built-in V2 bundle '{RelativeRoot}' could not be loaded: {exception.Message}");
                return V2CompositionPlanCompileResult.Failed([issue]);
            }
        }

        internal IReadOnlyList<long> GetMapCapacities(
            string profileId,
            string profileVersion,
            string icId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            try
            {
                return TrustedV2CompositionCompiler.GetMapCapacities(
                    _catalog.Value,
                    profileId,
                    profileVersion,
                    icId,
                    IcWorkflowIds.StandardMerge,
                    out issues);
            }
            catch (Exception exception) when (exception is IOException or
                                             UnauthorizedAccessException or
                                             InvalidDataException or
                                             ProfileBundleManifestNormalizationException or
                                             CompositionProfileNormalizationException or
                                             TrustedProfileBundleCatalogException)
            {
                issues =
                [
                    new CompositionIssue(
                        BuiltInV2BundleLoadFailed,
                        $"The built-in V2 bundle '{RelativeRoot}' could not be loaded: {exception.Message}"),
                ];
                return [];
            }
        }

        private TrustedProfileBundleCatalog LoadCatalog()
        {
            string bundleRoot = Path.Combine(AppContext.BaseDirectory, RelativeRoot);
            TrustedProfileBundle bundle = ProfileBundleLoader.Load(
                bundleRoot,
                "profile-bundle.json",
                new ProfileBundleTrustAnchor(ContentHash, BuiltInV2BundleTrustAnchorBindingId),
                new ProfileBundleLoadLimits(
                    maximumManifestBytes: 16384,
                    maximumJsonDepth: 32,
                    new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
            return TrustedProfileBundleCatalogProjection.Create(bundle.CreateDocumentProjection());
        }
    }

    private sealed class BuiltInV2StandardMergeRegistration
    {
        private readonly Lazy<V2CompositionPlanCompileResult> _summaryCompilation;

        internal BuiltInV2StandardMergeRegistration(
            string icId,
            string profileId,
            string profileVersion,
            BuiltInV2StandardMergeBundle bundle)
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

        internal BuiltInV2StandardMergeBundle Bundle { get; }

        internal bool HasMultipleMapCapacities
        {
            get
            {
                IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
                return issues.Count == 0 && capacities.Count > 1;
            }
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
                            $"Selected DP BIN length 0x{dpInputLength.Value:X} is unsupported; {IcId} Standard Merge accepts DP input lengths {FormatCapacities(capacities)}."),
                    ];
                    return;
                }

                requestedMapCapacity = dpInputLength.Value;
            }

            V2CompositionPlanCompileResult compilation = Bundle.Compile(
                ProfileId,
                ProfileVersion,
                IcId,
                requestedMapCapacity);
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
                        $"The built-in V2 profile for {IcId} did not produce an executable composition."),
                ];
            }
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

        private V2CompositionPlanCompileResult LoadSummaryCompilation()
        {
            IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0)
            {
                return V2CompositionPlanCompileResult.Failed(issues);
            }

            V2CompositionPlanCompileResult[] compilations =
            [
                .. capacities.Select(capacity => Bundle.Compile(
                    ProfileId,
                    ProfileVersion,
                    IcId,
                    capacities.Count > 1 ? capacity : null)),
            ];
            V2CompositionPlanCompileResult? failure = compilations.FirstOrDefault(compilation =>
                compilation.CompiledComposition is not { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable });
            if (failure is not null)
            {
                return failure.Issues.Count == 0
                    ? V2CompositionPlanCompileResult.Failed(
                        [new CompositionIssue(
                            BuiltInV2CompilationFailed,
                            $"The built-in V2 profile for {IcId} did not produce an executable composition.")])
                    : V2CompositionPlanCompileResult.Failed(failure.Issues);
            }

            CompiledComposition first = compilations[0].CompiledComposition!;
            return compilations.Skip(1).Any(compilation =>
                    compilation.CompiledComposition is not { } candidate ||
                    candidate.ProfileId != first.ProfileId ||
                    candidate.CompositionKind != first.CompositionKind ||
                    candidate.DefaultOutputFileName != first.DefaultOutputFileName ||
                    !candidate.Plan.RequiredInputAddressSpaceIds.SequenceEqual(
                        first.Plan.RequiredInputAddressSpaceIds,
                        StringComparer.Ordinal))
                ? V2CompositionPlanCompileResult.Failed(
                    [new CompositionIssue(
                        BuiltInV2CompilationFailed,
                        $"The capacity variants for built-in V2 profile {IcId} do not share one stable workbench summary.")])
                : compilations[0];
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
                    StandardMergeFallbackOutputFileName,
                    null,
                    CompileSucceeded: false,
                    Array.AsReadOnly(compilation.Issues.Select(static issue => issue.Code).ToArray()));
        }

        private IReadOnlyList<long> GetMapCapacities(out IReadOnlyList<CompositionIssue> issues)
        {
            return Bundle.GetMapCapacities(ProfileId, ProfileVersion, IcId, out issues);
        }

        private static string FormatCapacities(IEnumerable<long> capacities)
        {
            return string.Join(" / ", capacities.Select(static capacity => $"0x{capacity:X}"));
        }
    }
}
