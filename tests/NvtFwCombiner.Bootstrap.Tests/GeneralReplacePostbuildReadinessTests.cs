using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench evidence for pre-run General Replace POSTBUILD readiness.</summary>
public sealed class GeneralReplacePostbuildReadinessTests
{
    /// <summary>An uncompiled POSTBUILD target fails before runtime readiness can claim Build identity.</summary>
    [Fact]
    public async Task MissingRuntimeToolCreatesDiagnosticWithoutEngineOutputOrMutation()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-diagnostic");
        byte[] referenceBytes = CreatePostbuildReference(0x31);
        byte[] sourceBytes = [0xA5, 0x5A];
        string referencePath = workspace.Write("reference.bin", referenceBytes);
        string sourcePath = workspace.Write("source.bin", sourceBytes);
        GeneralMappingDraftState draft = Draft(sourcePath);
        var provider = new TestReadinessProvider(isReady: false);
        TrustedProfileBundleCatalog exactParent =
            CreateStageBearingExactParent(workspace);
        var progress = new CompositionRunProgressFeed();
        CompositionAuthoringSessionAdapter.GeneralReplacePostbuildReadinessOverride
            runtime = RuntimeOverride(provider);

        WorkbenchRunResult result = await RunWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                draft,
                build: false,
                exactParent,
                BuiltInV2RegistrationRegistry.GeneralReplaceByIc["NT51926"].ProfileId,
                runtime,
                progress,
                outputPath: null,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Blocked", result.Status);
        Assert.True(result.HasRunReport);
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(referenceBytes, await File.ReadAllBytesAsync(
            referencePath,
            TestContext.Current.CancellationToken));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(
            sourcePath,
            TestContext.Current.CancellationToken));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement root = report.RootElement;
        Assert.Equal(
            WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
            Assert.Single(root.GetProperty("Issues").EnumerateArray())
                .GetProperty("Code").GetString());
        Assert.Empty(root.GetProperty("Mutations").EnumerateArray());
        Assert.Equal(0, root.GetProperty("Output").GetProperty("Size").GetInt64());
        Assert.Null(result.ActionReadiness);
        Assert.Equal("nt51926-general-replace.bin", result.OutputFileName);
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            result.OutputSha256);
        Assert.False(progress.IsAttached);
    }

    /// <summary>Build also fails before a runtime probe when no exact compilation exists.</summary>
    [Fact]
    public async Task MissingRuntimeToolDisablesBuildWithoutCreatingRunReport()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-build");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePostbuildReference(0x32));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        var provider = new TestReadinessProvider(isReady: false);
        TrustedProfileBundleCatalog exactParent =
            CreateStageBearingExactParent(workspace);
        var progress = new CompositionRunProgressFeed();

        WorkbenchRunResult result = await RunWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                build: true,
                exactParent,
                BuiltInV2RegistrationRegistry.GeneralReplaceByIc["NT51926"].ProfileId,
                RuntimeOverride(provider),
                progress,
                outputPath,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Blocked", result.Status);
        Assert.True(result.HasRunReport);
        Assert.NotEmpty(result.ReportJson);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(0, provider.CallCount);
        Assert.Null(result.ActionReadiness);
        Assert.False(progress.IsAttached);
    }

    /// <summary>An unsupported target never starts a refresh whose generation could become stale.</summary>
    [Fact]
    public async Task StaleRuntimeGenerationCannotReachEngineExecution()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-stale");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePostbuildReference(0x33));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        var provider = new TestReadinessProvider(isReady: true);
        TrustedProfileBundleCatalog exactParent =
            CreateStageBearingExactParent(workspace);
        var progress = new CompositionRunProgressFeed();

        WorkbenchRunResult result = await RunWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                build: false,
                exactParent,
                BuiltInV2RegistrationRegistry.GeneralReplaceByIc["NT51926"].ProfileId,
                RuntimeOverride(provider, generationIsCurrent: false),
                progress,
                outputPath: null,
                TestContext.Current.CancellationToken);

        Assert.Equal("Blocked", result.Status);
        Assert.Equal(0, provider.CallCount);
        Assert.Null(result.ActionReadiness);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(
            WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
            Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray())
                .GetProperty("Code")
                .GetString());
        Assert.False(progress.IsAttached);
    }

    private static CompositionAuthoringSessionAdapter
        .GeneralReplacePostbuildReadinessOverride RuntimeOverride(
            IRuntimeDependencyReadinessProvider provider,
            bool generationIsCurrent = true)
    {
        return new CompositionAuthoringSessionAdapter
            .GeneralReplacePostbuildReadinessOverride(
                provider,
                Generation: 1,
                generation => generationIsCurrent && generation == 1);
    }

    private static ValueTask<WorkbenchRunResult> RunWithPostbuildReadinessAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        bool build,
        TrustedProfileBundleCatalog exactParentCatalog,
        string exactParentProfileId,
        CompositionAuthoringSessionAdapter.GeneralReplacePostbuildReadinessOverride runtime,
        CompositionRunProgressFeed progress,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        SavedRuleV2GeneralReplaceExactParent exactParent =
            SavedRuleV2GeneralReplaceExactParentResolver.Resolve(
                exactParentCatalog,
                exactParentProfileId);
        return build
            ? CompositionExecutionAdapter.BuildGeneralReplaceWithInitialInspectionAsync(
                icId,
                number,
                slotPaths,
                mappingDraft,
                new AuthoringRevision(1),
                outputPath,
                progress,
                cancellationToken,
                savedRulePolicy: null,
                runtime,
                exactParent)
            : CompositionExecutionAdapter.PreviewGeneralReplaceWithInitialInspectionAsync(
                icId,
                number,
                slotPaths,
                mappingDraft,
                new AuthoringRevision(1),
                cancellationToken,
                savedRulePolicy: null,
                runtime,
                exactParent);
    }

    private static byte[] CreatePostbuildReference(byte seed)
    {
        byte[] image = CreatePattern(0x40000, seed);
        const int backupStart = 0x3F000;
        const int markerStart = 0x3FFFC;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = 0x20;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = 0xDF;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 2;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 0;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = 0;
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }

    private static TrustedProfileBundleCatalog CreateStageBearingExactParent(
        TempWorkspace workspace)
    {
        const string fixtureRootName = "exact-parent-bundle";
        const string profileRelativePath =
            "profiles/nt51926-general-replace-dp-single-candidate.json";
        string sourceRoot = Path.Combine(
            AppContext.BaseDirectory,
            "profiles",
            "built-in",
            "nt51926-ctrlram-replace-candidate");
        foreach (string sourcePath in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(
                sourceRoot,
                sourcePath);
            _ = workspace.Write(
                Path.Combine(fixtureRootName, relativePath),
                File.ReadAllBytes(sourcePath));
        }

        string profilePath = workspace.PathFor(
            Path.Combine(fixtureRootName, profileRelativePath));
        JsonObject profile = Assert.IsType<JsonObject>(
            JsonNode.Parse(File.ReadAllText(profilePath)));
        profile["views"] = new JsonArray(
            JsonNode.Parse(
                """
                {
                  "viewId": "processor-image",
                  "spaceId": "output-image",
                  "selector": {
                    "kind": "space-range",
                    "range": { "start": 0, "length": 262144 }
                  }
                }
                """));
        profile["operations"] = new JsonArray(
            JsonNode.Parse(
                """
                {
                  "operationId": "test-postbuild",
                  "sequence": 2147483647,
                  "overlapPolicy": "replace-existing",
                  "reason": "Test-only exact Parent runtime readiness stage.",
                  "kind": "run-processor",
                  "processorStageId": "test-general-postbuild"
                }
                """));
        profile["processorStages"] = new JsonArray(
            JsonNode.Parse(
                """
                {
                  "processorStageId": "test-general-postbuild",
                  "kind": "legacy-combiner-v1",
                  "toolBindingId": "legacy-combiner-1.13.0",
                  "invocationProfileId": "nfc.test.general-postbuild",
                  "targetSpaceId": "output-image",
                  "targetViewId": "processor-image",
                  "authority": "transform",
                  "purpose": "header-and-integrity",
                  "integrityDisposition": "recalculate-and-write",
                  "allowedReadViewIds": [ "processor-image" ],
                  "allowedWriteViewIds": [ "processor-image" ],
                  "stagedSourceBindings": [],
                  "stagedArtifactBindings": [],
                  "evidenceRef": "test-general-postbuild-evidence",
                  "failurePolicy": "fail-closed"
                }
                """));
        byte[] profileBytes = Encoding.UTF8.GetBytes(
            profile.ToJsonString());
        File.WriteAllBytes(profilePath, profileBytes);

        string manifestPath = workspace.PathFor(
            Path.Combine(fixtureRootName, "profile-bundle.json"));
        JsonObject manifest = Assert.IsType<JsonObject>(
            JsonNode.Parse(File.ReadAllText(manifestPath)));
        JsonArray entries = Assert.IsType<JsonArray>(
            manifest["entries"]);
        JsonObject profileEntry = Assert.IsType<JsonObject>(
            entries.Single(node =>
                StringComparer.Ordinal.Equals(
                    node!["path"]!.GetValue<string>(),
                    profileRelativePath)));
        profileEntry["contentHash"] = Hash(profileBytes);
        ProfileBundleEntryDocument[] documents =
        [
            .. entries.Select(node =>
            {
                JsonObject entry = Assert.IsType<JsonObject>(node);
                return new ProfileBundleEntryDocument(
                    entry["entryId"]!.GetValue<string>(),
                    entry["kind"]!.GetValue<string>(),
                    entry["path"]!.GetValue<string>(),
                    entry["schemaId"]!.GetValue<string>(),
                    entry["contentHash"]!.GetValue<string>());
            }),
        ];
        string bundleContentHash =
            ProfileBundleEntryArrayHasher.CalculateContentHash(
                documents);
        manifest["contentHash"] = bundleContentHash;
        File.WriteAllBytes(
            manifestPath,
            Encoding.UTF8.GetBytes(manifest.ToJsonString()));

        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            Path.GetDirectoryName(manifestPath)!,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(
                bundleContentHash,
                "built-in-profile-bundle-v2"),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(
                    16,
                    131072,
                    262144,
                    8)));
        return TrustedProfileBundleCatalogProjection.Create(
            bundle.CreateDocumentProjection(),
            BuiltInCanonicalMetadataDefinitionResolver.Instance);
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
    }

    private static GeneralMappingDraftState Draft(string sourcePath)
    {
        return new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "tp-map",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(sourcePath),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0x22800, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                "Runtime readiness regression."),
        ]);
    }

    private sealed class TestReadinessProvider :
        IRuntimeDependencyReadinessProvider
    {
        private readonly bool _isReady;

        internal TestReadinessProvider(bool isReady)
        {
            _isReady = isReady;
        }

        internal int CallCount { get; private set; }

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            ExternalProcessorDependencyReference dependency =
                Assert.Single(request.Dependencies);
            RuntimeDependencyEntry entry = _isReady
                ? RuntimeDependencyEntry.Ready(
                    dependency.ProcessorId,
                    dependency.ToolBindingId)
                : RuntimeDependencyEntry.Blocked(
                    dependency.ProcessorId,
                    dependency.ToolBindingId,
                    "external-tool.executable.missing",
                    "The required runtime tool is unavailable.");
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.CompilationFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                DateTimeOffset.UnixEpoch,
                [entry]));
        }
    }
}
