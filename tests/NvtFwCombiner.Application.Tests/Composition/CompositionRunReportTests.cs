using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using System.Text.Json;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Locks the typed run report as an immutable client projection.</summary>
public sealed class CompositionRunReportTests
{
    /// <summary>Non-AB and legacy reports omit format evidence rather than invent a Common decision.</summary>
    [Fact]
    public void AbsentFormatCaptureIsOmittedByActualReportSerializer()
    {
        CompositionRunReport report = CreateReport(null);
        Assert.Null(report.AbMergeFormat);
        var result = new CompositionRunResult(CompositionExecutionStatus.Succeeded, ReadOnlyMemory<byte>.Empty,
            report, null, null, null, null, null, null);
        using JsonDocument json = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        Assert.False(json.RootElement.TryGetProperty("AbMergeFormat", out _));
        Assert.False(json.RootElement.TryGetProperty("SourceEnvelope", out _));
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Recognized,
            CompositionRunReportJson.AssessReadCompleteness(json.RootElement, TestContext.Current.CancellationToken));
    }

    /// <summary>Caller-owned lists cannot race live projection with durable serialization.</summary>
    [Fact]
    public void ConstructorSnapshotsCollectionsForConcurrentProjectionAndSerialization()
    {
        List<InputArtifactSummary> inputs = [new("input", "artifact", 1, "input-hash")];
        List<OperationRunSummary> operations =
        [
            new(
                "copy",
                0,
                CompositionOperationKind.CopyRange,
                OperationRunStatus.Succeeded,
                "input",
                new ByteRange(0, 1),
                "output",
                new ByteRange(0, 1),
                OverlapPolicy.Reject,
                processorId: null,
                toolBindingId: null,
                processorAllowedReadRanges: [],
                processorAllowedWriteRanges: [],
                "test operation"),
        ];
        List<MutationRunSummary> mutations =
        [
            new(
                "copy",
                CompositionOperationKind.CopyRange,
                "output",
                new ByteRange(0, 1),
                1,
                "before-hash",
                "after-hash",
                "test mutation"),
        ];
        List<CompositionIssue> issues = [new("TEST_WARNING", "test warning", severity: "warning")];
        List<OutputDifferenceSummary> differences =
        [
            new(
                "difference",
                new ByteRange(0, 1),
                1,
                "DeclaredReplacement",
                isAccepted: true,
                "test-evidence",
                "test difference",
                "test section",
                "before-hash",
                "after-hash"),
        ];
        var report = new CompositionRunReport(
            "run",
            "profile",
            "1.0.0",
            "NT51929",
            "mode",
            "experience",
            CompositionKind.Replace,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            inputs,
            operations,
            mutations,
            issues,
            new OutputArtifactSummary("output.bin", 1, "output-hash", committed: false),
            differences);

        inputs.Clear();
        operations.Clear();
        mutations.Clear();
        issues.Clear();
        differences.Clear();

        _ = Assert.Single(report.Inputs);
        _ = Assert.Single(report.Operations);
        _ = Assert.Single(report.Mutations);
        _ = Assert.Single(report.Issues);
        _ = Assert.Single(report.OutputDifferences);
    }

    /// <summary>Bundle delivery is additive while loose reports retain their previous JSON shape.</summary>
    [Fact]
    public void BundleDeliverySerializesOnlyWhenCommittedEvidenceExists()
    {
        const string sha = "0000000000000000000000000000000000000000000000000000000000000000";
        CompositionOutputBundleDeliverySummary bundle =
            CompositionOutputBundleDeliverySummary.FromReceipt(
                new CompositionOutputBundleCommitReceipt(
                    @"C:\delivery\bundle",
                    [new CompositionOutputBundleArtifactReceipt(
                        "output", null, "output.bin", 1, sha)]));
        CompositionRunReport bundled = CreateReport(bundle);
        CompositionRunReport loose = CreateReport(bundleDelivery: null);

        string bundledJson = JsonSerializer.Serialize(bundled);
        string looseJson = JsonSerializer.Serialize(loose);

        Assert.Contains("\"BundleDelivery\"", bundledJson, StringComparison.Ordinal);
        Assert.Contains("\"ResolvedDirectory\"", bundledJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"BundleDelivery\"", looseJson, StringComparison.Ordinal);
    }

    /// <summary>The durable typed report discloses the exact compiled firmware map.</summary>
    [Fact]
    public void ResolvedMapIdIsSerializedWithoutInference()
    {
        CompositionRunReport report = new(
            "run",
            "profile",
            "1.0.0",
            "NT51929",
            "mode",
            "experience",
            CompositionKind.Merge,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [],
            [],
            [],
            [],
            new OutputArtifactSummary("output.bin", 1, "output-hash", committed: true),
            resolvedMapId: "nt51929-standard-merge-256k");

        string json = JsonSerializer.Serialize(report);

        Assert.Contains(
            "\"MapId\":\"nt51929-standard-merge-256k\"",
            json,
            StringComparison.Ordinal);
    }

    /// <summary>Input evidence uses final issue indexes, keeps equal-looking issues distinct, and is omitted when absent.</summary>
    [Fact]
    public void InputDiagnosticsAreDefensiveIndexedSnapshotsWithDurableJsonCompatibility()
    {
        var first = new CompositionIssue("INPUT", "same", "input", CompositionIssueSeverity.Warning);
        var second = new CompositionIssue("INPUT", "same", "input", CompositionIssueSeverity.Warning);
        var supplied = new List<InputDiagnosticSummary>
        {
            new(1, "tp", new InputDiagnosticEvidence("tp-input", null, null, new ByteRange(4, 2), 0xA5)),
            new(0, "dp", new InputDiagnosticEvidence("dp-input", 3, 4, null, null)),
        };
        CompositionRunReport report = new(
            "run", "profile", "1.0.0", "NT51929", "mode", "experience", CompositionKind.Merge,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [], [], [], [first, second],
            new OutputArtifactSummary("output.bin", 1, "output-hash", committed: false),
            inputDiagnostics: supplied);

        supplied.Clear();

        IReadOnlyList<InputDiagnosticSummary> diagnostics = Assert.IsType<IReadOnlyList<InputDiagnosticSummary>>(
            report.InputDiagnostics,
            exactMatch: false);
        Assert.Equal([1, 0], diagnostics.Select(static diagnostic => diagnostic.IssueIndex));
        Assert.Equal("tp", diagnostics[0].SlotId);
        string json = JsonSerializer.Serialize(report);
        Assert.Contains("\"InputDiagnostics\"", json, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement hydratedDiagnostic = document.RootElement.GetProperty("InputDiagnostics")[0];
        Assert.Equal(1, hydratedDiagnostic.GetProperty("IssueIndex").GetInt32());
        JsonElement hydratedEvidence = hydratedDiagnostic.GetProperty("Evidence");
        Assert.Equal(0xA5, hydratedEvidence.GetProperty("RepeatedByte").GetByte());
        JsonElement hydratedRange = hydratedEvidence.GetProperty("SourceRange");
        Assert.Equal(4, hydratedRange.GetProperty("Start").GetInt64());
        Assert.Equal(2, hydratedRange.GetProperty("Length").GetInt64());
        Assert.DoesNotContain("\"InputDiagnostics\"", JsonSerializer.Serialize(CreateReport(null)), StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new InputDiagnosticEvidence(
            "dp-input", null, null, default(ByteRange), null));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunReport(
            "run", "profile", "1.0.0", "NT51929", "mode", "experience", CompositionKind.Merge,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [], [], [], [first, second],
            new OutputArtifactSummary("output.bin", 1, "output-hash", committed: false),
            inputDiagnostics: [new InputDiagnosticSummary(2, "tp", new InputDiagnosticEvidence("tp-input", null, null, new ByteRange(0, 1), 0))]));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunReport(
            "run", "profile", "1.0.0", "NT51929", "mode", "experience", CompositionKind.Merge,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [], [], [], [first, second],
            new OutputArtifactSummary("output.bin", 1, "output-hash", committed: false),
            inputDiagnostics:
            [
                new InputDiagnosticSummary(0, "dp", new InputDiagnosticEvidence("dp-input", null, null, new ByteRange(0, 1), 0)),
                new InputDiagnosticSummary(0, "tp", new InputDiagnosticEvidence("tp-input", null, null, new ByteRange(0, 1), 0)),
            ]));
    }

    private static CompositionRunReport CreateReport(
        CompositionOutputBundleDeliverySummary? bundleDelivery)
    {
        return new CompositionRunReport(
            "run",
            "profile",
            "1.0.0",
            "NT51929",
            "mode",
            "experience",
            CompositionKind.Merge,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [],
            [],
            [],
            [],
            new OutputArtifactSummary("output.bin", 1, "output-hash", committed: true),
            bundleDelivery: bundleDelivery);
    }
}
