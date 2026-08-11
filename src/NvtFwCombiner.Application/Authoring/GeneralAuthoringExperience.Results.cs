using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class GeneralAuthoringExperience
{
    private static GeneralSelectedFileInspection AtRevision(
        GeneralSelectedFileInspection inspection,
        AuthoringRevision revision)
    {
        return new GeneralSelectedFileInspection(
            inspection.DefinitionId,
            revision,
            inspection.SelectedPathHint,
            inspection.FileStamp,
            inspection.DisplayNameHint,
            inspection.LastWriteTimeUtcHint,
            inspection.AcceptedBytes);
    }

    private static GeneralMappingDraftRow[] FileRows(GeneralMappingDraftState draft)
    {
        return
        [
            .. draft.Rows.Where(static row =>
                row.Source.Kind == GeneralMappingSourceKind.FileArtifact),
        ];
    }

    private static GeneralAuthoringSessionPreparation Failed(
        GeneralSelectedFileInspectionIssue issue)
    {
        return Failed(issue.Message, issue.DefinitionId, issue.Code);
    }

    private static GeneralAuthoringSessionPreparation Failed(
        AuthoringSessionIssue issue)
    {
        return Failed(issue.Message, issue.Subject, issue.Code);
    }

    private static GeneralAuthoringSessionPreparation Failed(
        IReadOnlyList<CompositionIssue> issues,
        GeneralAuthoringAdmissionResult? admission = null)
    {
        return new GeneralAuthoringSessionPreparation(
            AcceptedSession: null,
            issues,
            admission);
    }

    private static GeneralAuthoringSessionPreparation Failed(
        string message,
        string? subject = null,
        string code = CapabilityCatalogIssueCodes.RouteUnavailable)
    {
        return new GeneralAuthoringSessionPreparation(
            AcceptedSession: null,
            [new CompositionIssue(code, message, subject)]);
    }
}
