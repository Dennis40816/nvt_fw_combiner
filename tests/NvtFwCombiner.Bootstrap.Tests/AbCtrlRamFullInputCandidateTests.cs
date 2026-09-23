using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Canonical full CtrlRAM sources exercised against the approved AB Merge base.</summary>
public sealed class AbCtrlRamFullInputCandidateTests
{
    private const string StandardCaseId = "nt51929-fw200-single-auto-prj-594-20260717";
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

    /// <summary>Runs every bank selection with all three canonical CtrlRAM source BINs.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A, "a-only")]
    [InlineData(AbCtrlRamBankSelection.B, "b-only")]
    [InlineData(AbCtrlRamBankSelection.Both, "both")]
    public async Task RealSourcesProduceBankIsolatedCandidate(AbCtrlRamBankSelection selection, string caseName)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        JsonElement canonical = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", StandardCaseId);
        foreach ((string artifactId, string slotId) in new[]
        {
            ("postbuild-nf-ctrlram", "replace-ctrlram-nf"),
            ("postbuild-normal-ctrlram", "replace-ctrlram-normal"),
            ("postbuild-vn-ctrlram", "replace-ctrlram-vn"),
        })
        {
            string path = CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(canonical, artifactId));
            paths[slotId] = path;
            bytes[slotId] = File.ReadAllBytes(path);
        }

        byte[] reference = bytes[CompositionSlotIds.ReplaceBase];
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            state, "NT51929", "single", paths, bytes, new AbCtrlRamDraftState(selection));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        using TempWorkspace workspace = TempWorkspace.Create("ab-full-input-candidate");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, prepared.AcceptedSession!, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] output = result.OutputBytes.ToArray();
        Assert.Equal(0x80000, output.Length);
        Assert.Equal(output, File.ReadAllBytes(workspace.PathFor("result.bin")));
        if (selection == AbCtrlRamBankSelection.A)
        {
            Assert.Equal(reference[0x40000..], output[0x40000..]);
        }
        if (selection == AbCtrlRamBankSelection.B)
        {
            Assert.Equal(reference[..0x40000], output[..0x40000]);
        }

        string? evidenceRoot = Environment.GetEnvironmentVariable("NFC_AB_CTRLRAM_FULL_INPUT_DIR");
        if (string.IsNullOrWhiteSpace(evidenceRoot)) { return; }
        string runId = Environment.GetEnvironmentVariable("NFC_AB_CTRLRAM_RUN_ID")
            ?? throw new InvalidOperationException("NFC_AB_CTRLRAM_RUN_ID is required for candidate evidence.");
        if (!Guid.TryParse(runId, out _))
        {
            throw new InvalidOperationException("NFC_AB_CTRLRAM_RUN_ID must be a GUID.");
        }
        string testArea = Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")
            ?? throw new InvalidOperationException("NFC_TEST_AREA_ROOT is required for private candidate output.");
        string allowed = Path.GetFullPath(testArea).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destination = Path.GetFullPath(Path.Combine(evidenceRoot, caseName));
        if (!destination.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Candidate output must remain inside NFC_TEST_AREA_ROOT.");
        }
        RejectReparsePointAncestors(destination);
        _ = Directory.CreateDirectory(destination);
        RejectReparsePointAncestors(destination);
        using (var binary = new FileStream(Path.Combine(destination, "candidate-output.bin"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            binary.Write(output);
        }
        string report = JsonSerializer.Serialize(new
        {
            status = "candidate only; no independent complete-output Golden",
            runId,
            caseName,
            baseSha256 = Convert.ToHexStringLower(SHA256.HashData(reference)),
            outputSha256 = Convert.ToHexStringLower(SHA256.HashData(output)),
            outputLength = output.Length,
            assemblyDirectory = AppContext.BaseDirectory,
            assemblySha256 = Directory.EnumerateFiles(AppContext.BaseDirectory, "NvtFwCombiner*.dll")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToDictionary(static path => Path.GetFileName(path),
                    static path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                    StringComparer.Ordinal),
        }, EvidenceJsonOptions);
        using var reportFile = new FileStream(Path.Combine(destination, "report.json"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(reportFile);
        writer.Write(report);
    }

    private static void RejectReparsePointAncestors(string directory)
    {
        for (DirectoryInfo? current = new(directory); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Candidate path cannot contain a junction or symbolic link.");
            }
        }
    }
}
