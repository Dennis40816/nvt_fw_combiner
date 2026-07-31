using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
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
    /// <summary>A declared stage with a missing tool yields only a plan-only diagnostic.</summary>
    [Fact]
    public async Task MissingRuntimeToolCreatesDiagnosticWithoutEngineOutputOrMutation()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-diagnostic");
        byte[] referenceBytes = CreatePattern(0x40000, 0x31);
        byte[] sourceBytes = [0xA5, 0x5A];
        string referencePath = workspace.Write("reference.bin", referenceBytes);
        string sourcePath = workspace.Write("source.bin", sourceBytes);
        GeneralMappingDraftState draft = Draft(sourcePath);
        var provider = new TestReadinessProvider(isReady: false);
        TrustedProfileBundleCatalog exactParent =
            CreateStageBearingExactParent(workspace);
        var progress = new CompositionRunProgressFeed();
        WorkbenchCompositionService.GeneralReplacePostbuildReadinessOverride
            runtime = RuntimeOverride(provider);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunGeneralReplaceEphemeralDraftWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                draft,
                build: false,
                exactParent,
                WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileId,
                runtime,
                progress,
                outputPath: null,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("DiagnosticPlanOnly", result.Status);
        Assert.True(result.HasRunReport);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(referenceBytes, await File.ReadAllBytesAsync(
            referencePath,
            TestContext.Current.CancellationToken));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(
            sourcePath,
            TestContext.Current.CancellationToken));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement root = report.RootElement;
        JsonElement diagnostic = root.GetProperty("DiagnosticPreview");
        Assert.Equal(
            "test-general-postbuild",
            diagnostic.GetProperty("RequiredStageId").GetString());
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
            diagnostic.GetProperty("Blocker").GetProperty("Code").GetString());
        Assert.False(diagnostic.GetProperty("OutputProduced").GetBoolean());
        Assert.False(diagnostic.GetProperty("ClaimsFinalIntegrity").GetBoolean());
        Assert.Empty(root.GetProperty("Mutations").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Output").ValueKind);
        Assert.Empty(result.OutputFileName);
        Assert.Empty(result.OutputSha256);
        Assert.False(progress.IsAttached);
    }

    /// <summary>The same blocker disables Build before a report or BIN exists.</summary>
    [Fact]
    public async Task MissingRuntimeToolDisablesBuildWithoutCreatingRunReport()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-build");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x32));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        var provider = new TestReadinessProvider(isReady: false);
        TrustedProfileBundleCatalog exactParent =
            CreateStageBearingExactParent(workspace);
        var progress = new CompositionRunProgressFeed();

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunGeneralReplaceEphemeralDraftWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                build: true,
                exactParent,
                WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileId,
                RuntimeOverride(provider),
                progress,
                outputPath,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("BuildUnavailable", result.Status);
        Assert.False(result.HasRunReport);
        Assert.Empty(result.ReportJson);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
            result.ActionReadiness!.Build.PrimaryBlocker!.Code);
        Assert.False(progress.IsAttached);
    }

    /// <summary>A refreshed tool result from a superseded generation remains diagnostic-only.</summary>
    [Fact]
    public async Task StaleRuntimeGenerationCannotReachEngineExecution()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-stale");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x33));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        var provider = new TestReadinessProvider(isReady: true);
        TrustedProfileBundleCatalog exactParent =
            CreateStageBearingExactParent(workspace);
        var progress = new CompositionRunProgressFeed();

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunGeneralReplaceEphemeralDraftWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                build: false,
                exactParent,
                WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileId,
                RuntimeOverride(provider, generationIsCurrent: false),
                progress,
                outputPath: null,
                TestContext.Current.CancellationToken);

        Assert.Equal("DiagnosticPlanOnly", result.Status);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            result.ActionReadiness!.Build.PrimaryBlocker!.Code);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            report.RootElement.GetProperty("DiagnosticPreview")
                .GetProperty("Blocker")
                .GetProperty("Code")
                .GetString());
        Assert.Equal(
            JsonValueKind.Null,
            report.RootElement.GetProperty("Output").ValueKind);
        Assert.False(progress.IsAttached);
    }

    private static WorkbenchCompositionService
        .GeneralReplacePostbuildReadinessOverride RuntimeOverride(
            IRuntimeDependencyReadinessProvider provider,
            bool generationIsCurrent = true)
    {
        return new WorkbenchCompositionService
            .GeneralReplacePostbuildReadinessOverride(
                provider,
                Generation: 1,
                generation => generationIsCurrent && generation == 1);
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
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                DateTimeOffset.UnixEpoch,
                [entry]));
        }
    }
}
