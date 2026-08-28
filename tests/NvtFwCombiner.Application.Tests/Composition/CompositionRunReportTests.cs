using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using System.Text.Json;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Locks the typed run report as an immutable client projection.</summary>
public sealed class CompositionRunReportTests
{
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
