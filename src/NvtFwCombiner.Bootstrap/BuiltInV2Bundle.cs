using System.Collections.Frozen;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2BundleRegistry
{
    internal static FrozenDictionary<string, BuiltInV2Bundle> All { get; } =
        new (string Directory, string ContentHash)[]
        {
            ("nt51917-nt51927-general-merge-logical-candidate", "1025069140de5ba78296af045dc477cf8164395b68b0ce82a77970eecbe05c0e"),
            ("nt51917-ctrlram-replace-alias-candidate", "8992dbc5483054c5dc16e545444b1f94446c698c68b1abe7946efdb4d4ffb26b"),
            ("nt51919-nt51929-nt51932-ab-merge", "93902043b6e4ea4c8a2023a7f02c798e4497de3523b21115797e9b302ce22292"),
            ("nt51919-nt51929-nt51932-general-merge-logical-candidate", "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283"),
            ("nt51920-ctrlram-replace-candidate", "7394b8c650200fd6bb608312f1bf5177c5f1edf21cf9d485c7d5d5406d8a1b06"),
            ("nt51920-general-merge-logical-candidate", "d2f87973576f54b80439f30ef1790f47df2994a6811673f0ceb8ecd5cacdbdc7"),
            ("nt51920-dp-replace", "54fe891836a8e899f3ac27c3dd32e35678032b3f0c886d6a2f2659369c7185a9"),
            ("nt51920-standard-merge", "3bb76d56656642af553ff012a619ca8fc38fb7cdabf8ac674e5433998357f9f2"),
            ("nt51923-nt51926-general-merge-logical-candidate", "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96"),
            ("nt51923-ctrlram-replace-candidate", "a98432cdd049fde26a381534d9555b68671ef5e71604209a16b24972ad4b0cd1"),
            ("nt51923-dp-replace", "4c822759f79167ab16331bb9590b89f3d7874ec518660ca098e545ae432569a0"),
            ("nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96"),
            ("nt51926-ctrlram-replace-candidate", "31ce9eb783667ef7ee9e86657a97fd7e541f52fff7239f1a90a19863a06002ae"),
            ("nt51927-ctrlram-replace-candidate", "d0c8a8775a35a01b52b8d8f32a93af0ac798067e2577d2420ab0dd65dd815d0f"),
            ("nt51927-dp-replace", "c805ce9881786131a299675ec84ff272cd3effc74310fc783965abb1a8400568"),
            ("nt51927-standard-merge", "751f44c7dd790a826e9ab17747b933542c691125bdee8b975c9c764e4f2ef4b1"),
            ("nt51928-ctrlram-replace-candidate", "bba0e65221aff3ebbd4b06f83f38295b6e315eff0741fe68952e5844ae64c634"),
            ("nt51928-general-merge-logical-candidate", "9cdfbe52fcf58071ab7ea9648844dc3d0dd5363e6b41db02454709bf921512a6"),
            ("nt51928-dp-replace", "2bc3c74cb886c14d8550887770ba986368dcec28661c9bb5701f42567436e6eb"),
            ("nt51928-standard-merge", "27de29151abd1305a8ebf6ba25118acbf59392efd362d362699310a5564ad5af"),
            ("nt51929-ctrlram-replace-candidate", "a60c51ec6a15ef32f91029bf31fca225cbb7f0081c2ee1d760cb8df2294cf74a"),
            ("nt51929-dp-replace", "072ba46232d3052f4c6f914266135c89d19816243a2416d3516317d707be1c07"),
            ("nt51929-standard-merge", "3c8ace0d7b0360573847d4b2c5f052313af9d2ff680cebe6288cf1611edb8f09"),
            ("nt51930-general-merge-logical-candidate", "0baa3c4829da28540fd93be7b8afae23ce5a23521361976a2dddf2267e18b9e3"),
            ("nt51930-ctrlram-replace-candidate", "33d81fed0864ec319b04c4fd1442b33a4891ecbee2e54ddab179304778c06d48"),
            ("nt51930-standard-merge", "50f9b7f84879088c72ba6da8f23860d92d7819eff5dd0a4772e4b3bc28f0921a"),
            ("nt51931-general-merge-logical-candidate", "ce3b18aede5c884b074b6f9253d45a255e82a2147ec76bd300e7548d6fdc52fe"),
            ("nt51931-ctrlram-replace-candidate", "c52307476cd0df8ba4edc79b5882ca91313f265259290749e856dd8b130abe3d"),
            ("nt51931-dp-replace", "eae8c593556e9cb5d639d2f05c94f8144d091767e490e746cd5cdeb2b5384c9c"),
            ("nt51931-standard-merge", "a7b3534afce6d2fe107363e41554668a71832f203168c81fa09e9f98a1a5815f"),
            ("nt51932-ctrlram-replace-candidate", "273bc2e02812a7ef60dc0a234083316466b25205bd93ebc8e1862b4c35e26603"),
            ("nt51950-ab-merge", "abdd907710be94470937f4f6ee9c250e9ec1f90c4cbd1d10134584ef15878206"),
            ("nt51950-ctrlram-replace-candidate", "7dc48be0c50c94b97b208fbbc87666d71ce84b601a5af19c592155428cebff4b"),
            ("nt51951-ctrlram-replace-candidate", "20fc2016d43941a83fdc8403249384e43008d5b5087c03104eb9e847e6787e81"),
            ("nt51950-nt51951-general-merge-logical-candidate", "1da78f9a6d8aae1e7fbbda0f5977272b5c9902194ab102f2232586edd77eb121"),
            ("nt51950-nt51951-standard-merge", "65987f6b1e41feaca92e7b258bca282df9ae133f90db6877ba6b97c04d91f0f4"),
        }.ToFrozenDictionary(
            static bundle => bundle.Directory,
            static bundle => new BuiltInV2Bundle(bundle.Directory, bundle.ContentHash),
            StringComparer.Ordinal);
}

internal sealed class BuiltInV2Bundle
{
    internal const string CompilationFailed = "profile.v2.builtin-compilation-failed";
    private const string BundleLoadFailed = "profile.v2.builtin-bundle-load-failed";
    private const string TrustAnchorBindingId = "built-in-profile-bundle-v2";
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
            requestedMapCapacity,
            []);
        return compilation.CompiledComposition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable }
            ? compilation
            : V2CompositionPlanCompileResult.Failed(
            compilation.Issues.Count == 0
                ? [new CompositionIssue(CompilationFailed, failureMessage)]
                : compilation.Issues);
    }

    /// <summary>
    /// Compiles the narrowly admitted AB Code function-open route. It may run a
    /// profile whose only remaining blockers are direct-golden certification or
    /// firmware-owner review; every other candidate remains non-executable.
    /// </summary>
    internal V2CompositionPlanCompileResult CompileAbMergeFunctionOpen(
        string profileId,
        string profileVersion,
        string icId,
        long? requestedMapCapacity,
        TopologySelection? requestedTopology,
        string failureMessage)
    {
        V2CompositionPlanCompileResult compilation = Compile(
            profileId,
            profileVersion,
            icId,
            IcWorkflowIds.AbMerge,
            requestedMapCapacity,
            requestedTopology,
            []);
        return compilation.CompiledComposition is { } composition &&
               (composition.Eligibility == CompiledCompositionEligibility.V2RuntimeExecutable ||
                composition.IsV2AbFunctionOpenCandidate)
            ? compilation
            : V2CompositionPlanCompileResult.Failed(
            compilation.Issues.Count == 0
                ? [new CompositionIssue(CompilationFailed, failureMessage)]
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
        return Compile(
            profileId,
            profileVersion,
            icId,
            experienceId,
            requestedMapCapacity,
            requestedTopology: null,
            resolutionArtifacts);
    }

    internal V2CompositionPlanCompileResult Compile(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        long? requestedMapCapacity,
        TopologySelection? requestedTopology,
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
                requestedTopology,
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

    internal V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        string profileId,
        string profileVersion,
        string memberId,
        string experienceId,
        TopologySelection? requestedTopology,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        return CompileRuntimeReferenceReplace(
            profileId,
            profileVersion,
            memberId,
            experienceId,
            requestedTopology,
            [],
            request);
    }

    internal V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        string profileId,
        string profileVersion,
        string memberId,
        string experienceId,
        TopologySelection? requestedTopology,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(resolutionArtifacts);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
                _catalog.Value,
                profileId,
                profileVersion,
                memberId,
                experienceId,
                requestedTopology,
                resolutionArtifacts,
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
            BundleLoadFailed,
            $"The built-in V2 bundle '{RelativeRoot}' could not be loaded: {exception.Message}");
    }

    private TrustedProfileBundleCatalog LoadCatalog()
    {
        string bundleRoot = Path.Combine(AppContext.BaseDirectory, RelativeRoot);
        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(ContentHash, TrustAnchorBindingId),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(16, 131072, 262144, 8)));
        return TrustedProfileBundleCatalogProjection.Create(bundle.CreateDocumentProjection());
    }
}
