using System.Security.Cryptography;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class ShellViewModelTestData
{
    internal const int ReportFixtureTargetStart = 0x3E020;

    // Historical typed report plus byte planes; no catalog, admission, input file or live preparation is required.
    internal static CompositionRunResult CreateHistoricalReplaceInspectionResult(int changeLength = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(changeLength, 2);
        byte[] before = CreatePattern(0x40000, 0x51);
        byte[] after = [.. before];
        for (int index = 0; index < changeLength; index++)
        {
            after[ReportFixtureTargetStart + index] ^= 0xFF;
        }

        after[ReportFixtureTargetStart] = 0xA5;
        after[ReportFixtureTargetStart + 1] = 0x5A;
        var range = new ByteRange(ReportFixtureTargetStart, changeLength);
        string beforeHash = Convert.ToHexStringLower(SHA256.HashData(before.AsSpan(ReportFixtureTargetStart, changeLength)));
        string afterHash = Convert.ToHexStringLower(SHA256.HashData(after.AsSpan(ReportFixtureTargetStart, changeLength)));
        var difference = new OutputDifferenceSummary(
            "diff-001", range, changeLength, OutputDifferenceClassifications.DeclaredReplacement,
            isAccepted: true, "report-diff", "Historical declared replacement.", "DP payload", beforeHash, afterHash,
            beforeHexPreview: Convert.ToHexString(before.AsSpan(ReportFixtureTargetStart, Math.Min(changeLength, 16))),
            afterHexPreview: Convert.ToHexString(after.AsSpan(ReportFixtureTargetStart, Math.Min(changeLength, 16))),
            hexPreviewByteCount: Math.Min(changeLength, 16), isHexPreviewComplete: changeLength <= 16,
            replay: OutputDifferenceReplaySegment.CreateWithAlignedContext(before, after, range));
        var report = new CompositionRunReport(
            "historical-general-replace", "nt51926-general-replace-dp-single-candidate", "0.1.0", "NT51926",
            ExperienceIds.GeneralReplace, ExperienceIds.GeneralReplace, CompositionKind.Replace,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            [
                new InputArtifactSummary("reference-image", "base.bin", before.Length,
                    Convert.ToHexStringLower(SHA256.HashData(before))),
                new InputArtifactSummary("replacement", "replacement.bin", changeLength, afterHash),
            ],
            [new OperationRunSummary("report-diff", 100, CompositionOperationKind.ReplaceRange,
                OperationRunStatus.Succeeded, "replacement", new ByteRange(0, changeLength),
                CompositionAddressSpaceIds.OutputImage, range, OverlapPolicy.Reject, null, null, [], [],
                "Historical declared replacement.")],
            [new MutationRunSummary("report-diff", CompositionOperationKind.ReplaceRange,
                CompositionAddressSpaceIds.OutputImage, range, changeLength, beforeHash, afterHash,
                "Historical declared replacement.")],
            issues: [],
            new OutputArtifactSummary("preview.bin", after.Length, Convert.ToHexStringLower(SHA256.HashData(after)),
                committed: false),
            [difference],
            imageInitialization: ImageInitializationSummary.FromCompiled(
                ImageInitialization.Reference(CompositionAddressSpaceIds.OutputImage, "reference-image", before.Length)));
        return new CompositionRunResult(
            CompositionExecutionStatus.Succeeded, after, report, committedOutputId: null,
            CompositionAddressSpaceIds.OutputImage, "reference-image", before, after);
    }

    internal static CompositionRunResult WithReport(
        CompositionRunResult source,
        CompositionRunReport report)
    {
        return CloneRunResult(source, source.CommittedOutputId, report);
    }

    internal static CompositionRunResult CloneRunResult(
        CompositionRunResult source,
        string? committedOutputId,
        CompositionRunReport report,
        IReadOnlyList<CompositionDeliveryArtifact>? deliveryArtifacts = null,
        bool? isDeliveryComplete = null,
        string? deliveryFailureMessage = null)
    {
        CompositionRunInspectionSnapshot? inspection = source.InspectionSnapshot;
        var clone = new CompositionRunResult(
            source.Status,
            source.OutputBytes,
            report,
            committedOutputId,
            inspection?.OutputSpaceId,
            inspection?.ReferenceSpaceId,
            inspection?.ReferenceBytes.ToArray(),
            inspection?.OutputBytes,
            source.OutcomeStatus,
            source.AcceptedGeneralMappingDraft,
            source.ResolvedCapability,
            deliveryArtifacts ?? source.DeliveryArtifacts,
            isDeliveryComplete ?? source.IsDeliveryComplete,
            deliveryFailureMessage ?? source.DeliveryFailureMessage);
        return clone;
    }

    internal static CompositionRunReport CreateLargeDifferenceReport(
        CompositionRunReport source,
        int count,
        int sectionCount,
        string runId)
    {
        OutputDifferenceSummary[] differences =
        [
            .. Enumerable.Range(0, count).Select(index => new OutputDifferenceSummary(
                $"diff-{index:D5}",
                new ByteRange(index * 4L, 4),
                changedByteCount: 4,
                index == count - 1
                    ? OutputDifferenceClassifications.Unexpected
                    : OutputDifferenceClassifications.DeclaredReplacement,
                isAccepted: index != count - 1,
                $"evidence-{index:D5}",
                $"difference {index}",
                $"Section {index % sectionCount:D2}",
                "11111111111111111111",
                "22222222222222222222",
                beforeHexPreview: "AABBCCDD",
                afterHexPreview: "11223344",
                hexPreviewByteCount: 4,
                isHexPreviewComplete: true)),
        ];
        return new CompositionRunReport(
            runId,
            source.ProfileId,
            source.ProfileVersion,
            source.IcId,
            source.ModeId,
            source.ExperienceId,
            source.CompositionKind,
            source.StartedAtUtc,
            source.CompletedAtUtc,
            source.Inputs,
            source.Operations,
            source.Mutations,
            source.Issues,
            source.Output,
            differences,
            source.CompilationFingerprint,
            source.Validations,
            source.OutputNaming,
            source.DeliveryArtifacts,
            source.GeneralAdmission,
            source.ImageInitialization,
            source.DiagnosticPreview);
    }
}
