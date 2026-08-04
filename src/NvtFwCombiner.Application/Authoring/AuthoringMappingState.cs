using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Stable issue produced while editable General mapping text becomes a typed row.</summary>
public sealed record AuthoringMappingIssue(string Code, string Message);

/// <summary>Stable issue codes for the shared General mapping authoring seam.</summary>
public static class AuthoringMappingIssueCodes
{
    /// <summary>Start or Length text cannot form the required half-open ranges.</summary>
    public const string RangeInvalid = "authoring.general.mapping.range-invalid";

    /// <summary>The parsed row violates a non-range mapping invariant.</summary>
    public const string MappingInvalid = "authoring.general.mapping.invalid";
}

/// <summary>
/// Immutable editable General mapping state. Adapters retain the text while
/// every valid consumer shares the same typed half-open mapping.
/// </summary>
public sealed record AuthoringMappingState(
    string MappingId,
    GeneralMappingDraftRow? Mapping,
    AuthoringMappingIssue? Issue)
{
    /// <summary>Parses editable range text into one immutable mapping state.</summary>
    public static AuthoringMappingState Create(
        string mappingId,
        ExplicitMappingOperationKind operationKind,
        GeneralMappingSource source,
        string sourceStart,
        string targetStart,
        string length,
        string targetAddressSpaceId,
        OverlapPolicy overlapPolicy,
        int alignment,
        string reason,
        string? targetRegionId = null,
        OperationProvenance? provenance = null,
        GeneralMappingFileRangePreset? fileRangePreset = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentNullException.ThrowIfNull(source);
        AuthoringRangeTextIssue? targetIssue = null;
        if (!AuthoringByteRangeCodec.TryParseStartAndLength(
            sourceStart ?? string.Empty,
            length ?? string.Empty,
            out ByteRange sourceRange,
            out AuthoringRangeTextIssue? sourceIssue) ||
            !AuthoringByteRangeCodec.TryParseStartAndLength(
            targetStart ?? string.Empty,
            length ?? string.Empty,
            out ByteRange targetRange,
            out targetIssue))
        {
            return new AuthoringMappingState(mappingId, null, new AuthoringMappingIssue(
                AuthoringMappingIssueCodes.RangeInvalid,
                (sourceIssue ?? targetIssue)!.Message));
        }

        try
        {
            return new AuthoringMappingState(mappingId, new GeneralMappingDraftRow(
                mappingId,
                operationKind,
                source,
                sourceRange,
                targetAddressSpaceId,
                targetRange,
                overlapPolicy,
                alignment,
                reason,
                targetRegionId,
                provenance,
                fileRangePreset), null);
        }
        catch (ArgumentException exception)
        {
            return new AuthoringMappingState(mappingId, null, new AuthoringMappingIssue(
                AuthoringMappingIssueCodes.MappingInvalid,
                exception.Message));
        }
    }

    /// <summary>True when this state owns one typed mapping.</summary>
    public bool IsValid => Mapping is not null && Issue is null;
}
