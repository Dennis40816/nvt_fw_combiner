using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared trusted-V2 Standard Merge migration evidence helpers.</summary>
internal static class V2StandardMergeGoldenTestSupport
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 7, 12, 0, 0, 1, TimeSpan.Zero);

    internal static TrustedProfileBundleCatalog LoadCatalog(string bundleRoot, string bundleContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleContentHash);
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        AssertSchemaSnapshotMatchesContract(repositoryRoot, bundleRoot, "firmware-family-v1.schema.json");
        AssertSchemaSnapshotMatchesContract(repositoryRoot, bundleRoot, "composition-profile-v2.schema.json");
        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(bundleContentHash, "built-in-profile-bundle-v2"),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(8, 131072, 262144, 8)));
        return TrustedProfileBundleCatalogProjection.Create(bundle.CreateDocumentProjection());
    }

    internal static CompiledComposition CompileV2(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string icId)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            catalog,
            profileId,
            profileVersion,
            icId,
            ExperienceIds.StandardMerge);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        return composition;
    }

    internal static CompiledComposition CompileLegacy(string icId)
    {
        Profiles.CompositionProfileDefinition legacyProfile = BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
            .Single(profile => StringComparer.Ordinal.Equals(profile.IcId, icId));
        ProfileCompileResult legacyCompilation = CompositionProfileCompiler.Compile(legacyProfile, []);
        Assert.True(legacyCompilation.IsSuccess, FormatIssues(legacyCompilation.Issues));
        return Assert.IsType<CompiledComposition>(legacyCompilation.CompiledComposition);
    }

    internal static JsonElement ReadGoldenCase(string referenceIc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceIc);
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenRoot, "manifest.json")));
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
        var reader = new FakeArtifactReader(inputs.ToDictionary(
            item => $"{compiledComposition.ProfileId}:{item.Key}",
            static item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings =
        [
            .. inputs.Keys.Order(StringComparer.Ordinal)
                .Select(addressSpaceId => CreateInputBinding(compiledComposition.ProfileId, addressSpaceId)),
        ];
        var service = new CompositionRunService(reader, new FakeClock([StartedAtUtc, CompletedAtUtc]));
        var request = new CompositionRunRequest(
            $"golden-{compiledComposition.IcId.ToLowerInvariant()}",
            compiledComposition,
            bindings,
            compiledComposition.DefaultOutputFileName);
        return await service.PreviewAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    internal static void AssertPlanGeometryAndOperationParity(CompositionPlan legacy, CompositionPlan v2)
    {
        Assert.Equal(legacy.OutputSpaceId, v2.OutputSpaceId);
        Assert.Equal(legacy.OutputInitialization.Capacity, v2.OutputInitialization.Capacity);
        Assert.Equal(legacy.OutputInitialization.FillByte, v2.OutputInitialization.FillByte);
        AddressSpace[] legacySpaces = [.. legacy.AddressSpaces.OrderBy(static space => space.AddressSpaceId)];
        AddressSpace[] v2Spaces = [.. v2.AddressSpaces.OrderBy(static space => space.AddressSpaceId)];
        Assert.Equal(legacySpaces.Length, v2Spaces.Length);
        foreach ((AddressSpace legacySpace, AddressSpace v2Space) in legacySpaces.Zip(v2Spaces))
        {
            Assert.Equal(legacySpace.AddressSpaceId, v2Space.AddressSpaceId);
            Assert.Equal(legacySpace.Length, v2Space.Length);
            Assert.Equal(legacySpace.Mutability, v2Space.Mutability);
        }
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

    internal static void AssertSuccessfulGoldenOutput(
        CompositionRunResult result,
        CompiledComposition compiledComposition,
        byte[] expectedOutput)
    {
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(expectedOutput, result.OutputBytes.ToArray());
        Assert.Equal(Hash(expectedOutput), result.Report.Output.Sha256);
        Assert.Equal(compiledComposition.CompilationFingerprint, result.Report.CompilationFingerprint);
    }

    internal static string GoldenRoot => Path.Combine(
        RepositoryPaths.FindRepositoryRoot(),
        "testdata",
        "golden",
        "standard-merge-gen-flash");

    private static void AssertSchemaSnapshotMatchesContract(
        string repositoryRoot,
        string bundleRoot,
        string schemaFileName)
    {
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(repositoryRoot, "docs", "contracts", schemaFileName)),
            File.ReadAllBytes(Path.Combine(bundleRoot, "schemas", schemaFileName)));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }

    private static InputArtifactBinding CreateInputBinding(string profileId, string addressSpaceId)
    {
        (string originalFileName, CompiledInputArtifactClass artifactClass) = addressSpaceId switch
        {
            "dp-input" => ("dp.bin", CompiledInputArtifactClass.DpFirmware),
            "tp-input" => ("tp.bin", CompiledInputArtifactClass.TpFirmware),
            "ld-input" => ("ld.bin", CompiledInputArtifactClass.Auxiliary),
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
