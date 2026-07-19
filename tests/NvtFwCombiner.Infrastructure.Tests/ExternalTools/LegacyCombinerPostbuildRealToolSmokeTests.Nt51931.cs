using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    private const string Nt51931ModeParityOutputSha256 =
        "f38fdecd95092d9bacabd8ca59c442cbdb601edd280c23efa088693cb256c594";

    /// <summary>
    /// Locks the owner-selected 1.13.0/NT51931-based route to the reviewed
    /// 1.2.0.4/NT51930-based full-byte control output.
    /// </summary>
    [Fact]
    public async Task Nt51931Based113MatchesReviewed1204ControlOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51931-fw130-cascade6-auto-prj-158-20260718");
        var artifactPaths = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .ToDictionary(
                artifact => artifact.GetProperty("artifactId").GetString()!,
                CanonicalGoldenTestData.ArtifactPath,
                StringComparer.Ordinal);
        byte[] ownerExpected = File.ReadAllBytes(artifactPaths["expected-output"]);
        byte[] ownerExpectedSnapshot = [.. ownerExpected];
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        Assert.Equal(
            manifest.Sha256,
            Sha256(Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName)));

        LegacyCombinerPostbuildProfile profile = PostbuildTestCatalog.Nt51931;
        Assert.Equal(manifest.ToolBindingId, profile.ToolBindingId);
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["6"]);
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
        LegacyCombinerPostbuildCommand command = Assert.Single(plan.Commands);
        Assert.Equal("NT51931BASED_NORMAL_MODE", command.ModeArgument);
        Assert.Equal("CRC8", command.CrcArgument);

        ExternalProcessorStagedArtifact[] artifacts =
        [
            Artifact("postbuild-nf-ctrlram", "nf-ctrlram-input"),
            Artifact("postbuild-normal-ctrlram", "normal-ctrlram-input"),
            Artifact("postbuild-mp-ctrlram", "mp-ctrlram-input"),
            Artifact("postbuild-vn-ctrlram", "vn-ctrlram-input"),
            Artifact("postbuild-diffdlm", "diffdlm-input"),
        ];
        byte[][] artifactSnapshots = [.. artifacts.Select(artifact => artifact.Bytes.ToArray())];
        ByteRange[] expectedChangedRanges =
        [
            new(0x1C, 4),
            new(0xFC, 4),
            new(0x1DA48, 8),
            new(0x1DA6C, 4),
            new(0x1DA7C, 4),
            new(0x1DA8C, 4),
            new(0x1DA9C, 76),
            new(0x1DB2C, 4),
        ];
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-nt51931-mode-parity-{Guid.NewGuid():N}");

        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [profile],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            var request = new ExternalProcessorRequest(
                "nt51931-mode-parity",
                profile.ProcessorId,
                profile.ToolBindingId,
                ownerExpected,
                expectedChangedRanges,
                selection,
                stagedArtifacts: artifacts);

            ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Equal(expectedChangedRanges, result.ChangedRanges);
            Assert.Equal(
                Nt51931ModeParityOutputSha256,
                Convert.ToHexString(SHA256.HashData(result.OutputBytes.Span)).ToLowerInvariant());
            Assert.Equal(108, result.ChangedRanges.Sum(range => range.Length));
            Assert.Equal(ownerExpectedSnapshot, ownerExpected);
            Assert.Equal(ownerExpected, request.InputBytes.ToArray());
            for (int index = 0; index < artifacts.Length; index++)
            {
                Assert.Equal(artifactSnapshots[index], artifacts[index].Bytes.ToArray());
            }
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }

        ExternalProcessorStagedArtifact Artifact(string artifactId, string canonicalArtifactId)
        {
            return new ExternalProcessorStagedArtifact(
                artifactId,
                File.ReadAllBytes(artifactPaths[canonicalArtifactId]));
        }
    }
}
