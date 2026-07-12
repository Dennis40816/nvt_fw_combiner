using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for the first canonical V2 Standard Merge bundle.</summary>
public sealed class Nt51920V2StandardMergeGoldenTests
{
    private const string BundleContentHash = "d75ee5b099d2a3d8515d4c13a1f70b50c3f0cbff98f0578910f7929abbe03260";
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 7, 12, 0, 0, 1, TimeSpan.Zero);

    /// <summary>Verifies the trusted NT51920 V2 bundle compiles and preserves the owner-approved Standard Merge bytes and plan geometry.</summary>
    [Fact]
    public async Task TrustedV2BundleMatchesLegacyPlanAndOwnerApprovedGoldenBytes()
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", "nt51920-standard-merge");
        AssertSchemaSnapshotMatchesContract(
            repositoryRoot,
            bundleRoot,
            "firmware-family-v1.schema.json");
        AssertSchemaSnapshotMatchesContract(
            repositoryRoot,
            bundleRoot,
            "composition-profile-v2.schema.json");
        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(BundleContentHash, "built-in-profile-bundle-v2"),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
        TrustedProfileBundleCatalog catalog = TrustedProfileBundleCatalogProjection.Create(
            bundle.CreateDocumentProjection());
        TrustedProfileBundleCatalog.ProfileSelection selection = Assert.IsType<TrustedProfileBundleCatalog.ProfileSelection>(
            catalog.SelectProfile("nt51920-standard-merge-gen-flash", "0.5.0").Selection);
        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            new V2CompositionPreparationRequest(
                selection,
                new FirmwareMapResolutionInputs(
                    "NT51920",
                    ExperienceIds.StandardMerge,
                    0x40000,
                    requestedTopology: null,
                    [])));
        Assert.True(preparation.IsAdmitted, FormatIssues(preparation.Issues));

        V2CompositionPlanCompileResult compilation = V2CompositionPlanCompiler.Compile(preparation);
        CompiledComposition v2 = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, v2.Eligibility);
        Assert.Equal("NT51920", v2.IcId);
        Assert.Equal(ExperienceIds.StandardMerge, v2.ModeId);
        Assert.Equal("nt51920-standard-merge-gen-flash.bin", v2.DefaultOutputFileName);

        Profiles.CompositionProfileDefinition legacyProfile = BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles
            .Single(profile => profile.IcId == "NT51920");
        ProfileCompileResult legacyCompilation = CompositionProfileCompiler.Compile(legacyProfile, []);
        CompiledComposition legacy = Assert.IsType<CompiledComposition>(legacyCompilation.CompiledComposition);
        Assert.True(legacyCompilation.IsSuccess, FormatIssues(legacyCompilation.Issues));
        AssertPlanParity(legacy.Plan, v2.Plan);

        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == "51920")
            .Clone();
        Dictionary<string, byte[]> inputs = ReadInputs(goldenRoot, goldenCase.GetProperty("inputs"));
        CompositionRunResult result = await PreviewAsync(v2, inputs);
        byte[] expectedOutput = ReadManifestFile(goldenRoot, goldenCase.GetProperty("expectedOutput"));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(expectedOutput, result.OutputBytes.ToArray());
        Assert.Equal(Hash(expectedOutput), result.Report.Output.Sha256);
        Assert.Equal(v2.CompilationFingerprint, result.Report.CompilationFingerprint);
        Assert.Equal(
            ["dp.bin", "tp.bin"],
            result.Report.Inputs.Select(static input => input.OriginalFileName).Order(StringComparer.Ordinal));
    }

    private static void AssertPlanParity(CompositionPlan legacy, CompositionPlan v2)
    {
        Assert.Equal(legacy.OutputSpaceId, v2.OutputSpaceId);
        Assert.Equal(legacy.OutputInitialization.Capacity, v2.OutputInitialization.Capacity);
        Assert.Equal(legacy.OutputInitialization.FillByte, v2.OutputInitialization.FillByte);
        Assert.Equal(
            legacy.AddressSpaces.OrderBy(static space => space.AddressSpaceId).Select(static space =>
                (space.AddressSpaceId, space.Length, space.Mutability)),
            v2.AddressSpaces.OrderBy(static space => space.AddressSpaceId).Select(static space =>
                (space.AddressSpaceId, space.Length, space.Mutability)));
        Assert.Equal(legacy.OrderedOperations.Count, v2.OrderedOperations.Count);
        for (int index = 0; index < legacy.OrderedOperations.Count; index++)
        {
            CompositionOperation legacyOperation = legacy.OrderedOperations[index];
            CompositionOperation v2Operation = v2.OrderedOperations[index];
            Assert.Equal(legacyOperation.OperationId, v2Operation.OperationId);
            Assert.Equal(legacyOperation.Sequence, v2Operation.Sequence);
            Assert.Equal(legacyOperation.Kind, v2Operation.Kind);
            Assert.Equal(legacyOperation.SourceSpaceId, v2Operation.SourceSpaceId);
            Assert.Equal(legacyOperation.SourceRange, v2Operation.SourceRange);
            Assert.Equal(legacyOperation.TargetSpaceId, v2Operation.TargetSpaceId);
            Assert.Equal(legacyOperation.TargetRange, v2Operation.TargetRange);
            Assert.Equal(legacyOperation.OverlapPolicy, v2Operation.OverlapPolicy);
        }
    }

    private static async ValueTask<CompositionRunResult> PreviewAsync(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, byte[]> inputs)
    {
        var reader = new FakeArtifactReader(inputs.ToDictionary(
            static item => $"nt51920-v2:{item.Key}",
            static item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings = [
            new InputArtifactBinding(
                "tp-input",
                "tp-input",
                "nt51920-v2:tp-input",
                "tp.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "dp-input",
                "dp-input",
                "nt51920-v2:dp-input",
                "dp.bin",
                CompiledInputArtifactClass.DpFirmware),
        ];
        var service = new CompositionRunService(reader, new FakeClock([StartedAtUtc, CompletedAtUtc]));
        var request = new CompositionRunRequest(
            "golden-nt51920-v2",
            compiledComposition,
            bindings,
            compiledComposition.DefaultOutputFileName);
        return await service.PreviewAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    private static Dictionary<string, byte[]> ReadInputs(string goldenRoot, JsonElement inputs)
    {
        return inputs.EnumerateObject().ToDictionary(
            static input => input.Name,
            input => ReadManifestFile(goldenRoot, input.Value),
            StringComparer.Ordinal);
    }

    private static void AssertSchemaSnapshotMatchesContract(
        string repositoryRoot,
        string bundleRoot,
        string schemaFileName)
    {
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(repositoryRoot, "docs", "contracts", schemaFileName)),
            File.ReadAllBytes(Path.Combine(bundleRoot, "schemas", schemaFileName)));
    }

    private static byte[] ReadManifestFile(string goldenRoot, JsonElement manifestFile)
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.ManifestPath(goldenRoot, manifestFile));
        Assert.Equal(manifestFile.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(manifestFile.GetProperty("sha256").GetString(), Hash(bytes));
        return bytes;
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
