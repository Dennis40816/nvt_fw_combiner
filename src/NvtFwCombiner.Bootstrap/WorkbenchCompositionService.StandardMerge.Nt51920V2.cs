using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51920IcId = "NT51920";
    private const string Nt51920ProfileId = "nt51920-standard-merge-gen-flash";
    private const string Nt51920ProfileVersion = "0.5.0";
    private const string Nt51920BundleContentHash = "2acde361b0537210c4707f2a77a112d659ac885254ef863df2a2d75baa12ff53";
    private const string Nt51920BundleTrustAnchorBindingId = "built-in-profile-bundle-v2";
    private const string Nt51920BundleRelativeRoot = "profiles\\built-in\\nt51920-standard-merge";
    private const string Nt51920V2BundleLoadFailed = "profile.v2.builtin-bundle-load-failed";
    private const string Nt51920V2CompilationFailed = "profile.v2.builtin-compilation-failed";

    private static readonly Lazy<Nt51920V2StandardMergeCompilation> s_nt51920V2Compilation = new(
        LoadNt51920V2StandardMergeCompilation);

    private static bool IsNt51920V2StandardMerge(string icId)
    {
        return StringComparer.Ordinal.Equals(icId, Nt51920IcId);
    }

    private static bool TryCompileNt51920V2StandardMerge(
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        Nt51920V2StandardMergeCompilation compilation = s_nt51920V2Compilation.Value;
        composition = compilation.Composition;
        issues = compilation.Issues;
        return composition is not null;
    }

    private static WorkbenchProfileSummary CreateNt51920V2StandardMergeProfileSummary()
    {
        return TryCompileNt51920V2StandardMerge(out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues)
            ? CreateProfileSummary(composition)
            : new WorkbenchProfileSummary(
                Nt51920ProfileId,
                Nt51920IcId,
                CompositionKind.Merge,
                [],
                StandardMergeFallbackOutputFileName,
                null,
                CompileSucceeded: false,
                Array.AsReadOnly(issues.Select(static issue => issue.Code).ToArray()));
    }

    private static Nt51920V2StandardMergeCompilation LoadNt51920V2StandardMergeCompilation()
    {
        try
        {
            string bundleRoot = Path.Combine(AppContext.BaseDirectory, Nt51920BundleRelativeRoot);
            TrustedProfileBundle bundle = ProfileBundleLoader.Load(
                bundleRoot,
                "profile-bundle.json",
                new ProfileBundleTrustAnchor(Nt51920BundleContentHash, Nt51920BundleTrustAnchorBindingId),
                new ProfileBundleLoadLimits(
                    maximumManifestBytes: 16384,
                    maximumJsonDepth: 32,
                    new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
            TrustedProfileBundleCatalog catalog = TrustedProfileBundleCatalogProjection.Create(
                bundle.CreateDocumentProjection());
            V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
                catalog,
                Nt51920ProfileId,
                Nt51920ProfileVersion,
                Nt51920IcId,
                IcWorkflowIds.StandardMerge);
            return compilation.CompiledComposition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable } composition
                ? Nt51920V2StandardMergeCompilation.Succeeded(composition)
                : Nt51920V2StandardMergeCompilation.Failed(
                    compilation.Issues,
                    Nt51920V2CompilationFailed,
                    "The built-in NT51920 V2 profile did not produce an executable composition.");
        }
        catch (Exception exception) when (exception is IOException or
                                         UnauthorizedAccessException or
                                         InvalidDataException or
                                         ProfileBundleManifestNormalizationException or
                                         CompositionProfileNormalizationException or
                                         TrustedProfileBundleCatalogException)
        {
            return Nt51920V2StandardMergeCompilation.Failed(
                [],
                Nt51920V2BundleLoadFailed,
                $"The built-in NT51920 V2 bundle could not be loaded: {exception.Message}");
        }
    }

    private sealed class Nt51920V2StandardMergeCompilation
    {
        private readonly CompositionIssue[] _issues;

        private Nt51920V2StandardMergeCompilation(
            CompiledComposition? composition,
            IEnumerable<CompositionIssue> issues)
        {
            ArgumentNullException.ThrowIfNull(issues);
            _issues = [.. issues];
            if (_issues.Any(static issue => issue is null) ||
                (composition is null) != (_issues.Length != 0))
            {
                throw new ArgumentException(
                    "A built-in V2 compilation must contain either one composition or one or more issues.",
                    nameof(issues));
            }

            Composition = composition;
            Issues = Array.AsReadOnly(_issues);
        }

        internal CompiledComposition? Composition { get; }

        internal IReadOnlyList<CompositionIssue> Issues { get; }

        internal static Nt51920V2StandardMergeCompilation Succeeded(CompiledComposition composition)
        {
            ArgumentNullException.ThrowIfNull(composition);
            return new Nt51920V2StandardMergeCompilation(composition, []);
        }

        internal static Nt51920V2StandardMergeCompilation Failed(
            IEnumerable<CompositionIssue> issues,
            string fallbackCode,
            string fallbackMessage)
        {
            ArgumentNullException.ThrowIfNull(issues);
            ArgumentException.ThrowIfNullOrWhiteSpace(fallbackCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(fallbackMessage);
            CompositionIssue[] snapshot = [.. issues];
            return new Nt51920V2StandardMergeCompilation(
                composition: null,
                snapshot.Length == 0
                    ? [new CompositionIssue(fallbackCode, fallbackMessage)]
                    : snapshot);
        }
    }
}
