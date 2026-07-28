using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const int GeneralReplaceMappingSequenceStart = 100;

    private static bool TryCreateGeneralReplaceMappings(
        GeneralMappingDraftState mappingDraft,
        long referenceCapacity,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<GeneralReplacePatchArtifact> patchArtifacts,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<GeneralReplacePatchArtifact> artifacts = [];
        List<CompositionIssue> issueList = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        int operationIndex = 0;

        foreach (GeneralMappingDraftRow input in mappingDraft.Rows)
        {
            if (!TryRegisterGeneralReplaceId(input.MappingId, ids, issueList))
            {
                operationIndex++;
                continue;
            }

            bool isFile = input.Source.Kind == GeneralMappingSourceKind.FileArtifact;
            if (isFile)
            {
                if (!TryCreateFileGeneralReplaceMapping(
                    input,
                    operationIndex,
                    out ExplicitMapping? mapping,
                    out AddressSpace? space,
                    out InputArtifactBinding? binding,
                    out CompositionIssue? issue))
                {
                    if (issue is not null)
                    {
                        issueList.Add(issue);
                    }

                    operationIndex++;
                    continue;
                }

                mappings.Add(mapping!);
                spaces.Add(space!);
                bindings.Add(binding!);
            }
            else
            {
                if (!TryCreatePatchGeneralReplaceMapping(
                    input,
                    operationIndex,
                    referenceCapacity,
                    out ExplicitMapping? mapping,
                    out AddressSpace? space,
                    out InputArtifactBinding? binding,
                    out GeneralReplacePatchArtifact? artifact,
                    out CompositionIssue? issue))
                {
                    if (issue is not null)
                    {
                        issueList.Add(issue);
                    }

                    operationIndex++;
                    continue;
                }

                mappings.Add(mapping!);
                spaces.Add(space!);
                bindings.Add(binding!);
                artifacts.Add(artifact!);
            }

            operationIndex++;
        }

        explicitMappings = mappings;
        requestAddressSpaces = spaces;
        mappingBindings = bindings;
        patchArtifacts = artifacts;
        issues = issueList;
        return issueList.Count == 0;
    }

    private static bool TryCreateFileGeneralReplaceMapping(
        GeneralMappingDraftRow input,
        int operationIndex,
        out ExplicitMapping? mapping,
        out AddressSpace? addressSpace,
        out InputArtifactBinding? binding,
        out CompositionIssue? issue)
    {
        mapping = null;
        addressSpace = null;
        binding = null;
        string addressSpaceId = $"{input.MappingId}-input";
        string fullPath = Path.GetFullPath(input.Source.Reference);
        long declaredLength = File.Exists(fullPath)
            ? new FileInfo(fullPath).Length
            : input.SourceRange.EndExclusive;
        if (declaredLength < input.SourceRange.EndExclusive)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"General Replace mapping '{input.MappingId}' source range exceeds the selected replacement file length.",
                input.MappingId);
            return false;
        }

        addressSpace = new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable);
        binding = new InputArtifactBinding(addressSpaceId, input.MappingId, fullPath);
        mapping = CreateGeneralReplaceMapping(
            input,
            operationIndex,
            addressSpaceId);
        issue = null;
        return true;
    }

    private static bool TryCreatePatchGeneralReplaceMapping(
        GeneralMappingDraftRow input,
        int operationIndex,
        long referenceCapacity,
        out ExplicitMapping? mapping,
        out AddressSpace? addressSpace,
        out InputArtifactBinding? binding,
        out GeneralReplacePatchArtifact? artifact,
        out CompositionIssue? issue)
    {
        mapping = null;
        addressSpace = null;
        binding = null;
        artifact = null;
        if (input.TargetRange.EndExclusive > referenceCapacity)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"General Replace patch '{input.MappingId}' target range exceeds the {referenceCapacity}-byte base flash BIN.",
                input.MappingId);
            return false;
        }

        if (!TryCreatePatchSource(
                input,
                out byte[]? overwriteBytes,
                out byte? fillByte,
                out string? reason,
                out issue))
        {
            return false;
        }

        string addressSpaceId = $"{input.MappingId}-input";
        string virtualArtifactId = VirtualArtifactLocator.CreateGeneralReplacePatch(input.MappingId);
        artifact = new GeneralReplacePatchArtifact(
            input.MappingId,
            virtualArtifactId,
            input.TargetRange.Length,
            overwriteBytes,
            fillByte);
        addressSpace = new AddressSpace(addressSpaceId, input.SourceRange.EndExclusive, AddressSpaceMutability.Immutable);
        binding = new InputArtifactBinding(addressSpaceId, input.MappingId, virtualArtifactId);
        mapping = CreateGeneralReplaceMapping(
            input,
            operationIndex,
            addressSpaceId,
            reason);
        issue = null;
        return true;
    }

    private static ExplicitMapping CreateGeneralReplaceMapping(
        GeneralMappingDraftRow input,
        int operationIndex,
        string addressSpaceId,
        string? reason = null)
    {
        return new ExplicitMapping(
            input.MappingId,
            checked(GeneralReplaceMappingSequenceStart + operationIndex),
            input.OperationKind,
            addressSpaceId,
            input.SourceRange,
            input.TargetAddressSpaceId,
            input.TargetRange,
            input.OverlapPolicy,
            input.Alignment,
            reason ?? input.Reason,
            input.TargetRegionId,
            input.Provenance);
    }

    private static bool TryCreatePatchSource(
        GeneralMappingDraftRow input,
        out byte[]? overwriteBytes,
        out byte? fillByte,
        out string? reason,
        out CompositionIssue? issue)
    {
        overwriteBytes = null;
        fillByte = null;
        reason = null;
        if (!TryParseHexBytes(input.Source.InlineValue, out byte[]? suppliedBytes))
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplacePatchHexInvalid,
                $"General Replace patch '{input.MappingId}' must contain complete hexadecimal byte pairs.",
                input.MappingId);
            return false;
        }

        switch (input.Source.Kind)
        {
            case GeneralMappingSourceKind.HexOverwrite:
                if (suppliedBytes!.LongLength != input.TargetRange.Length)
                {
                    issue = new CompositionIssue(
                        WorkbenchIssueCodes.GeneralReplacePatchLengthMismatch,
                        $"General Replace patch '{input.MappingId}' supplies {suppliedBytes.LongLength} byte(s) for a {input.TargetRange.Length}-byte target range.",
                        input.MappingId);
                    return false;
                }

                overwriteBytes = suppliedBytes;
                reason = "Overwrite hexadecimal General range.";
                issue = null;
                return true;
            case GeneralMappingSourceKind.HexFill:
                if (suppliedBytes!.Length != 1)
                {
                    issue = new CompositionIssue(
                        WorkbenchIssueCodes.GeneralReplacePatchFillByteInvalid,
                        $"General Replace fill '{input.MappingId}' must contain exactly one hexadecimal byte.",
                        input.MappingId);
                    return false;
                }

                fillByte = suppliedBytes[0];
                reason = $"Fill hexadecimal General range with 0x{suppliedBytes[0]:X2}.";
                issue = null;
                return true;
            case GeneralMappingSourceKind.FileArtifact:
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.GeneralReplacePatchHexInvalid,
                    $"General Replace patch '{input.MappingId}' requires an inline patch source.",
                    input.MappingId);
                return false;
            default:
                throw new InvalidOperationException(
                    $"Unknown General mapping source kind '{input.Source.Kind}'.");
        }
    }

    private static bool TryMaterializeGeneralReplacePatchArtifacts(
        IReadOnlyList<GeneralReplacePatchArtifact> patchArtifacts,
        out IReadOnlyDictionary<string, byte[]> virtualArtifacts,
        out IReadOnlyList<CompositionIssue> issues)
    {
        Dictionary<string, byte[]> artifacts = new(StringComparer.Ordinal);
        List<CompositionIssue> issueList = [];
        foreach (GeneralReplacePatchArtifact artifact in patchArtifacts)
        {
            if (!artifact.TryMaterialize(out byte[]? bytes, out CompositionIssue? issue))
            {
                issueList.Add(issue!);
                continue;
            }

            artifacts.Add(artifact.VirtualArtifactId, bytes!);
        }

        virtualArtifacts = artifacts;
        issues = issueList;
        return issueList.Count == 0;
    }

    private sealed record GeneralReplacePatchArtifact(
        string PatchId,
        string VirtualArtifactId,
        long Length,
        byte[]? OverwriteBytes,
        byte? FillByte)
    {
        public bool TryMaterialize(out byte[]? bytes, out CompositionIssue? issue)
        {
            if (OverwriteBytes is not null)
            {
                bytes = OverwriteBytes;
                issue = null;
                return true;
            }

            if (FillByte is not { } fillByte || Length > Array.MaxLength)
            {
                bytes = null;
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.GeneralReplacePatchLengthMismatch,
                    $"General Replace fill '{PatchId}' is too large to materialize safely.",
                    PatchId);
                return false;
            }

            try
            {
                bytes = new byte[checked((int)Length)];
                Array.Fill(bytes, fillByte);
                issue = null;
                return true;
            }
            catch (OutOfMemoryException)
            {
                bytes = null;
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.GeneralReplacePatchLengthMismatch,
                    $"General Replace fill '{PatchId}' could not be materialized safely.",
                    PatchId);
                return false;
            }
        }
    }

    private static bool TryParseHexBytes(string? value, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string compact = string.Concat(value.Where(character =>
            !char.IsWhiteSpace(character) && character is not '-' and not ',' and not '_'));
        try
        {
            bytes = Convert.FromHexString(compact);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryRegisterGeneralReplaceId(
        string? id,
        HashSet<string> ids,
        List<CompositionIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(['/', '\\', ':']) >= 0 || id is "." or "..")
        {
            issues.Add(new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplacePatchIdInvalid,
                "General Replace mapping and patch ids must be non-empty report-safe identifiers.",
                id));
            return false;
        }

        if (!ids.Add(id))
        {
            issues.Add(new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplacePatchIdDuplicate,
                $"General Replace mapping or patch id '{id}' is declared more than once.",
                id));
            return false;
        }

        return true;
    }

}

/// <summary>One user-authored General Replace mapping row from the workbench surface.</summary>
public sealed record WorkbenchGeneralReplaceMappingInput(
    string MappingId,
    string FilePath,
    string TargetStart,
    string TargetEndInclusive);
