using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const int GeneralReplaceHexViewportBytesPerRow = 16;
    private const int GeneralReplaceHexViewportRowCount = 32;

    /// <summary>
    /// Reads a fixed-width base BIN viewport and applies staged General Replace patch bytes only in memory.
    /// This is an inspection surface, not an execution path; profile validation remains part of Build.
    /// </summary>
    public static WorkbenchGeneralReplaceHexViewport CreateGeneralReplaceHexViewport(
        string basePath,
        long viewportStart,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(patches);

        if (viewportStart < 0)
        {
            return CreateGeneralReplaceHexViewportFailure(new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                "Hexadecimal viewport start must be zero or greater.",
                WorkbenchSlotIds.ReplaceBase));
        }

        if (!TryReadGeneralReplaceHexViewportBase(
                basePath,
                viewportStart,
                out string? fullBasePath,
                out long baseLength,
                out long alignedStart,
                out byte[]? before,
                out CompositionIssue? readIssue))
        {
            return CreateGeneralReplaceHexViewportFailure(readIssue!);
        }

        byte[] after = [.. before!];
        List<CompositionIssue> issues = [];
        List<(ByteRange Range, byte[]? OverwriteBytes, byte? FillByte)> overlays = [];
        foreach (WorkbenchGeneralReplacePatchInput patch in patches)
        {
            if (!TryParseGeneralReplaceRange(
                    patch.PatchId,
                    patch.TargetStart,
                    patch.TargetEndInclusive,
                    out ByteRange range,
                    out CompositionIssue? rangeIssue))
            {
                issues.Add(rangeIssue!);
                continue;
            }

            if (range.EndExclusive > baseLength)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"Patch range {FormatGeneralReplaceHexRange(range)} exceeds the {baseLength}-byte base flash BIN.",
                    patch.PatchId));
                continue;
            }

            if (!TryCreatePatchSource(
                    patch,
                    range,
                    out byte[]? overwriteBytes,
                    out byte? fillByte,
                    out _,
                    out CompositionIssue? patchIssue))
            {
                issues.Add(patchIssue!);
                continue;
            }

            if (overlays.Any(existing => existing.Range.Overlaps(range)))
            {
                issues.Add(new CompositionIssue(
                    WorkbenchIssueCodes.GeneralReplacePatchOverlap,
                    $"Staged patch '{patch.PatchId}' overlaps another hexadecimal patch.",
                    patch.PatchId));
                continue;
            }

            overlays.Add((range, overwriteBytes, fillByte));
        }

        ByteRange viewportRange = new(alignedStart, before.Length);
        foreach ((ByteRange range, byte[]? overwriteBytes, byte? fillByte) in overlays)
        {
            ByteRange? intersection = range.Intersect(viewportRange);
            if (intersection is null)
            {
                continue;
            }

            ByteRange visible = intersection.Value;
            int sourceOffset = checked((int)(visible.Start - range.Start));
            int targetOffset = checked((int)(visible.Start - viewportRange.Start));
            if (overwriteBytes is not null)
            {
                overwriteBytes.AsSpan(sourceOffset, checked((int)visible.Length)).CopyTo(after.AsSpan(targetOffset));
            }
            else
            {
                after.AsSpan(targetOffset, checked((int)visible.Length)).Fill(fillByte!.Value);
            }
        }

        return new WorkbenchGeneralReplaceHexViewport(
            CreateGeneralReplaceHexViewportRows(alignedStart, before, after),
            issues,
            baseLength,
            alignedStart,
            before.Length);
    }

    private static bool TryReadGeneralReplaceHexViewportBase(
        string basePath,
        long requestedStart,
        out string? fullBasePath,
        out long baseLength,
        out long alignedStart,
        out byte[]? bytes,
        out CompositionIssue? issue)
    {
        fullBasePath = null;
        baseLength = 0;
        alignedStart = 0;
        bytes = null;
        try
        {
            fullBasePath = Path.GetFullPath(basePath);
            if (!File.Exists(fullBasePath))
            {
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    "Base flash BIN path does not exist for hexadecimal inspection.",
                    WorkbenchSlotIds.ReplaceBase);
                return false;
            }

            baseLength = new FileInfo(fullBasePath).Length;
            if (baseLength <= 0)
            {
                issue = new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    "Base flash BIN must not be empty for hexadecimal inspection.",
                    WorkbenchSlotIds.ReplaceBase);
                return false;
            }

            alignedStart = Math.Min(
                requestedStart - (requestedStart % GeneralReplaceHexViewportBytesPerRow),
                (baseLength - 1) / GeneralReplaceHexViewportBytesPerRow * GeneralReplaceHexViewportBytesPerRow);
            int length = checked((int)Math.Min(
                baseLength - alignedStart,
                (long)GeneralReplaceHexViewportBytesPerRow * GeneralReplaceHexViewportRowCount));
            bytes = new byte[length];
            using var stream = new FileStream(fullBasePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _ = stream.Seek(alignedStart, SeekOrigin.Begin);
            stream.ReadExactly(bytes);
            issue = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.InputArtifactReadFailed,
                "Base flash BIN could not be read for hexadecimal inspection.",
                WorkbenchSlotIds.ReplaceBase);
            return false;
        }
    }

    private static List<WorkbenchGeneralReplaceHexViewportRow> CreateGeneralReplaceHexViewportRows(
        long start,
        byte[] before,
        byte[] after)
    {
        List<WorkbenchGeneralReplaceHexViewportRow> rows = [];
        for (int offset = 0; offset < before.Length; offset += GeneralReplaceHexViewportBytesPerRow)
        {
            int length = Math.Min(GeneralReplaceHexViewportBytesPerRow, before.Length - offset);
            List<WorkbenchGeneralReplaceHexByte> bytes = [];
            for (int index = 0; index < length; index++)
            {
                bytes.Add(new WorkbenchGeneralReplaceHexByte(
                    checked(start + offset + index),
                    before[offset + index],
                    after[offset + index]));
            }

            ReadOnlySpan<byte> beforeRow = before.AsSpan(offset, length);
            ReadOnlySpan<byte> afterRow = after.AsSpan(offset, length);
            rows.Add(new WorkbenchGeneralReplaceHexViewportRow(
                checked(start + offset),
                bytes,
                FormatAscii(beforeRow),
                FormatAscii(afterRow)));
        }

        return rows;
    }

    private static string FormatGeneralReplaceHexRange(ByteRange range)
    {
        return $"0x{range.Start:X}-0x{range.EndExclusive - 1:X}";
    }

    private static string FormatAscii(ReadOnlySpan<byte> bytes)
    {
        Span<char> characters = stackalloc char[bytes.Length];
        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            characters[index] = value is >= 0x20 and <= 0x7E ? (char)value : '.';
        }

        return new string(characters);
    }

    private static WorkbenchGeneralReplaceHexViewport CreateGeneralReplaceHexViewportFailure(CompositionIssue issue)
    {
        return new WorkbenchGeneralReplaceHexViewport([], [issue], 0, 0, 0);
    }
}
