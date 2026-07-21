using System.Collections.Frozen;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2BundleRegistry
{
    internal static FrozenDictionary<string, BuiltInV2Bundle> All { get; } =
        new (string Directory, string ContentHash)[]
        {
            ("nt51917-nt51927-general-merge-logical-candidate", "1025069140de5ba78296af045dc477cf8164395b68b0ce82a77970eecbe05c0e"),
            ("nt51917-ctrlram-replace-alias-candidate", "e845b4ee12aa0f7c3cef32f72b65942201b370fdef5850614299e84ab555d677"),
            ("nt51919-nt51929-nt51932-general-merge-logical-candidate", "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283"),
            ("nt51920-ctrlram-replace-candidate", "2f540ce425976a1b446f0b3b83b4d62f9cd08a4ab61dd0e5b1bc4bfb299b6021"),
            ("nt51920-general-merge-logical-candidate", "d2f87973576f54b80439f30ef1790f47df2994a6811673f0ceb8ecd5cacdbdc7"),
            ("nt51920-dp-replace", "54fe891836a8e899f3ac27c3dd32e35678032b3f0c886d6a2f2659369c7185a9"),
            ("nt51920-standard-merge", "3bb76d56656642af553ff012a619ca8fc38fb7cdabf8ac674e5433998357f9f2"),
            ("nt51923-nt51926-general-merge-logical-candidate", "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96"),
            ("nt51923-ctrlram-replace-candidate", "93386dd570ffdc62dc22081d9b7c888b395699ccd9ec1542b91de83463eecc8a"),
            ("nt51923-dp-replace", "4c822759f79167ab16331bb9590b89f3d7874ec518660ca098e545ae432569a0"),
            ("nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96"),
            ("nt51926-ctrlram-replace-candidate", "0fc26f5dda12c9ebb0127f961ecccc53eb986d1cb8397c118f48ed7c6751f430"),
            ("nt51927-ctrlram-replace-candidate", "36b7258c3b660156a4b920726c9d8d9b83083abf8975c3a1ba4c6516abfe01d0"),
            ("nt51927-dp-replace", "c805ce9881786131a299675ec84ff272cd3effc74310fc783965abb1a8400568"),
            ("nt51927-standard-merge", "751f44c7dd790a826e9ab17747b933542c691125bdee8b975c9c764e4f2ef4b1"),
            ("nt51928-ctrlram-replace-candidate", "6e3b7a3c4f509b051773933da7b3221869f34ef7ae50772509baab393254706f"),
            ("nt51928-general-merge-logical-candidate", "9cdfbe52fcf58071ab7ea9648844dc3d0dd5363e6b41db02454709bf921512a6"),
            ("nt51928-dp-replace", "2bc3c74cb886c14d8550887770ba986368dcec28661c9bb5701f42567436e6eb"),
            ("nt51928-standard-merge", "27de29151abd1305a8ebf6ba25118acbf59392efd362d362699310a5564ad5af"),
            ("nt51929-ctrlram-replace-candidate", "dd0e423068c9800b3a5900dc26d504ecf414c4c87756d4f5ac0c6896973785fa"),
            ("nt51929-dp-replace", "072ba46232d3052f4c6f914266135c89d19816243a2416d3516317d707be1c07"),
            ("nt51929-standard-merge", "3c8ace0d7b0360573847d4b2c5f052313af9d2ff680cebe6288cf1611edb8f09"),
            ("nt51930-general-merge-logical-candidate", "0baa3c4829da28540fd93be7b8afae23ce5a23521361976a2dddf2267e18b9e3"),
            ("nt51930-ctrlram-replace-candidate", "f454edeaf306fda174582883d4ff2eb841766ef8fc440141d0474aacabbc82cc"),
            ("nt51930-standard-merge", "50f9b7f84879088c72ba6da8f23860d92d7819eff5dd0a4772e4b3bc28f0921a"),
            ("nt51931-general-merge-logical-candidate", "ce3b18aede5c884b074b6f9253d45a255e82a2147ec76bd300e7548d6fdc52fe"),
            ("nt51931-ctrlram-replace-candidate", "868005d188b502e6fd9b82af2c2af54c02080ac4e41c06c3ff15a017be633836"),
            ("nt51931-dp-replace", "eae8c593556e9cb5d639d2f05c94f8144d091767e490e746cd5cdeb2b5384c9c"),
            ("nt51931-standard-merge", "a7b3534afce6d2fe107363e41554668a71832f203168c81fa09e9f98a1a5815f"),
            ("nt51932-ctrlram-replace-candidate", "8c991b66a3eb8cc77552f1c6a86af7e107b1dd8568c2dbd956801a0818f12bf9"),
            ("nt51950-ctrlram-replace-candidate", "3a4b2b62a9cf736b0161f0e0dd3e56dd77dd54a9b2dbef239e3f1f25fb034fb0"),
            ("nt51951-ctrlram-replace-candidate", "dec3e75a0e3f162361af34385550b1bb24a051096a02925f096885b61b50decb"),
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
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
        return TrustedProfileBundleCatalogProjection.Create(bundle.CreateDocumentProjection());
    }
}
