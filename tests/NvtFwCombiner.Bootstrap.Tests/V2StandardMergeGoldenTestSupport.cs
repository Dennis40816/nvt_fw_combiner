using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared trusted-V2 Standard Merge migration evidence helpers.</summary>
internal static class V2StandardMergeGoldenTestSupport
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 7, 12, 0, 0, 1, TimeSpan.Zero);

    internal static TrustedProfileBundleCatalog LoadDeployedCatalog(string bundleDirectory, string bundleContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleContentHash);
        string bundleRoot = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", bundleDirectory);
        return LoadCatalogRoot(bundleRoot, bundleContentHash);
    }

    internal static void AssertSourceCatalogIsRejected(string bundleDirectory, string bundleContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleContentHash);
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", bundleDirectory);
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => LoadBundle(bundleRoot, bundleContentHash));
        Assert.Contains("schemas/", exception.Message, StringComparison.Ordinal);
    }

    private static TrustedProfileBundleCatalog LoadCatalogRoot(string bundleRoot, string bundleContentHash)
    {
        AssertSchemaSnapshotsMatchManifest(bundleRoot);
        TrustedProfileBundle bundle = LoadBundle(bundleRoot, bundleContentHash);
        return TrustedProfileBundleCatalogProjection.Create(
            bundle.CreateDocumentProjection(),
            BuiltInCanonicalMetadataDefinitionResolver.Instance);
    }

    private static TrustedProfileBundle LoadBundle(string bundleRoot, string bundleContentHash)
    {
        return ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(bundleContentHash, "built-in-profile-bundle-v2"),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(16, 131072, 262144, 8)));
    }

    internal static CompiledComposition CompileV2(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string icId,
        long? requestedMapCapacity = null,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        V2CompositionPlanCompileResult compilation = catalog.Compile(
            profileId,
            profileVersion,
            icId,
            ExperienceIds.StandardMerge,
            requestedMapCapacity,
            [],
            selectedInputSlotIds);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        return composition;
    }

    internal static JsonElement ReadGoldenCase(string referenceIc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceIc);
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        return manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => StringComparer.Ordinal.Equals(item.GetProperty("ic").GetString(), referenceIc))
            .Clone();
    }

    internal static Dictionary<string, byte[]> ReadInputs(JsonElement inputs)
    {
        return inputs.EnumerateObject().ToDictionary(
            static input => input.Name,
            input => ReadManifestFile(input.Value),
            StringComparer.Ordinal);
    }

    internal static byte[] ReadManifestFile(JsonElement manifestFile)
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.ManifestPath(GoldenRoot, manifestFile));
        Assert.Equal(manifestFile.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(manifestFile.GetProperty("sha256").GetString(), Hash(bytes));
        return bytes;
    }

    internal static async ValueTask<CompositionRunResult> PreviewAsync(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, byte[]> inputs)
    {
        CompiledAuthoringSelectedInput[] selectedInputs =
        [
            .. inputs.OrderBy(static item => item.Key, StringComparer.Ordinal)
                .Select(static item => new CompiledAuthoringSelectedInput(
                    item.Key,
                    $"{item.Key}.bin",
                    item.Value)),
        ];
        var session = new AuthoringSessionState(ExperienceIds.StandardMerge);
        CompiledAuthoringSessionPreparation prepared =
            BootstrapTestHost.Services.StandardMergeAuthoring.PrepareSession(
                session,
                compiledComposition.V2Details.Provenance.Context.MemberId,
                selectedInputs);
        Assert.True(prepared.Succeeded, FormatIssues(prepared.Issues));
        ActiveSessionSnapshot acceptedSession = Assert.IsType<ActiveSessionSnapshot>(prepared.Snapshot);
        ResolvedCapability acceptedCapability = Assert.IsType<ResolvedCapability>(acceptedSession.ExactCapability);
        CompiledComposition executionComposition = acceptedCapability.CompiledComposition;
        AssertPublishedCompositionMatchesBundle(compiledComposition, executionComposition);
        AcceptedOutputNamingInspection acceptedNaming =
            AcceptedOutputNamingInspection.Accept(acceptedSession);
        OutputNamingAdmissionIdentity namingAdmission = OutputNamingAdmissionIdentity.Capture(
            acceptedCapability,
            acceptedSession.AuthoringRevision.Value);

        var reader = new FakeArtifactReader(inputs.ToDictionary(
            item => $"{compiledComposition.V2Details.ProfileId}:{item.Key}",
            static item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings =
        [
            .. inputs.Keys.Order(StringComparer.Ordinal)
                .Select(addressSpaceId => CreateInputBinding(compiledComposition.V2Details.ProfileId, addressSpaceId)),
        ];
        var service = new CompositionRunService(reader, new FakeClock([StartedAtUtc, CompletedAtUtc]));
        var request = new CompositionRunRequest(
            $"golden-{compiledComposition.V2Details.Provenance.Context.MemberId.ToLowerInvariant()}",
            executionComposition,
            bindings,
            executionComposition.V2Details.OutputNamingRequirement.FileNameTemplate,
            outputNamingInspection: acceptedNaming,
            outputNamingAdmission: namingAdmission,
            resolvedCapability: acceptedCapability);
        return await service.PreviewAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    internal static void AssertSuccessfulGoldenOutput(
        CompositionRunResult result,
        CompiledComposition compiledComposition,
        byte[] expectedOutput,
        IReadOnlyList<string>? expectedIssueCodes = null)
    {
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(
            expectedIssueCodes ?? [],
            result.Report.Issues.Select(static issue => issue.Code));
        Assert.Equal(expectedOutput, result.OutputBytes.ToArray());
        Assert.Equal(Hash(expectedOutput), result.Report.Output.Sha256);
        Assert.Equal(compiledComposition.V2Details.ProfileId, result.Report.ProfileId);
        Assert.Equal(compiledComposition.V2Details.ProfileVersion, result.Report.ProfileVersion);
        Assert.Equal(compiledComposition.V2Details.Provenance.Context.MemberId, result.Report.IcId);
        Assert.Equal(compiledComposition.V2Details.Provenance.Context.ModeId, result.Report.ModeId);
        Assert.Equal(compiledComposition.V2Details.ExperienceId, result.Report.ExperienceId);
        Assert.Equal(64, Assert.IsType<string>(result.Report.CompilationFingerprint).Length);
    }

    internal static string GoldenRoot => CanonicalGoldenTestData.Root;

    private static void AssertSchemaSnapshotsMatchManifest(string bundleRoot)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundleRoot, "profile-bundle.json")));
        foreach (JsonElement entry in manifest.RootElement.GetProperty("entries").EnumerateArray().Where(static entry =>
                     StringComparer.Ordinal.Equals(entry.GetProperty("kind").GetString(), "schema")))
        {
            string schemaFileName = Path.GetFileName(entry.GetProperty("path").GetString()!);
            string contentHash = entry.GetProperty("contentHash").GetString()!;
            Assert.Equal(
                File.ReadAllBytes(ResolveContractSchemaPath(schemaFileName, contentHash)),
                File.ReadAllBytes(Path.Combine(bundleRoot, "schemas", schemaFileName)));
        }
    }

    internal static string ResolveContractSchemaPath(string schemaPath, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        string contractRoot = RepositoryPaths.FromRepositoryRoot("docs", "contracts");
        string[] matches =
        [
            .. Directory.EnumerateFiles(contractRoot, "*.schema.json", SearchOption.TopDirectoryOnly)
                .Where(path => StringComparer.Ordinal.Equals(Hash(File.ReadAllBytes(path)), contentHash)),
        ];
        return Assert.Single(matches);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }

    private static void AssertPublishedCompositionMatchesBundle(
        CompiledComposition bundleComposition,
        CompiledComposition publishedComposition)
    {
        Assert.Equal(bundleComposition.V2Details.ProfileId, publishedComposition.V2Details.ProfileId);
        Assert.Equal(bundleComposition.V2Details.ProfileVersion, publishedComposition.V2Details.ProfileVersion);
        Assert.Equal(
            bundleComposition.V2Details.Provenance.Context.MemberId,
            publishedComposition.V2Details.Provenance.Context.MemberId);
        Assert.Equal(
            bundleComposition.V2Details.Provenance.Context.ModeId,
            publishedComposition.V2Details.Provenance.Context.ModeId);
        Assert.Equal(bundleComposition.V2Details.ExperienceId, publishedComposition.V2Details.ExperienceId);
        Assert.Equal(bundleComposition.V2Details.CompositionKind, publishedComposition.V2Details.CompositionKind);
        Assert.Equal(
            bundleComposition.V2Details.OutputNamingRequirement.FileNameTemplate,
            publishedComposition.V2Details.OutputNamingRequirement.FileNameTemplate);
        Assert.Equal(bundleComposition.Plan.OutputSpaceId, publishedComposition.Plan.OutputSpaceId);
        Assert.Equal(
            bundleComposition.Plan.OutputInitialization.Capacity,
            publishedComposition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            bundleComposition.Plan.OrderedOperations.Select(static operation => operation.OperationId),
            publishedComposition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
    }

    private static InputArtifactBinding CreateInputBinding(string profileId, string addressSpaceId)
    {
        (string originalFileName, CompiledInputArtifactClass artifactClass) = addressSpaceId switch
        {
            "dp-input" => ("dp.bin", CompiledInputArtifactClass.DpFirmware),
            "tp-input" => ("tp.bin", CompiledInputArtifactClass.TpFirmware),
            "ldc-input" => ("ldc.bin", CompiledInputArtifactClass.Auxiliary),
            _ => throw new ArgumentOutOfRangeException(nameof(addressSpaceId), addressSpaceId, "Unsupported Standard Merge input space."),
        };
        return new InputArtifactBinding(
            addressSpaceId,
            addressSpaceId,
            $"{profileId}:{addressSpaceId}",
            originalFileName,
            artifactClass);
    }
}
