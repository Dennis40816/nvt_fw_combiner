using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <inheritdoc/>
public sealed class RuntimeReferenceReplaceCompilerIntegrationTests
{
    /// <summary>Verifies a compiler-lowered executable candidate reaches Application Preview without mutating either caller input.</summary>
    [Fact]
    public async Task CompilerLoweredRuntimeReferenceCandidateRunsThroughSharedApplicationEngine()
    {
        V2CompositionPlanCompileResult result = CreateRuntimeReferenceCatalog().CompileRuntimeReferenceReplace(
            "runtime-general-replace",
            "1.0.0",
            "NT00001",
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2RuntimeReferenceReplaceInputBinding("base", "reference", 16),
                    new V2RuntimeReferenceReplaceInputBinding("source-a", "source", 4),
                ],
                [new ExplicitMapping(
                    "replace-source",
                    10,
                    ExplicitMappingOperationKind.ReplaceRange,
                    "source-a",
                    new ByteRange(2, 2),
                    "output-image",
                    new ByteRange(8, 2),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "Synthetic runtime General Replace mapping")]));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.True(result.IsCompiled);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, composition.V2Details.Provenance.Promotion.Stage);

        byte[] reference = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        byte[] source = [0xAA, 0xBB, 0xCC, 0xDD];
        byte[] originalReference = [.. reference];
        byte[] originalSource = [.. source];
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["base-artifact"] = reference,
                ["source-artifact"] = source,
            }),
            new FakeClock([
                new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 15, 0, 0, 1, TimeSpan.Zero),
            ]),
            writer);
        var request = new CompositionRunRequest(
            "compiler-lowered-runtime-reference",
            composition,
            [
                new InputArtifactBinding(
                    "base",
                    "base",
                    "base-artifact",
                    "base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "source-a",
                    "source-a",
                    "source-artifact",
                    "source.bin",
                    CompiledInputArtifactClass.Auxiliary),
            ],
            "runtime-general-replace.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 0xCC, 0xDD, 10, 11, 12, 13, 14, 15], preview.OutputBytes.ToArray());
        Assert.Equal(originalReference, reference);
        Assert.Equal(originalSource, source);
        Assert.Equal(composition.CompilationFingerprint, preview.Report.CompilationFingerprint);
        Assert.Equal(
            ["base", "source-a"],
            preview.Report.Inputs.Select(static input => input.ArtifactId).Order(StringComparer.Ordinal));
        Assert.False(writer.WasCalled);
    }

    private static TrustedProfileBundleCatalog CreateRuntimeReferenceCatalog()
    {
        RuntimeReferenceReplaceMapDocument[] maps = [new("map", 16)];
        string familyJson = RuntimeReferenceReplaceTestDocuments.FamilyJson(maps, "explicit-range");
        string familyHash = HashRuntimeReferenceDocument(familyJson);
        string profileJson = RuntimeReferenceReplaceTestDocuments.ProfileJson(
            familyHash,
            "executable-candidate",
            maps.Select(static map => map.MapId));
        using var familyDocument = JsonDocument.Parse(familyJson);
        using var profileDocument = JsonDocument.Parse(profileJson);
        return TrustedProfileBundleCatalogFactory.Create(new TrustedProfileBundleCatalogSource(
            RuntimeReferenceManifestHash,
            new ProfileBundleIdentity(
                "runtime-reference-bundle",
                "1.0.0",
                RuntimeReferenceBundleHash,
                "runtime-reference-release"),
            [new TrustedFirmwareFamilyJsonSource(
                new TrustedProfileBundleCatalogEntryIdentity(
                    "family-entry",
                    "families/family-entry.json",
                    RuntimeReferenceFamilySchemaId,
                    familyHash),
                familyDocument.RootElement.Clone())],
            [new TrustedCompositionProfileJsonSource(
                new TrustedProfileBundleCatalogEntryIdentity(
                    "runtime-reference-profile",
                    "profiles/runtime-reference-profile.json",
                    RuntimeReferenceProfileSchemaId,
                    HashRuntimeReferenceDocument(profileJson)),
                profileDocument.RootElement.Clone())]));
    }

    private static string HashRuntimeReferenceDocument(string document)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document))).ToLowerInvariant();
    }

    private const string RuntimeReferenceManifestHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RuntimeReferenceBundleHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string RuntimeReferenceFamilySchemaId =
        "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json";
    private const string RuntimeReferenceProfileSchemaId =
        "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json";

    private sealed class RecordingOutputWriter : ICompositionOutputWriter
    {
        public bool WasCalled { get; private set; }

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }
}
