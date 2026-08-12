using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

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
}
