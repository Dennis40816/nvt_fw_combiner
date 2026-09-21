using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    private static readonly JsonSerializerOptions Nt51929EvidenceJsonOptions = new() { WriteIndented = true };

    /// <summary>Characterizes B address strategies without certifying either as a firmware golden.</summary>
    [Fact]
    public async Task Nt51929AbSameContentControlsPreserveRealToolStrategyEvidence()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("NT51929 characterization requires the real Windows Combiner executable.");
        }

        string toolRoot = Path.Combine(RepositoryPaths.FindRepositoryRoot(), "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        Assert.Equal(manifest.Sha256,
            Sha256(Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName)));
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "ab-merge", "NT51929", "expected-output", "t05-d06"));
        byte[] aInput = reference[..0x40000];
        Assert.Equal("e257e734a63d0d8a0e471bc7b541366578b9b56c94dd914197508d5af1127c12",
            Convert.ToHexString(SHA256.HashData(aInput)).ToLowerInvariant());
        byte[] bInput = ShiftNt51929StoredAddresses(aInput, 0x40000);
        byte[] normalizedInput = ShiftNt51929StoredAddresses(bInput, -0x40000);
        Assert.Equal(aInput, normalizedInput);

        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile("NT51929", out LegacyCombinerPostbuildProfile? profile));
        var selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]);
        LegacyCombinerPostbuildCommandPlan plan = profile!.ResolvePlan(selection);
        // NF first-byte replacement also propagates to 0x2EA00 outside this plan's
        // authority (retained failed experiment). Use Normal to isolate bank addresses.
        TpFlashMapRegion region = BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(
            "NT51929", selection, profile).Single(item => item.RegionId == "normal");
        Assert.True(region.Range.Contains(new ByteRange(0x21B90, 1)));
        byte[] replacement = aInput.AsSpan((int)region.Range.Start, (int)region.Range.Length).ToArray();
        replacement[0] ^= 0x5A;
        ByteRange[] allowedWrites = [.. LegacyCombinerPostbuildPlanCompiler
            .GetAllowedWriteRangeSectionsForStagedSources(plan, aInput.Length, [region.Range],
                LegacyCombinerPostbuildPlanCompiler.GetStagedFileBlocks(plan).Select(block => block.FirmwareRange))
            .Select(section => section.Range)];
        ByteRange[] integrityWords = [.. LegacyCombinerPostbuildPlanCompiler
            .GetKnownIntegrityWriteRangeSections(plan, aInput.Length).Select(section => section.Range)];
        ByteRange[] addressWords = [new(0x7164, 4), new(0x7168, 4), new(0x716C, 4)];
        // Compare only exact fields, never the entire allowed header-copy destination.
        ByteRange[] permittedDifferences = [.. integrityWords, .. addressWords,
            .. integrityWords.Concat(addressWords).Where(range => new ByteRange(0x7000, 0x200).Contains(range))
                .Select(range => new ByteRange(0x27EF0 + range.Start - 0x7000, range.Length))];
        using TempWorkspace workspace = TempWorkspace.Create("nfc-nt51929-ab-strategies");
        string? evidenceParent = Environment.GetEnvironmentVariable("NFC_AB_CTRLRAM_EVIDENCE_DIR");
        string evidenceRoot = evidenceParent is null ? workspace.PathFor("evidence")
            : Path.Combine(evidenceParent, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(evidenceRoot);
        List<object> cases = [];
        var runner = new Nt51929EvidenceRunner(evidenceRoot, plan.ProtocolPlan.TargetFileName);
        var processor = new LegacyCombinerPostbuildProcessor(
            new ExternalCombinerToolRegistry([manifest]), toolRoot, workspace.PathFor("staging"), runner);

        async Task<ExternalProcessorResult> RunAsync(string name, byte[] input)
        {
            byte[] before = [.. input];
            File.WriteAllBytes(Path.Combine(evidenceRoot, name + "-input.bin"), input);
            ExternalProcessorResult result = await processor.TransformAsync(new ExternalProcessorRequest(
                name, profile.ProcessorId, profile.ToolBindingId, input, allowedWrites, selection,
                [new ExternalProcessorStagedSource(region.Range, replacement)],
                protocolPlan: plan.ProtocolPlan), CancellationToken.None);
            Assert.Equal(before, input);
            if (result.Succeeded)
            {
                File.WriteAllBytes(Path.Combine(evidenceRoot, name + "-output.bin"), result.OutputBytes.ToArray());
            }

            cases.Add(new
            {
                name,
                result.Succeeded,
                result.Issues,
                result.ExecutedCommands,
                input = Nt51929Snapshot(input),
                output = result.Succeeded ? Nt51929Snapshot(result.OutputBytes.ToArray()) : null,
                changedRanges = result.ChangedRanges
            });
            return result;
        }

        ExternalProcessorResult a = await RunAsync("a-baseline", aInput);
        ExternalProcessorResult direct = await RunAsync("b-direct", bInput);
        ExternalProcessorResult normalized = await RunAsync("b-normalized", normalizedInput);
        byte[]? restored = normalized.Succeeded
            ? ShiftNt51929StoredAddresses(normalized.OutputBytes.ToArray(), 0x40000) : null;
        if (restored is not null)
        {
            File.WriteAllBytes(Path.Combine(evidenceRoot, "b-restored.bin"), restored);
        }

        object? Compare(byte[]? output)
        {
            if (output is null || !a.Succeeded)
            {
                return null;
            }

            byte[] baseline = a.OutputBytes.ToArray();
            IReadOnlyList<ByteRange> differences = ByteDiff.FindChangedRanges(baseline, output);
            int[] outside = [.. Enumerable.Range(0, baseline.Length).Where(index => baseline[index] != output[index]
                && !permittedDifferences.Any(range => range.Contains(new ByteRange(index, 1))))];
            return new { differences, outsidePermittedFieldOffsets = outside };
        }

        File.WriteAllText(Path.Combine(evidenceRoot, "report.json"), JsonSerializer.Serialize(new
        {
            status = "characterization-only; no AB Replace golden or production admission",
            referenceSha256 = Convert.ToHexString(SHA256.HashData(reference)),
            referenceSlice = new ByteRange(0, 0x40000),
            manifest.ToolVersion,
            manifest.Sha256,
            syntheticB = "same A slice; add 0x40000 only at U32 LE 0x7164/0x7168/0x716C",
            replacementRange = region.Range,
            replacementFirstByte = replacement[0],
            allowedWrites,
            permittedDifferences,
            cases,
            directComparison = Compare(direct.Succeeded ? direct.OutputBytes.ToArray() : null),
            restoredComparison = Compare(restored),
            restored = restored is null ? null : Nt51929Snapshot(restored),
        }, Nt51929EvidenceJsonOptions));

        // These establish the experiment's valid control, not the correctness of a B strategy.
        Assert.True(a.Succeeded, string.Join("; ", a.Issues.Select(issue => issue.Message)));
        Assert.True(normalized.Succeeded, string.Join("; ", normalized.Issues.Select(issue => issue.Message)));
        Assert.Equal(aInput.Length, a.OutputBytes.Length);
        Assert.Equal(replacement[0], a.OutputBytes.Span[(int)region.Range.Start]);
        Assert.Equal(a.OutputBytes.ToArray(), normalized.OutputBytes.ToArray());
        if (direct.Succeeded)
        {
            Assert.Equal(bInput.Length, direct.OutputBytes.Length);
            Assert.Equal(replacement[0], direct.OutputBytes.Span[(int)region.Range.Start]);
        }

        Assert.Equal(reference[..0x40000], aInput);
    }

    private static byte[] ShiftNt51929StoredAddresses(byte[] input, int delta)
    {
        byte[] output = [.. input];
        foreach (int offset in new[] { 0x7164, 0x7168, 0x716C })
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(offset, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(offset, 4), checked((uint)(value + delta)));
        }

        return output;
    }

    private static object Nt51929Snapshot(byte[] bytes)
    {
        int[] fields = [0x7100, 0x710C, 0x7118, 0x7164, 0x7168, 0x716C,
            0x27FF0, 0x27FFC, 0x28008, 0x28054, 0x28058, 0x2805C];
        return new
        {
            bytes.Length,
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            fields = fields.ToDictionary(offset => $"0x{offset:X}", offset =>
                offset + 4 <= bytes.Length ? Convert.ToHexString(bytes.AsSpan(offset, 4)) : "unavailable")
        };
    }

    private sealed class Nt51929EvidenceRunner(string evidenceRoot, string targetFileName) : IExternalProcessRunner
    {
        private readonly SystemExternalProcessRunner _inner = new();
        private int _commandNumber;

        public async ValueTask<ExternalProcessResult> RunAsync(
            ExternalProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            string stem = Path.Combine(evidenceRoot,
                $"{Path.GetFileName(startInfo.WorkingDirectory)}-command-{++_commandNumber}");
            string firmwarePath = Path.Combine(startInfo.WorkingDirectory, "output", targetFileName);
            byte[] before = File.ReadAllBytes(firmwarePath);
            File.WriteAllBytes(stem + "-before.bin", before);
            ExternalProcessResult result = await _inner.RunAsync(startInfo, cancellationToken);
            byte[]? after = File.Exists(firmwarePath) ? File.ReadAllBytes(firmwarePath) : null;
            if (after is not null)
            {
                File.WriteAllBytes(stem + "-after.bin", after);
            }

            File.WriteAllText(stem + ".json", JsonSerializer.Serialize(new
            {
                invocation = startInfo.ToExecutedCommand(),
                result,
                before = Nt51929Snapshot(before),
                after = after is null ? null : Nt51929Snapshot(after),
                changedRanges = after?.Length == before.Length ? ByteDiff.FindChangedRanges(before, after) : null,
            }, Nt51929EvidenceJsonOptions));
            return result;
        }
    }
}
