using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Exact V2 route evidence from NT51929 Common FW 2.0.0 owner artifacts.</summary>
public sealed class Nt51929CtrlRamFw200SingleEvidenceTests
{
    private const string OwnerExpectedSha256 = "d3c958d2aac1e29bd1f88b8ac62dc74c36810ab11e707770199d4b34f5ce3910";
    private const string CurrentOutputSha256 = "d23f53a13db3c6fc0009ed547e8cfa5f1b54033145825828416c7458b14f198f";
    private const string RegisteredCombinerSha256 = "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf";
    private const string CasePath = "fixtures/20260717/NT51929/replace/ctrlram/single/";
    private const int Capacity = 0x40000;
    private const int NfStart = 0x1FC00;
    private const int NfMaximumLength = 0x1F90;
    private const int NormalStart = 0x21B90;
    private const int NormalLength = 0x4A00;
    private const int VnStart = 0x26590;
    private const int VnMaximumLength = 0x1960;
    private const int HeaderCopyStart = 0x27EF0;
    private const int HeaderCopyLength = 0x200;

    /// <summary>Locks the true non-AB single golden, Standard Merge reconstruction, and admission metadata.</summary>
    [Theory]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash")]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias")]
    public async Task StandardMergeReconstructsTrueSingleGoldenAndFirmwareContextAsync(
        string icId,
        string expectedProfileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-base");
        string outputPath = workspace.PathFor("standard-merge-base.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            icId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = evidence.Dp.Path,
                [CompositionAddressSpaceIds.TpInput] = evidence.Tp.Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(expectedProfileId, ReadProfileId(result.ReportJson));
        Assert.Equal(OwnerExpectedSha256, Hash(File.ReadAllBytes(outputPath)));
        Assert.Equal(OwnerExpectedSha256, Hash(evidence.Expected.Bytes));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(evidence.Expected.Bytes, out FirmwareConfigMetadata metadata));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
        Assert.Equal(1, metadata.ChipNumber);
        Assert.Equal(0x4703, metadata.ProjectId);

        WorkbenchFirmwareContextSuggestion suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            WorkbenchCompositionService.TryReadFirmwareContextSuggestion(icId, outputPath));
        Assert.Equal("single", suggestion.NumberToken);
        Assert.Equal(metadata.CommonFwVersion, suggestion.CommonFwVersion);
        Assert.Equal(metadata.ChipNumber, suggestion.ChipNumber);
        Assert.Equal(metadata.ProjectId, suggestion.ProjectId);
        Assert.Equal(
            "c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2",
            Hash(evidence.AbExpected.Bytes));
        Assert.Equal(0x80000, evidence.AbExpected.Bytes.Length);
    }

    /// <summary>Proves the exact V2 route produces the locked full output and process evidence.</summary>
    [Theory]
    [InlineData("NT51929", "nt51929-ctrlram-replace-fw200-single", "nfc.nt51929.ctrlram-postbuild-v1")]
    [InlineData("NT51919", "nt51919-ctrlram-replace-fw200-single", "nfc.nt51919.ctrlram-postbuild-v1")]
    public async Task V2ProducesLockedExactOutputWithCrcOnlyGoldenDeviationAsync(
        string icId,
        string expectedProfileId,
        string expectedProcessorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase evidence = ReadOwnerCase();
        var immutableHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.RelativePath,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-v2");
        IReadOnlyDictionary<string, string> slots = CreateSlotPaths(evidence, evidence.Expected.Path);
        string v2Path = workspace.PathFor("v2.bin");
        WorkbenchRunResult v2 = await WorkbenchCompositionService.RunReplaceAsync(
            icId, "single", WorkbenchReplaceModes.CtrlRam, slots, true,
            TestContext.Current.CancellationToken, v2Path);

        Assert.True(v2.Succeeded, v2.ReportJson);
        byte[] v2Bytes = File.ReadAllBytes(v2Path);
        Assert.Equal(CurrentOutputSha256, Hash(v2Bytes));
        AssertGoldenDifferenceClassification(evidence.Expected.Bytes, v2Bytes);
        AssertPhysicalInputProjection(evidence, v2Bytes);

        using var v2Report = JsonDocument.Parse(v2.ReportJson);
        AssertReportIdentity(v2Report.RootElement, expectedProfileId, icId);
        AssertOversizedNormalInputWarning(v2Report.RootElement);
        AssertProcessEvidence(v2Report.RootElement, icId, expectedProcessorId);
        Assert.All(
            v2Report.RootElement.GetProperty("OutputDifferences").EnumerateArray(),
            difference =>
            {
                Assert.True(difference.GetProperty("IsAccepted").GetBoolean());
                Assert.Equal("PostbuildCrcHeader", difference.GetProperty("Classification").GetString());
            });
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Proves another base, project, version, count, or selector fails closed.</summary>
    [Theory]
    [InlineData("NT51929", "base")]
    [InlineData("NT51929", "pid")]
    [InlineData("NT51929", "version")]
    [InlineData("NT51929", "chip")]
    [InlineData("NT51929", "selector")]
    [InlineData("NT51919", "base")]
    [InlineData("NT51919", "pid")]
    [InlineData("NT51919", "version")]
    [InlineData("NT51919", "chip")]
    [InlineData("NT51919", "selector")]
    public async Task UnreviewedShapesFailClosedAsync(string icId, string mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        OwnerCase evidence = ReadOwnerCase();
        var immutableHashes = evidence.Artifacts.ToDictionary(
            static artifact => artifact.RelativePath,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-negative-route");
        string referencePath = workspace.PathFor("reference.bin");
        byte[] reference = [.. evidence.Expected.Bytes];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.FirmwareConfigStart);
        string number = "single";
        switch (mutation)
        {
            case "base":
                reference[0x100] ^= 0x01;
                break;
            case "pid":
                BinaryPrimitives.WriteUInt16LittleEndian(
                    reference.AsSpan(start + FirmwareConfigLayout.ProjectIdOffset),
                    0xFFFF);
                break;
            case "version":
                reference[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset]++;
                break;
            case "chip":
                reference[start + FirmwareConfigLayout.ChipNumberOffset] = 2;
                break;
            case "selector":
                number = "1";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown route mutation.");
        }

        File.WriteAllBytes(referencePath, reference);
        string outputPath = workspace.PathFor("unsupported.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId, number, CreateSlotPaths(evidence, referencePath), true,
            outputPath, null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(Hash(reference), Hash(File.ReadAllBytes(referencePath)));
        Assert.All(
            evidence.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.RelativePath], Hash(File.ReadAllBytes(artifact.Path))));
    }

    /// <summary>Proves accepted NT51929 identifiers select the same exact V2 route.</summary>
    [Theory]
    [InlineData("51929", "NT51929", "nt51929-ctrlram-replace-fw200-single")]
    [InlineData("nt51929", "NT51929", "nt51929-ctrlram-replace-fw200-single")]
    [InlineData(" NT51929 ", "NT51929", "nt51929-ctrlram-replace-fw200-single")]
    [InlineData("51919", "NT51919", "nt51919-ctrlram-replace-fw200-single")]
    [InlineData("nt51919", "NT51919", "nt51919-ctrlram-replace-fw200-single")]
    [InlineData(" NT51919 ", "NT51919", "nt51919-ctrlram-replace-fw200-single")]
    public async Task AcceptedIcAliasesSelectExactV2RouteAsync(
        string icId,
        string canonicalIcId,
        string expectedProfileId)
    {
        OwnerCase evidence = ReadOwnerCase();
        using var workspace = TempWorkspace.Create("nfc-nt51929-fw200-alias");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            icId, "single", CreateSlotPaths(evidence, evidence.Expected.Path), true,
            workspace.PathFor("alias.bin"), null, new PassThroughProcessor(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        AssertReportIdentity(report.RootElement, expectedProfileId, canonicalIcId);
    }

    private static void AssertGoldenDifferenceClassification(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        ByteRange[] crcWords = [new(0x7100, 4), new(0x7118, 4), new(0x27FF0, 4), new(0x28008, 4)];
        Assert.Equal(15, CountDifferences(expected, actual));
        Assert.Equal(4, CountDifferences(expected, actual, crcWords[0]));
        Assert.Equal(3, CountDifferences(expected, actual, crcWords[1]));
        Assert.Equal(4, CountDifferences(expected, actual, crcWords[2]));
        Assert.Equal(4, CountDifferences(expected, actual, crcWords[3]));
        Assert.Equal(0, CountDifferencesOutside(expected, actual, crcWords));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NfStart, NfMaximumLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(NormalStart, NormalLength)));
        Assert.Equal(0, CountDifferences(expected, actual, new ByteRange(VnStart, VnMaximumLength)));
    }

    private static void AssertPhysicalInputProjection(OwnerCase evidence, byte[] output)
    {
        OwnerArtifact nf = evidence.Require("NF_Ctrlram.bin");
        OwnerArtifact normal = evidence.Require("Normal_Ctrlram.bin");
        OwnerArtifact vn = evidence.Require("VN_Ctrlram.bin");
        Assert.Equal(nf.Bytes, output.AsSpan(NfStart, nf.Bytes.Length).ToArray());
        Assert.Equal(normal.Bytes.AsSpan(0, NormalLength).ToArray(), output.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.Equal(vn.Bytes, output.AsSpan(VnStart, vn.Bytes.Length).ToArray());
    }

    private static void AssertProcessEvidence(
        JsonElement report,
        string icId,
        string expectedProcessorId)
    {
        JsonElement session = Assert.Single(ReadProcessorSessions(report));
        Assert.Equal(ExpectedArguments(icId), ReadNormalizedArguments(session));
        Assert.Equal(2, session.GetProperty("ExecutedCommands").GetArrayLength());
        AssertProcessorIdentity(session, expectedProcessorId);
        Assert.Equal([new ByteRange(0, Capacity)], ReadRanges(session, "ProcessorAllowedReadRanges"));
        ByteRange[] expectedWrites = [
            new(0x7100, 4), new(0x7118, 4), new(NfStart, 1624), new(NormalStart, NormalLength),
            new(VnStart, VnMaximumLength), new(HeaderCopyStart, HeaderCopyLength),
        ];
        Assert.Equal(expectedWrites, ReadRanges(session, "ProcessorAllowedWriteRanges"));
        string executable = session.GetProperty("ExecutedCommands")[0].GetProperty("ExecutablePath").GetString()!;
        Assert.Equal(RegisteredCombinerSha256, Hash(File.ReadAllBytes(executable)));
    }

    private static void AssertWorkflowNotSupported(WorkbenchRunResult result, string outputPath)
    {
        Assert.False(result.Succeeded, result.ReportJson);
        Assert.Null(result.CommittedOutputId);
        Assert.False(File.Exists(outputPath));
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == WorkbenchIssueCodes.ReplaceWorkflowNotSupported);
        Assert.False(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

    private static string[][] ExpectedArguments(string icId)
    {
        string firmware = $"output/{icId.ToLowerInvariant()}_fw.bin";
        return [
            [
                "NT51932BASED_NORMAL_MODE", "CRC8", firmware, firmware,
                "BIN/Normal_Ctrlram.bin", "0x0", "0x21B90", "18944",
                "BIN/VN_Ctrlram.bin", "0x0", "0x26590", "6496",
                "BIN/NF_Ctrlram.bin", "0x0", "0x1FC00", "8080",
                firmware, "0x7000", "0x27EF0", "512",
            ],
            [
                "NT51932BASED_NORMAL_MODE", "CRC8", firmware, firmware,
                firmware, "0x7000", "0x27EF0", "512",
            ],
        ];
    }

    private static JsonElement[] ReadProcessorSessions(JsonElement report)
    {
        return [
            .. report.GetProperty("Operations").EnumerateArray().Where(operation =>
                StringComparer.Ordinal.Equals(operation.GetProperty("Kind").GetString(), "RunExternalProcessor")),
        ];
    }

    private static string[][] ReadNormalizedArguments(JsonElement session)
    {
        return [
            .. session.GetProperty("ExecutedCommands").EnumerateArray().Select(command =>
            {
                string workingDirectory = command.GetProperty("WorkingDirectory").GetString()!;
                return command.GetProperty("Arguments").EnumerateArray()
                    .Select(argument => argument.GetString()!)
                    .Select(argument => Path.IsPathRooted(argument)
                        ? Path.GetRelativePath(workingDirectory, argument).Replace('\\', '/')
                        : argument.Replace('\\', '/'))
                    .ToArray();
            }),
        ];
    }

    private static ByteRange[] ReadRanges(JsonElement operation, string propertyName)
    {
        return [
            .. operation.GetProperty(propertyName).EnumerateArray()
                .Select(range => new ByteRange(range.GetProperty("Start").GetInt64(), range.GetProperty("Length").GetInt64()))
                .OrderBy(static range => range.Start),
        ];
    }

    private static void AssertProcessorIdentity(JsonElement session, string expectedProcessorId)
    {
        Assert.Equal(expectedProcessorId, session.GetProperty("ProcessorId").GetString());
        Assert.Equal("legacy-combiner-1.13.0", session.GetProperty("ToolBindingId").GetString());
        Assert.Equal("Succeeded", session.GetProperty("Status").GetString());
    }

    private static void AssertReportIdentity(JsonElement report, string profileId, string icId)
    {
        Assert.Equal(profileId, report.GetProperty("ProfileId").GetString());
        Assert.Equal(icId, report.GetProperty("IcId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ModeId").GetString());
        Assert.Equal("ctrlram-replace", report.GetProperty("ExperienceId").GetString());
        Assert.Equal("Replace", report.GetProperty("CompositionKind").GetString());
    }

    private static void AssertOversizedNormalInputWarning(JsonElement report)
    {
        JsonElement issue = Assert.Single(report.GetProperty("Issues").EnumerateArray());
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceTruncated, issue.GetProperty("Code").GetString());
        Assert.Equal(CompositionIssueSeverity.Warning, issue.GetProperty("Severity").GetString());
        Assert.Equal("replace-ctrlram-normal", issue.GetProperty("OperationId").GetString());
        string message = issue.GetProperty("Message").GetString()!;
        Assert.Contains("655360", message, StringComparison.Ordinal);
        Assert.Contains("18944", message, StringComparison.Ordinal);
        Assert.Contains("636416 trailing bytes were discarded", message, StringComparison.Ordinal);
    }

    private static string ReadProfileId(string reportJson)
    {
        using var report = JsonDocument.Parse(reportJson);
        return report.RootElement.GetProperty("ProfileId").GetString()!;
    }

    private static Dictionary<string, string> CreateSlotPaths(OwnerCase evidence, string referencePath)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-normal"] = evidence.Require("Normal_Ctrlram.bin").Path,
            ["replace-ctrlram-vn"] = evidence.Require("VN_Ctrlram.bin").Path,
            ["replace-ctrlram-nf"] = evidence.Require("NF_Ctrlram.bin").Path,
        };
    }

    private static OwnerCase ReadOwnerCase()
    {
        string root = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ctrlram-replace");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.20260717.json")));
        OwnerArtifact[] artifacts = [
            .. manifest.RootElement.GetProperty("payloads").EnumerateArray()
                .Where(item => item.GetProperty("path").GetString()!.StartsWith(CasePath, StringComparison.Ordinal))
                .Select(item => ReadArtifact(root, item)),
        ];
        return new OwnerCase(
            artifacts,
            artifacts.Single(static artifact => artifact.Role == "standard-merge-dp-input"),
            artifacts.Single(static artifact => artifact.Role == "standard-merge-tp-input"),
            artifacts.Single(static artifact => artifact.Role == "expected-final-output-single"),
            artifacts.Single(static artifact => artifact.Role == "expected-final-output-ab"));
    }

    private static OwnerArtifact ReadArtifact(string root, JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(root, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new(
            entry.GetProperty("originalFileName").GetString()!,
            entry.GetProperty("path").GetString()!,
            entry.GetProperty("role").GetString()!,
            path,
            bytes);
    }

    private static long CountDifferences(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        return CountDifferences(expected, actual, new ByteRange(0, Math.Min(expected.Length, actual.Length))) +
            Math.Abs(expected.Length - actual.Length);
    }

    private static long CountDifferences(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, ByteRange range)
    {
        long count = 0;
        for (int index = checked((int)range.Start); index < checked((int)range.EndExclusive); index++)
        {
            count += expected[index] == actual[index] ? 0 : 1;
        }

        return count;
    }

    private static long CountDifferencesOutside(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        IReadOnlyList<ByteRange> allowed)
    {
        long count = 0;
        for (int index = 0; index < Math.Min(expected.Length, actual.Length); index++)
        {
            count += expected[index] != actual[index] && !allowed.Any(range => range.Contains(index)) ? 1 : 0;
        }

        return count + Math.Abs(expected.Length - actual.Length);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class PassThroughProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, []));
        }
    }

    private sealed record OwnerArtifact(string FileName, string RelativePath, string Role, string Path, byte[] Bytes);

    private sealed record OwnerCase(
        IReadOnlyList<OwnerArtifact> Artifacts,
        OwnerArtifact Dp,
        OwnerArtifact Tp,
        OwnerArtifact Expected,
        OwnerArtifact AbExpected)
    {
        public OwnerArtifact Require(string fileName)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.FileName, fileName));
        }
    }
}
