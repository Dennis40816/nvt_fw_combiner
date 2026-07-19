using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralReplaceMappings(
        WorkbenchGeneralReplaceMappingInput[] mappingInputs,
        WorkbenchGeneralReplacePatchInput[] patchInputs,
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

        foreach (WorkbenchGeneralReplaceMappingInput input in mappingInputs)
        {
            if (!TryRegisterGeneralReplaceId(input.MappingId, ids, issueList))
            {
                operationIndex++;
                continue;
            }

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
            operationIndex++;
        }

        foreach (WorkbenchGeneralReplacePatchInput input in patchInputs)
        {
            if (!TryRegisterGeneralReplaceId(input.PatchId, ids, issueList))
            {
                operationIndex++;
                continue;
            }

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
        WorkbenchGeneralReplaceMappingInput input,
        int operationIndex,
        out ExplicitMapping? mapping,
        out AddressSpace? addressSpace,
        out InputArtifactBinding? binding,
        out CompositionIssue? issue)
    {
        mapping = null;
        addressSpace = null;
        binding = null;
        if (!TryParseGeneralReplaceRange(
                input.MappingId,
                input.TargetStart,
                input.TargetEndInclusive,
                out ByteRange targetRange,
                out issue))
        {
            return false;
        }

        string addressSpaceId = $"{input.MappingId}-input";
        string fullPath = Path.GetFullPath(input.FilePath);
        long declaredLength = File.Exists(fullPath)
            ? new FileInfo(fullPath).Length
            : targetRange.Length;
        addressSpace = new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable);
        binding = new InputArtifactBinding(addressSpaceId, input.MappingId, fullPath);
        mapping = CreateGeneralReplaceMapping(
            input.MappingId,
            operationIndex,
            addressSpaceId,
            targetRange,
            "Replace explicit General range.");
        issue = null;
        return true;
    }

    private static bool TryCreatePatchGeneralReplaceMapping(
        WorkbenchGeneralReplacePatchInput input,
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
        if (!TryParseGeneralReplaceRange(
                input.PatchId,
                input.TargetStart,
                input.TargetEndInclusive,
                out ByteRange targetRange,
                out issue))
        {
            return false;
        }

        if (targetRange.EndExclusive > referenceCapacity)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"General Replace patch '{input.PatchId}' target range exceeds the {referenceCapacity}-byte base flash BIN.",
                input.PatchId);
            return false;
        }

        if (!TryCreatePatchSource(
                input,
                targetRange,
                out byte[]? overwriteBytes,
                out byte? fillByte,
                out string? reason,
                out issue))
        {
            return false;
        }

        string addressSpaceId = $"{input.PatchId}-input";
        string virtualArtifactId = VirtualArtifactLocator.CreateGeneralReplacePatch(input.PatchId);
        artifact = new GeneralReplacePatchArtifact(
            input.PatchId,
            virtualArtifactId,
            targetRange.Length,
            overwriteBytes,
            fillByte);
        addressSpace = new AddressSpace(addressSpaceId, targetRange.Length, AddressSpaceMutability.Immutable);
        binding = new InputArtifactBinding(addressSpaceId, input.PatchId, virtualArtifactId);
        mapping = CreateGeneralReplaceMapping(
            input.PatchId,
            operationIndex,
            addressSpaceId,
            targetRange,
            reason!);
        issue = null;
        return true;
    }

    private static ExplicitMapping CreateGeneralReplaceMapping(
        string mappingId,
        int operationIndex,
        string addressSpaceId,
        ByteRange targetRange,
        string reason)
    {
        return new ExplicitMapping(
            mappingId,
            checked(GeneralReplaceMappingSequenceStart + operationIndex),
            ExplicitMappingOperationKind.ReplaceRange,
            addressSpaceId,
            new ByteRange(0, targetRange.Length),
            CompositionAddressSpaceIds.OutputImage,
            targetRange,
            OverlapPolicy.Reject,
            alignment: 1,
            reason,
            targetRegionId: null);
    }

    private static bool TryCreatePatchSource(
        WorkbenchGeneralReplacePatchInput input,
        ByteRange targetRange,
        out byte[]? overwriteBytes,
        out byte? fillByte,
        out string? reason,
        out CompositionIssue? issue)
    {
        overwriteBytes = null;
        fillByte = null;
        reason = null;
        if (!TryParseHexBytes(input.Value, out byte[]? suppliedBytes))
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplacePatchHexInvalid,
                $"General Replace patch '{input.PatchId}' must contain complete hexadecimal byte pairs.",
                input.PatchId);
            return false;
        }

        switch (input.Kind)
        {
            case WorkbenchGeneralReplacePatchKind.Overwrite:
                if (suppliedBytes!.LongLength != targetRange.Length)
                {
                    issue = new CompositionIssue(
                        WorkbenchIssueCodes.GeneralReplacePatchLengthMismatch,
                        $"General Replace patch '{input.PatchId}' supplies {suppliedBytes.LongLength} byte(s) for a {targetRange.Length}-byte target range.",
                        input.PatchId);
                    return false;
                }

                overwriteBytes = suppliedBytes;
                reason = "Overwrite hexadecimal General range.";
                issue = null;
                return true;
            case WorkbenchGeneralReplacePatchKind.Fill:
                if (suppliedBytes!.Length != 1)
                {
                    issue = new CompositionIssue(
                        WorkbenchIssueCodes.GeneralReplacePatchFillByteInvalid,
                        $"General Replace fill '{input.PatchId}' must contain exactly one hexadecimal byte.",
                        input.PatchId);
                    return false;
                }

                fillByte = suppliedBytes[0];
                reason = $"Fill hexadecimal General range with 0x{suppliedBytes[0]:X2}.";
                issue = null;
                return true;
            default:
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.GeneralReplacePatchHexInvalid,
                    $"General Replace patch '{input.PatchId}' has an unsupported patch operation.",
                    input.PatchId);
                return false;
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

    private static bool TryParseGeneralReplaceRange(
        string id,
        string targetStart,
        string targetEndInclusive,
        out ByteRange targetRange,
        out CompositionIssue? issue)
    {
        targetRange = default;
        if (!BootstrapRangeText.TryParseNonNegativeLong(targetStart, out long start) ||
            !BootstrapRangeText.TryParseNonNegativeLong(targetEndInclusive, out long endInclusive) ||
            endInclusive < start)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                $"General Replace mapping or patch '{id}' must use a valid inclusive start/end range.",
                id);
            return false;
        }

        try
        {
            targetRange = ByteRange.FromStartEndExclusive(start, checked(endInclusive + 1));
            issue = null;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                $"General Replace mapping or patch '{id}' must use a valid inclusive start/end range.",
                id);
            return false;
        }
        catch (OverflowException)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                $"General Replace mapping or patch '{id}' range exceeds the supported address size.",
                id);
            return false;
        }
    }

}

/// <summary>One user-authored General Replace mapping row from the workbench surface.</summary>
public sealed record WorkbenchGeneralReplaceMappingInput(
    string MappingId,
    string FilePath,
    string TargetStart,
    string TargetEndInclusive);
