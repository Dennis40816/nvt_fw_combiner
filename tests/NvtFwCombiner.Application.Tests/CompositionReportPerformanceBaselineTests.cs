using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Deterministic upstream report facts for fragmented Replace output.</summary>
public sealed class CompositionReportPerformanceBaselineTests
{
    private const int DifferenceCount = 10_000;
    private const int BaselineJsonCharacterCount = 15_868_142;
    private const string BaselineOutputSha256 = "e7b39a736b02c1793f1c22ab4c21e29bc478bd94465614c27bd70c4ac42c25b4";
    private const string BaselineReportJsonSha256 = "25962675c8c3a24ba1e99f882a5032ac5f93ec2557f0545cb1f23bda1d32e312";
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = StartedAtUtc.AddSeconds(1);
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Locks complete report and JSON facts while recording non-gating local cost evidence.</summary>
    [Fact]
    public async Task FragmentedReplaceReportPreservesTenThousandDifferenceFacts()
    {
        (byte[] referenceBytes, byte[] replacementBytes) = CreateFragmentedInputs();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = referenceBytes,
                ["replacement-artifact"] = replacementBytes,
            }),
            new FakeClock([StartedAtUtc, CompletedAtUtc]));

        int runThroughReportThread = Environment.CurrentManagedThreadId;
        long runThroughReportAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var runThroughReportTimer = Stopwatch.StartNew();
        CompositionRunResult result = await service.PreviewAsync(
            CreateFragmentedReplaceRequest(referenceBytes.Length),
            TestContext.Current.CancellationToken);
        runThroughReportTimer.Stop();
        long runThroughReportAllocated = Environment.CurrentManagedThreadId == runThroughReportThread
            ? GC.GetAllocatedBytesForCurrentThread() - runThroughReportAllocatedBefore
            : -1;

        long serializationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var serializationTimer = Stopwatch.StartNew();
        string reportJson = JsonSerializer.Serialize(result.Report, ReportJsonOptions);
        serializationTimer.Stop();
        long serializationAllocated = GC.GetAllocatedBytesForCurrentThread() - serializationAllocatedBefore;
        string reportJsonSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reportJson)));
        long repeatedSerializationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var repeatedSerializationTimer = Stopwatch.StartNew();
        string repeatedReportJson = JsonSerializer.Serialize(result.Report, ReportJsonOptions);
        repeatedSerializationTimer.Stop();
        long repeatedSerializationAllocated =
            GC.GetAllocatedBytesForCurrentThread() - repeatedSerializationAllocatedBefore;

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(DifferenceCount, ByteDiff.FindChangedRanges(referenceBytes, result.OutputBytes.Span).Count);
        Assert.Equal(DifferenceCount, result.Report.OutputDifferences.Count);
        Assert.All(result.Report.OutputDifferences, AssertAcceptedOneByteDifference);
        AssertDifference(result.Report.OutputDifferences[0], "diff-001", start: 0, afterPreview: "01");
        AssertDifference(result.Report.OutputDifferences[254], "diff-255", start: 508, afterPreview: "ff");
        AssertDifference(result.Report.OutputDifferences[^1], "diff-10000", start: 19_998, afterPreview: "37");
        AssertReplay(
            result.Report.OutputDifferences[0],
            new ByteRange(0, 48),
            expectedDifferenceOffset: 0);
        AssertReplay(
            result.Report.OutputDifferences[254],
            new ByteRange(464, 80),
            expectedDifferenceOffset: 44);
        AssertReplay(
            result.Report.OutputDifferences[^1],
            new ByteRange(19_952, 48),
            expectedDifferenceOffset: 46);
        Assert.Same(
            result.Report.OutputDifferences[0].BeforeSha256,
            result.Report.OutputDifferences[1].BeforeSha256);
        Assert.Same(
            result.Report.OutputDifferences[0].AfterSha256,
            result.Report.OutputDifferences[255].AfterSha256);
        Assert.Equal(20_000, result.Report.Output.Size);
        Assert.Equal(BaselineOutputSha256, result.Report.Output.Sha256);
        Assert.Equal(BaselineJsonCharacterCount, reportJson.Length);
        Assert.Equal(BaselineReportJsonSha256, reportJsonSha256);
        Assert.True(
            reportJson.AsSpan().SequenceEqual(repeatedReportJson),
            "Repeated report serialization must preserve the exact JSON text.");
        Assert.Contains("\"OutputDifferences\": [", reportJson, StringComparison.Ordinal);
        Assert.Contains("\"ExecutionSnapshot\"", reportJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DeliveryArtifacts\"", reportJson, StringComparison.Ordinal);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"REPORT_BASELINE differences={DifferenceCount} outputSha256={result.Report.Output.Sha256} " +
            $"jsonChars={reportJson.Length} jsonSha256={reportJsonSha256} " +
            $"runThroughReportMs={runThroughReportTimer.Elapsed.TotalMilliseconds:F3} " +
            $"runThroughReportAllocated={runThroughReportAllocated} " +
            $"serializationMs={serializationTimer.Elapsed.TotalMilliseconds:F3} serializationAllocated={serializationAllocated} " +
            $"repeatedSerializationMs={repeatedSerializationTimer.Elapsed.TotalMilliseconds:F3} " +
            $"repeatedSerializationAllocated={repeatedSerializationAllocated}");
    }

    /// <summary>Verifies immutable semantic metadata is reused only within one report-generation run.</summary>
    [Fact]
    public async Task FragmentedReplaceReportSharesStableSemanticsOnlyWithinOneRun()
    {
        const int differenceCount = 2;
        (byte[] referenceBytes, byte[] replacementBytes) = CreateFragmentedInputs(differenceCount);
        var artifacts = new Dictionary<string, byte[]>
        {
            ["reference-artifact"] = referenceBytes,
            ["replacement-artifact"] = replacementBytes,
        };
        var firstService = new CompositionRunService(
            new FakeArtifactReader(artifacts),
            new FakeClock([StartedAtUtc, CompletedAtUtc]));
        var secondService = new CompositionRunService(
            new FakeArtifactReader(artifacts),
            new FakeClock([StartedAtUtc, CompletedAtUtc]));

        CompositionRunResult first = await firstService.PreviewAsync(
            CreateFragmentedReplaceRequest(referenceBytes.Length),
            TestContext.Current.CancellationToken);
        CompositionRunResult second = await secondService.PreviewAsync(
            CreateFragmentedReplaceRequest(referenceBytes.Length),
            TestContext.Current.CancellationToken);

        Assert.Equal(differenceCount, first.Report.OutputDifferences.Count);
        Assert.Equal(differenceCount, second.Report.OutputDifferences.Count);
        OutputDifferenceSemantic firstSemantic = Assert.IsType<OutputDifferenceSemantic>(
            first.Report.OutputDifferences[0].Semantic);
        OutputDifferenceSemantic secondFragmentSemantic = Assert.IsType<OutputDifferenceSemantic>(
            first.Report.OutputDifferences[1].Semantic);
        OutputDifferenceSemantic secondRunSemantic = Assert.IsType<OutputDifferenceSemantic>(
            second.Report.OutputDifferences[0].Semantic);
        Assert.Same(firstSemantic, secondFragmentSemantic);
        Assert.NotSame(firstSemantic, secondRunSemantic);
    }

    private static (byte[] ReferenceBytes, byte[] ReplacementBytes) CreateFragmentedInputs(
        int differenceCount = DifferenceCount)
    {
        byte[] referenceBytes = new byte[differenceCount * 2];
        byte[] replacementBytes = new byte[referenceBytes.Length];
        for (int index = 0; index < differenceCount; index++)
        {
            replacementBytes[index * 2] = checked((byte)((index % byte.MaxValue) + 1));
        }

        return (referenceBytes, replacementBytes);
    }

    private static CompositionRunRequest CreateFragmentedReplaceRequest(int outputLength)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", outputLength, AddressSpaceMutability.Immutable),
            new("replacement-input", outputLength, AddressSpaceMutability.Immutable),
            new("output-image", outputLength, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", outputLength),
            addressSpaces,
            [
                CompositionOperation.ReplaceRange(
                    "replace-fragmented-input",
                    10,
                    "replacement-input",
                    new ByteRange(0, outputLength),
                    "output-image",
                    new ByteRange(0, outputLength),
                    OverlapPolicy.Reject,
                    "Replace deterministic fragmented input."),
            ]);
        CompiledComposition compiledComposition = CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                "fragmented-report-baseline",
                "1.0.0",
                "NT-SYNTHETIC",
                "general-replace",
                "general-replace",
                CompositionKind.Replace),
            "fragmented-report.bin",
            CompiledIcNumberPolicy.SingleSelector);
        return new CompositionRunRequest(
            "fragmented-report-baseline-run",
            compiledComposition,
            [
                new InputArtifactBinding(
                    "reference-base",
                    "reference-base",
                    "reference-artifact",
                    "reference-base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "replacement-input",
                    "replacement-input",
                    "replacement-artifact",
                    "replacement-input.bin",
                    CompiledInputArtifactClass.TpFirmware),
            ],
            "fragmented-report.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static void AssertAcceptedOneByteDifference(OutputDifferenceSummary difference)
    {
        Assert.Equal(1, difference.Range.Length);
        Assert.Equal(1, difference.ChangedByteCount);
        Assert.Equal(OutputDifferenceClassifications.DeclaredReplacement, difference.Classification);
        Assert.True(difference.IsAccepted);
        Assert.Equal("replace-fragmented-input", difference.Evidence);
        Assert.Equal(1, difference.HexPreviewByteCount);
        Assert.True(difference.IsHexPreviewComplete);
        Assert.Equal(64, difference.BeforeSha256.Length);
        Assert.Equal(64, difference.AfterSha256.Length);
    }

    private static void AssertDifference(
        OutputDifferenceSummary difference,
        string differenceId,
        long start,
        string afterPreview)
    {
        Assert.Equal(differenceId, difference.DifferenceId);
        Assert.Equal(new ByteRange(start, 1), difference.Range);
        Assert.Equal("00", difference.BeforeHexPreview);
        Assert.Equal(afterPreview, difference.AfterHexPreview);
    }

    private static void AssertReplay(
        OutputDifferenceSummary difference,
        ByteRange expectedRange,
        int expectedDifferenceOffset)
    {
        OutputDifferenceReplaySegment replay = Assert.IsType<OutputDifferenceReplaySegment>(difference.Replay);
        Assert.Equal(expectedRange, replay.Range);
        Assert.Equal(expectedRange.Length, replay.BeforeBytes.Length);
        Assert.Equal(expectedRange.Length, replay.AfterBytes.Length);
        Assert.Equal(0, replay.BeforeBytes.Span[expectedDifferenceOffset]);
        Assert.Equal(
            byte.Parse(
                difference.AfterHexPreview,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture),
            replay.AfterBytes.Span[expectedDifferenceOffset]);
    }
}
