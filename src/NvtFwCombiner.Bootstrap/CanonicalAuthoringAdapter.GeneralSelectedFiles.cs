using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Bootstrap;

public static partial class CanonicalAuthoringAdapter
{
    internal static AuthoringCapabilityCatalogSnapshot CreateGeneralExactCatalog(
        ResolvedCapability capability,
        IReadOnlyList<GeneralInputResource> inputResources,
        GeneralMappingDraftState draft,
        long? referenceCapacity = null)
    {
        var lengths = inputResources.ToDictionary(
            static resource => resource.SlotId,
            static resource => resource.LengthBytes,
            StringComparer.Ordinal);
        var slotLengths = draft.Rows.ToDictionary(
            static row => row.MappingId,
            row => lengths.GetValueOrDefault(row.MappingId, row.SourceRange.EndExclusive),
            StringComparer.Ordinal);
        if (referenceCapacity is { } capacity)
        {
            slotLengths.Add(GeneralReplaceV2Registration.ReferenceAddressSpaceId, capacity);
        }
        return AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
            capability,
            slotLengths);
    }

    /// <summary>Inspects one desktop-selected General file before command readiness is published.</summary>
    public static async ValueTask<GeneralSelectedFileInspectionResult> InspectGeneralSelectedFileAsync(
        string mappingId,
        string selectedPath,
        AuthoringRevision authoringRevision,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        GeneralSelectedFileInspectionResult result = await CreateGeneralFileInspector(selectedPath).InspectAsync(
            mappingId,
            selectedPath,
            authoringRevision,
            cancellationToken).ConfigureAwait(false);
        return result.Inspection is { } inspection &&
            inspection.FileStamp.AcceptedLength != expectedLength
                ? new GeneralSelectedFileInspectionResult(
                    null,
                    new GeneralSelectedFileInspectionIssue(
                        GeneralSelectedFileInspectionIssueCodes.ObservedLengthChanged,
                        "Selected General file length changed after exact input-contract compilation.",
                        mappingId))
                : result;
    }

    /// <summary>Observes a bounded length before the same General route compiles its inspection contract.</summary>
    public static ValueTask<GeneralSelectedFileLengthResult> ObserveGeneralSelectedFileLengthAsync(
        string mappingId,
        string selectedPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        return CreateGeneralFileInspector(selectedPath).ObserveLengthAsync(
            mappingId, selectedPath, cancellationToken);
    }

    private static GeneralSelectedFileInspectionService CreateGeneralFileInspector(string path)
    {
        return new(new FileContentSnapshotInspector(
                [Path.GetDirectoryName(Path.GetFullPath(path))!]),
                GeneralAuthoringTechnicalLimits.Default.MaximumFileBytes);
    }

    /// <summary>
    /// Both desktop/workbench and CLI paths cross this Application result
    /// before General validation, compilation, Preview, Build, or reports.
    /// </summary>
    public static async ValueTask<GeneralSelectedFileBindingResult>
        InspectGeneralSelectedFilesAsync(
            GeneralMappingDraftState draft,
            AuthoringRevision authoringRevision,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        GeneralMappingDraftRow[] pendingRows =
        [
            .. draft.Rows.Where(static row =>
                row.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                row.Source.AcceptedFileStamp is null),
        ];
        if (pendingRows.Length == 0)
        {
            return new GeneralSelectedFileBindingResult(draft, []);
        }

        string[] roots =
        [
            .. pendingRows
                .Select(static row =>
                    Path.GetDirectoryName(
                        Path.GetFullPath(row.Source.Reference))!)
                .Distinct(
                    OperatingSystem.IsWindows()
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal),
        ];
        var service = new GeneralSelectedFileInspectionService(
            new FileContentSnapshotInspector(roots),
            GeneralAuthoringTechnicalLimits.Default.MaximumFileBytes);
        GeneralSelectedFileDraftInspectionResult result =
            await service.AcceptDraftAsync(
                draft,
                authoringRevision,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded
            ? new GeneralSelectedFileBindingResult(result.Draft!, [])
            : new GeneralSelectedFileBindingResult(
                Draft: null,
                [
                    .. result.Issues.Select(static issue =>
                        new CompositionIssue(
                            issue.Code,
                            issue.Message,
                            issue.DefinitionId)),
                ]);
    }

    internal static GeneralSelectedFileBindingResult RequireAcceptedGeneralSelectedFiles(
        GeneralMappingDraftState draft)
    {
        CompositionIssue[] issues =
        [
            .. draft.Rows
                .Where(static row =>
                    row.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                    row.Source.AcceptedFileStamp is null)
                .Select(static row => new CompositionIssue(
                    GeneralSelectedFileInspectionIssueCodes.SnapshotRequired,
                    "The selected General file requires explicit inspection or Reload/Rebind before execution.",
                    row.MappingId)),
        ];
        return issues.Length == 0
            ? new GeneralSelectedFileBindingResult(draft, [])
            : new GeneralSelectedFileBindingResult(Draft: null, issues);
    }

    internal static bool IsAcceptedGeneralMappingDraft(
        GeneralMappingDraftState? draft)
    {
        return draft is not null &&
            draft.Rows.All(static row =>
                row.Source.Kind != GeneralMappingSourceKind.FileArtifact ||
                row.Source.AcceptedFileStamp is not null);
    }

    internal static ResolvedCapability? TryRetainGeneralMappingCompilation(
        AuthoringSessionState session,
        GeneralMappingDraftState draft,
        string mappingId,
        long observedLength)
    {
        ActiveSessionSnapshot? current = session.CurrentSnapshot;
        GeneralMappingDraftState? currentMappings = current?.DraftState switch
        {
            GeneralMergeDraftState merge => merge.Mappings,
            GeneralMappingDraftState replace => replace,
            _ => null,
        };
        string[] sourceAddressSpaceIds = current?.ExactCapability?.CompiledComposition
            .Plan.OrderedOperations
            .Where(operation =>
                StringComparer.Ordinal.Equals(operation.OperationId, mappingId) &&
                operation.SourceSpaceId is not null)
            .Select(static operation => operation.SourceSpaceId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        bool sameCompiledLength = current?.ExactCapability is { } exact &&
            sourceAddressSpaceIds.Length > 0 &&
            sourceAddressSpaceIds.All(addressSpaceId =>
            {
                AddressSpace space = exact.CompiledComposition.Plan.AddressSpaces.Single(
                    candidate => StringComparer.Ordinal.Equals(
                        candidate.AddressSpaceId,
                        addressSpaceId));
                return space.Mutability == AddressSpaceMutability.Immutable &&
                    space.Length == observedLength;
            });
        return current?.ExactCapability is { } capability &&
            currentMappings is not null &&
            sameCompiledLength &&
            currentMappings.HasSameCompilationInputs(draft)
                ? capability
                : null;
    }

    internal static bool TryCollectGeneralCandidateFileLengths(
        AuthoringSessionState session,
        GeneralMappingDraftState draft,
        string mappingId,
        long observedLength,
        out IReadOnlyDictionary<string, long> observedFileLengths)
    {
        var lengths = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (GeneralMappingDraftRow row in draft.Rows.Where(static row =>
            row.Source.Kind == GeneralMappingSourceKind.FileArtifact))
        {
            long? length = StringComparer.Ordinal.Equals(row.MappingId, mappingId)
                ? observedLength
                : row.Source.AcceptedFileStamp?.AcceptedLength;
            if (length is null &&
                session.TryGetCachedGeneralSelectedFileInspection(
                    row.MappingId,
                    row.Source.Reference,
                    out GeneralSelectedFileInspection? cached))
            {
                length = cached.FileStamp.AcceptedLength;
            }

            if (length is not > 0)
            {
                observedFileLengths = lengths;
                return false;
            }
            lengths.Add(row.MappingId, length.Value);
        }

        observedFileLengths = lengths;
        return true;
    }

}

/// <summary>Content-bound General draft or stable inspection issues.</summary>
public sealed record GeneralSelectedFileBindingResult(
    GeneralMappingDraftState? Draft,
    IReadOnlyList<CompositionIssue> Issues)
{
    /// <summary>True only when every selected file is content-bound.</summary>
    public bool Succeeded => Draft is not null && Issues.Count == 0;
}
