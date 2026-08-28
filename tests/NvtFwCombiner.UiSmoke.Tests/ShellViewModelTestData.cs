using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class ShellViewModelTestData
{
    internal static async Task<CompositionRunResult> CreateDpReplaceInspectionResultAsync(
        CompositionHostServices host,
        int changeLength = 2)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-report-hex-diff");
        byte[] baseBytes = CreatePattern(0x40000, 0x51);
        byte[] replacementBytes = (byte[])baseBytes.Clone();
        for (int index = 0; index < changeLength; index++)
        {
            replacementBytes[0x100 + index] ^= 0xFF;
        }

        replacementBytes[0x100] = 0xA5;
        replacementBytes[0x101] = 0x5A;
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", replacementBytes);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-dp"] = replacementPath,
        };

        CompiledAuthoringSelectionSnapshot discovery =
            host.DpReplaceAuthoring.GetAuthoringSnapshot(
                "NT51950",
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        CompiledAuthoringInputBinding replacement = discovery.InputBindings.Single(static binding =>
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase) &&
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.LdcReplacement));
        var session = new AuthoringSessionState(ExperienceIds.DpReplace);
        CompiledAuthoringSessionPreparation prepared =
            host.DpReplaceAuthoring.PrepareSession(
                session,
                "NT51950",
                [
                    new CompiledAuthoringSelectedInput(
                        CompositionAddressSpaceIds.ReferenceBase,
                        basePath,
                        baseBytes),
                    new CompiledAuthoringSelectedInput(
                        replacement.AddressSpaceId,
                        replacementPath,
                        replacementBytes),
                ]);
        Assert.True(prepared.Succeeded);
        CompositionRunResult result = await host.CompositionExecution
            .ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    prepared.Snapshot!,
                    paths,
                    build: false),
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
