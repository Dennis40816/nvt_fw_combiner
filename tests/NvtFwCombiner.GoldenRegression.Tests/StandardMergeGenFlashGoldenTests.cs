using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Owner-approved golden regression tests for gen_flash standard merge profiles.</summary>
public sealed class StandardMergeGenFlashGoldenTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 6, 30, 0, 0, 1, TimeSpan.Zero);

    /// <summary>Verifies every gen_flash IC profile matches full-byte golden BIN output.</summary>
    [Fact]
    public async Task GenFlashStandardMergeProfilesMatchOwnerApprovedGoldenBytes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using JsonDocument manifestDocument = LoadJson(Path.Combine(goldenRoot, "manifest.json"));
        using JsonDocument configDocument = LoadJson(Path.Combine(goldenRoot, "test_ic_config.json"));

        JsonElement manifest = manifestDocument.RootElement;
        Assert.Equal("owner-approved-golden-firmware", manifest.GetProperty("payloadClass").GetString());
        Assert.True(manifest.GetProperty("binaryPayloadsIncluded").GetBoolean());
        VerifyManifestFile(goldenRoot, manifest.GetProperty("supportingFiles").GetProperty("test_ic_config"));

        JsonElement[] cases = [.. manifest.GetProperty("cases").EnumerateArray().Select(item => item.Clone())];
        string[] configIcIds = [
            .. configDocument.RootElement.EnumerateObject()
                .Where(property => property.Name != "global")
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(configIcIds, cases.Select(item => item.GetProperty("ic").GetString()).Order(StringComparer.Ordinal));

        foreach (JsonElement goldenCase in cases.OrderBy(item => item.GetProperty("ic").GetString(), StringComparer.Ordinal))
        {
            await VerifyGoldenCaseAsync(goldenRoot, goldenCase);
        }
    }

    /// <summary>Verifies owner-confirmed alias profiles match the referenced gen_flash golden output bytes.</summary>
    [Theory]
    [InlineData("51917", "51927")]
    [InlineData("51919", "51929")]
    public async Task OwnerConfirmedAliasProfilesMatchReferenceGoldenBytes(string aliasIc, string referenceIc)
    {
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        using JsonDocument manifestDocument = LoadJson(Path.Combine(goldenRoot, "manifest.json"));
        JsonElement referenceCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == referenceIc)
            .Clone();
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.OwnerConfirmedAliasStandardMergeProfiles
            .Single(item => item.IcId == $"NT{aliasIc}");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        Dictionary<string, byte[]> inputs = ReadInputs(goldenRoot, referenceCase.GetProperty("inputs"));
        byte[] expectedOutput = ReadManifestFile(goldenRoot, referenceCase.GetProperty("expectedOutput"));
        CompositionRunResult result = await PreviewAsync(profile, compile.Plan!, inputs);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(expectedOutput, result.OutputBytes.ToArray());
        Assert.Equal($"nt{aliasIc}-standard-merge-gen-flash-alias", result.Report.ProfileId);
        Assert.Equal($"NT{aliasIc}", result.Report.IcId);
        Assert.Equal(Sha256Hex(expectedOutput), result.Report.Output.Sha256);
    }

    private static async ValueTask VerifyGoldenCaseAsync(string goldenRoot, JsonElement goldenCase)
    {
        string ic = goldenCase.GetProperty("ic").GetString()!;
        string profileId = goldenCase.GetProperty("profileId").GetString()!;
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles
            .Single(item => item.ProfileId == profileId && item.IcId == $"NT{ic}");
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        Assert.True(compile.IsSuccess, FormatIssues(compile.Issues));

        Dictionary<string, byte[]> inputs = ReadInputs(goldenRoot, goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = ReadManifestFile(goldenRoot, goldenCase.GetProperty("expectedOutput"));
        CompositionRunResult result = await PreviewAsync(profile, compile.Plan!, inputs).ConfigureAwait(false);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(expectedOutput, result.OutputBytes.ToArray());
        Assert.Equal(Sha256Hex(expectedOutput), result.Report.Output.Sha256);
        Assert.Equal(expectedOutput.LongLength, result.Report.Output.Size);
        Assert.Equal(profileId, result.Report.ProfileId);
        Assert.Equal(
            inputs.Keys.Order(StringComparer.Ordinal),
            result.Report.Inputs.Select(input => input.AddressSpaceId).Order(StringComparer.Ordinal));
    }

    private static async ValueTask<CompositionRunResult> PreviewAsync(
        CompositionProfileDefinition profile,
        CompositionPlan plan,
        IReadOnlyDictionary<string, byte[]> inputs)
    {
        var reader = new FakeArtifactReader(inputs.ToDictionary(
            item => $"{profile.ProfileId}:{item.Key}",
            item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings = [
            .. inputs.Keys
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => new InputArtifactBinding(
                    addressSpaceId,
                    addressSpaceId,
                    $"{profile.ProfileId}:{addressSpaceId}")),
        ];
        var service = new CompositionRunService(reader, new FakeClock());
        var request = new CompositionRunRequest(
            $"golden-{profile.IcId.ToLowerInvariant()}",
            ToRunProfile(profile),
            plan,
            bindings,
            profile.DefaultOutputFileName);

        return await service.PreviewAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    private static CompositionRunProfile ToRunProfile(CompositionProfileDefinition profile)
    {
        return new CompositionRunProfile(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind,
            profile.IcNumberInputMode);
    }

    private static Dictionary<string, byte[]> ReadInputs(string goldenRoot, JsonElement inputs)
    {
        return inputs.EnumerateObject()
            .ToDictionary(
                input => input.Name,
                input => ReadManifestFile(goldenRoot, input.Value),
                StringComparer.Ordinal);
    }

    private static void VerifyManifestFile(string goldenRoot, JsonElement manifestFile)
    {
        _ = ReadManifestFile(goldenRoot, manifestFile);
    }

    private static byte[] ReadManifestFile(string goldenRoot, JsonElement manifestFile)
    {
        string relativePath = manifestFile.GetProperty("path").GetString()!;
        string fullPath = Path.Combine(goldenRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        byte[] bytes = File.ReadAllBytes(fullPath);

        Assert.Equal(manifestFile.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(manifestFile.GetProperty("sha256").GetString(), Sha256Hex(bytes));
        return bytes;
    }

    private static JsonDocument LoadJson(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SPEC.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }

    private sealed class FakeArtifactReader : IArtifactReader
    {
        private readonly IReadOnlyDictionary<string, byte[]> _artifacts;

        internal FakeArtifactReader(IReadOnlyDictionary<string, byte[]> artifacts)
        {
            _artifacts = artifacts;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_artifacts[artifactId]);
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        private int _callCount;

        public DateTimeOffset UtcNow => _callCount++ == 0 ? StartedAtUtc : CompletedAtUtc;
    }
}
