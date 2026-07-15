using System.Collections.Frozen;
using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string BuiltInV2BundleTrustAnchorBindingId = "built-in-profile-bundle-v2";
    private const string BuiltInV2BundleLoadFailed = "profile.v2.builtin-bundle-load-failed";
    private const string BuiltInV2CompilationFailed = "profile.v2.builtin-compilation-failed";

    private static class BuiltInV2BundleRegistry
    {
        internal static FrozenDictionary<string, BuiltInV2Bundle> All { get; } =
            new (string Directory, string ContentHash)[]
            {
                ("nt51917-nt51927-general-merge-logical-candidate", "1025069140de5ba78296af045dc477cf8164395b68b0ce82a77970eecbe05c0e"),
                ("nt51919-nt51929-nt51932-general-merge-logical-candidate", "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283"),
                ("nt51920-general-merge-logical-candidate", "d2f87973576f54b80439f30ef1790f47df2994a6811673f0ceb8ecd5cacdbdc7"),
                ("nt51920-standard-merge", "3bb76d56656642af553ff012a619ca8fc38fb7cdabf8ac674e5433998357f9f2"),
                ("nt51923-nt51926-general-merge-logical-candidate", "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96"),
                ("nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96"),
                ("nt51926-ctrlram-replace-candidate", "83f22d939a257046a6b7357c98c34b1e953687b28545612d749a16a9323c0736"),
                ("nt51927-standard-merge", "751f44c7dd790a826e9ab17747b933542c691125bdee8b975c9c764e4f2ef4b1"),
                ("nt51928-general-merge-logical-candidate", "9cdfbe52fcf58071ab7ea9648844dc3d0dd5363e6b41db02454709bf921512a6"),
                ("nt51928-standard-merge", "27de29151abd1305a8ebf6ba25118acbf59392efd362d362699310a5564ad5af"),
                ("nt51929-standard-merge", "3c8ace0d7b0360573847d4b2c5f052313af9d2ff680cebe6288cf1611edb8f09"),
                ("nt51930-general-merge-logical-candidate", "dd94152806731536a7641b06b33ed177cc17e141032b705ed5b89956e3affc39"),
                ("nt51930-standard-merge", "b9ca3d66d8674d080b4e0c8563110dfd305b3df18746f5164e7ed45514e0714e"),
                ("nt51931-general-merge-logical-candidate", "ce3b18aede5c884b074b6f9253d45a255e82a2147ec76bd300e7548d6fdc52fe"),
                ("nt51931-standard-merge", "a7b3534afce6d2fe107363e41554668a71832f203168c81fa09e9f98a1a5815f"),
                ("nt51950-nt51951-general-merge-logical-candidate", "1da78f9a6d8aae1e7fbbda0f5977272b5c9902194ab102f2232586edd77eb121"),
                ("nt51950-nt51951-standard-merge", "65987f6b1e41feaca92e7b258bca282df9ae133f90db6877ba6b97c04d91f0f4"),
            }.ToFrozenDictionary(
                static bundle => bundle.Directory,
                static bundle => new BuiltInV2Bundle(bundle.Directory, bundle.ContentHash),
                StringComparer.Ordinal);
    }
    private static readonly ReadOnlyCollection<BuiltInV2StandardMergeRegistration> s_builtInV2StandardMergeRegistrations =
        Array.AsReadOnly<BuiltInV2StandardMergeRegistration>(
        [
            new("NT51917", "nt51917-standard-merge-gen-flash-alias", "0.5.0", Bundle("nt51927-standard-merge")),
            new("NT51919", "nt51919-standard-merge-gen-flash-alias", "0.5.0", Bundle("nt51929-standard-merge")),
            new("NT51920", "nt51920-standard-merge-gen-flash", "0.5.0", Bundle("nt51920-standard-merge")),
            new("NT51923", "nt51923-standard-merge-gen-flash", "0.5.0", Bundle("nt51923-standard-merge")),
            new("NT51926", "nt51926-standard-merge-gen-flash", "0.5.0", Bundle("nt51923-standard-merge")),
            new("NT51927", "nt51927-standard-merge-gen-flash", "0.5.0", Bundle("nt51927-standard-merge")),
            new("NT51928", "nt51928-standard-merge-gen-flash", "0.5.0", Bundle("nt51928-standard-merge")),
            new("NT51929", "nt51929-standard-merge-gen-flash", "0.5.0", Bundle("nt51929-standard-merge")),
            new("NT51930", "nt51930-standard-merge-flashmap", "0.5.0", Bundle("nt51930-standard-merge")),
            new("NT51931", "nt51931-standard-merge-gen-flash", "0.5.0", Bundle("nt51931-standard-merge")),
            new("NT51932", "nt51932-standard-merge-gen-flash", "0.5.0", Bundle("nt51929-standard-merge")),
            new("NT51950", "nt51950-standard-merge-dp-perspective", "0.5.1", Bundle("nt51950-nt51951-standard-merge")),
            new("NT51951", "nt51951-standard-merge-dp-perspective", "0.5.1", Bundle("nt51950-nt51951-standard-merge")),
        ]);
    private static readonly ReadOnlyDictionary<string, BuiltInV2StandardMergeRegistration> s_builtInV2StandardMergeByIc =
        new(
            s_builtInV2StandardMergeRegistrations.ToDictionary(
                static registration => registration.IcId,
                StringComparer.Ordinal));

    private static IReadOnlyList<BuiltInV2StandardMergeRegistration> BuiltInV2StandardMergeRegistrations =>
        s_builtInV2StandardMergeRegistrations;

    private static BuiltInV2Bundle Bundle(string directory)
    {
        return BuiltInV2BundleRegistry.All[directory];
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

    private static bool TryGetBuiltInV2StandardMergeContainerPolicy(
        string icId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out V2StandardMergeContainerPolicy? policy)
    {
        if (!s_builtInV2StandardMergeByIc.TryGetValue(icId, out BuiltInV2StandardMergeRegistration? registration))
        {
            policy = null;
            return false;
        }

        return registration.TryGetContainerPolicy(out policy);
    }

    private static string FormatStandardMergeSupportedDpLengths(string icId)
    {
        return TryGetBuiltInV2StandardMergeContainerPolicy(icId, out V2StandardMergeContainerPolicy? policy)
            ? BuiltInV2Bundle.FormatCapacities(policy.SupportedCapacities)
            : "unavailable";
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

    private sealed class BuiltInV2Bundle
    {
        private readonly Lazy<TrustedProfileBundleCatalog> _catalog;

        internal BuiltInV2Bundle(string bundleDirectory, string contentHash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
            RelativeRoot = Path.Combine("profiles", "built-in", bundleDirectory);
            ContentHash = contentHash;
            _catalog = new Lazy<TrustedProfileBundleCatalog>(LoadCatalog);
        }

        internal string RelativeRoot { get; }

        internal string ContentHash { get; }

        internal static string FormatCapacities(IEnumerable<long> capacities)
        {
            return string.Join(" / ", capacities.Select(static capacity => $"0x{capacity:X}"));
        }

        internal V2CompositionPlanCompileResult Compile(
            string profileId,
            string profileVersion,
            string icId,
            string experienceId,
            long? requestedMapCapacity)
        {
            return Compile(
                profileId,
                profileVersion,
                icId,
                experienceId,
                requestedMapCapacity,
                []);
        }

        internal V2CompositionPlanCompileResult CompileExecutable(
            string profileId,
            string profileVersion,
            string icId,
            string experienceId,
            long? requestedMapCapacity,
            string failureMessage)
        {
            V2CompositionPlanCompileResult compilation = Compile(
                profileId,
                profileVersion,
                icId,
                experienceId,
                requestedMapCapacity);
            return compilation.CompiledComposition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable }
                ? compilation
                : V2CompositionPlanCompileResult.Failed(
                compilation.Issues.Count == 0
                    ? [new CompositionIssue(BuiltInV2CompilationFailed, failureMessage)]
                    : compilation.Issues);
        }

        internal V2CompositionPlanCompileResult Compile(
            string profileId,
            string profileVersion,
            string icId,
            string experienceId,
            long? requestedMapCapacity,
            IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts)
        {
            ArgumentNullException.ThrowIfNull(resolutionArtifacts);
            try
            {
                return TrustedV2CompositionCompiler.Compile(
                    _catalog.Value,
                    profileId,
                    profileVersion,
                    icId,
                    experienceId,
                    requestedMapCapacity,
                    resolutionArtifacts);
            }
            catch (Exception exception) when (IsBundleLoadFailure(exception))
            {
                return V2CompositionPlanCompileResult.Failed([CreateBundleLoadIssue(exception)]);
            }
        }

        internal V2CompositionPlanCompileResult CompileLogicalOutput(
            string profileId,
            string profileVersion,
            string memberId,
            V2LogicalOutputCompileRequest request)
        {
            try
            {
                return TrustedV2CompositionCompiler.CompileLogicalOutput(
                    _catalog.Value,
                    profileId,
                    profileVersion,
                    memberId,
                    request);
            }
            catch (Exception exception) when (IsBundleLoadFailure(exception))
            {
                return V2CompositionPlanCompileResult.Failed([CreateBundleLoadIssue(exception)]);
            }
        }

        internal IReadOnlyList<long> GetMapCapacities(
            string profileId,
            string profileVersion,
            string icId,
            string experienceId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            try
            {
                return TrustedV2CompositionCompiler.GetMapCapacities(
                    _catalog.Value,
                    profileId,
                    profileVersion,
                    icId,
                    experienceId,
                    out issues);
            }
            catch (Exception exception) when (IsBundleLoadFailure(exception))
            {
                issues = [CreateBundleLoadIssue(exception)];
                return [];
            }
        }

        private static bool IsBundleLoadFailure(Exception exception)
        {
            return exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                ProfileBundleManifestNormalizationException or
                CompositionProfileNormalizationException or
                TrustedProfileBundleCatalogException;
        }

        private CompositionIssue CreateBundleLoadIssue(Exception exception)
        {
            return new CompositionIssue(
                BuiltInV2BundleLoadFailed,
                $"The built-in V2 bundle '{RelativeRoot}' could not be loaded: {exception.Message}");
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

        private V2CompositionPlanCompileResult LoadSummaryCompilation()
        {
            IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            return (issues.Count, capacities.Count) switch
            {
                ( > 0, _) => V2CompositionPlanCompileResult.Failed(issues),
                (_, 0) => V2CompositionPlanCompileResult.Failed(
                    [new CompositionIssue(
                        BuiltInV2CompilationFailed,
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
            return Bundle.GetMapCapacities(
                ProfileId,
                ProfileVersion,
                IcId,
                IcWorkflowIds.StandardMerge,
                out issues);
        }

    }
}

internal sealed record V2StandardMergeContainerPolicy(
    IReadOnlyList<long> SupportedCapacities,
    ByteRange TpOverlayRange,
    ByteRange CustomerInfoRange);
