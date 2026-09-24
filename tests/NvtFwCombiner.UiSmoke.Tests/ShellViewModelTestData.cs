using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class ShellViewModelTestData
{
    internal const int ReportFixtureTargetStart = 0x3E020;

    internal static async Task<CompositionRunResult> CreateGeneralReplaceInspectionResultAsync(
        CompositionHostServices host,
        int changeLength = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(changeLength, 2);
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-report-hex-diff");
        byte[] baseBytes = CreatePattern(0x40000, 0x51);
        byte[] replacementBytes = baseBytes.AsSpan(ReportFixtureTargetStart, changeLength).ToArray();
        for (int index = 0; index < changeLength; index++)
        {
            replacementBytes[index] ^= 0xFF;
        }

        replacementBytes[0] = 0xA5;
        replacementBytes[1] = 0x5A;
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", replacementBytes);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
        };
        var draft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "report-diff",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(replacementPath),
                new ByteRange(0, changeLength),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(ReportFixtureTargetStart, changeLength),
                OverlapPolicy.Reject,
                alignment: 1,
                "Synthetic Report replay fixture."),
        ]);
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareReplaceSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralReplace),
            "NT51926",
            "single",
            basePath,
            draft,
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CompositionRunResult result = await host.CompositionExecution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                prepared.AcceptedSession!,
                paths,
                build: false,
                actionReadiness: prepared.Readiness),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        _ = Assert.IsType<CompositionRunInspectionSnapshot>(result.InspectionSnapshot);
        return result;
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
            source.PreviewToken,
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
