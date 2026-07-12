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
        "2acde361b0537210c4707f2a77a112d659ac885254ef863df2a2d75baa12ff53");
    private static readonly BuiltInV2StandardMergeBundle s_nt51929FamilyV2Bundle = new(
        "profiles\\built-in\\nt51929-standard-merge",
        "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3");
    private static readonly ReadOnlyCollection<BuiltInV2StandardMergeRegistration> s_builtInV2StandardMergeRegistrations =
        Array.AsReadOnly(
        [
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
                "NT51929",
                "nt51929-standard-merge-gen-flash",
                "0.5.0",
                s_nt51929FamilyV2Bundle),
            new BuiltInV2StandardMergeRegistration(
                "NT51932",
                "nt51932-standard-merge-gen-flash",
                "0.5.0",
                s_nt51929FamilyV2Bundle),
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
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!s_builtInV2StandardMergeByIc.TryGetValue(icId, out BuiltInV2StandardMergeRegistration? registration))
        {
            composition = null;
            issues = [];
            return false;
        }

        V2CompositionPlanCompileResult compilation = registration.Compilation;
        composition = compilation.CompiledComposition;
        issues = compilation.Issues;
        return true;
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
            try
            {
                return TrustedV2CompositionCompiler.Compile(
                    _catalog.Value,
                    profileId,
                    profileVersion,
                    icId,
                    IcWorkflowIds.StandardMerge);
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
        private readonly Lazy<V2CompositionPlanCompileResult> _compilation;

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
            _compilation = new Lazy<V2CompositionPlanCompileResult>(
                LoadCompilation);
        }

        internal string IcId { get; }

        internal string ProfileId { get; }

        internal string ProfileVersion { get; }

        internal BuiltInV2StandardMergeBundle Bundle { get; }

        internal V2CompositionPlanCompileResult Compilation => _compilation.Value;

        private V2CompositionPlanCompileResult LoadCompilation()
        {
            V2CompositionPlanCompileResult compilation = Bundle.Compile(ProfileId, ProfileVersion, IcId);
            return compilation.CompiledComposition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable }
                ? compilation
                : compilation.Issues.Count == 0
                ? V2CompositionPlanCompileResult.Failed(
                    [new CompositionIssue(
                        BuiltInV2CompilationFailed,
                        $"The built-in V2 profile for {IcId} did not produce an executable composition.")])
                : V2CompositionPlanCompileResult.Failed(compilation.Issues);
        }

        internal WorkbenchProfileSummary CreateProfileSummary()
        {
            return Compilation.CompiledComposition is { } composition
                ? WorkbenchCompositionService.CreateProfileSummary(composition)
                : new WorkbenchProfileSummary(
                    ProfileId,
                    IcId,
                    CompositionKind.Merge,
                    [],
                    StandardMergeFallbackOutputFileName,
                    null,
                    CompileSucceeded: false,
                    Array.AsReadOnly(Compilation.Issues.Select(static issue => issue.Code).ToArray()));
        }
    }
}
