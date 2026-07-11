using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const int GeneralReplaceHexViewportBytesPerRow = 16;
    private const int GeneralReplaceHexViewportRowCount = 32;
    private const int GeneralReplaceHexViewportContextRows = 4;

    /// <summary>Reads a selected base BIN once into an immutable General Replace session snapshot.</summary>
    public static bool TryLoadGeneralReplaceBaseSnapshot(
        string basePath,
        out WorkbenchGeneralReplaceBaseSnapshot? snapshot,
        out CompositionIssue? issue)
    {
        snapshot = null;
        try
        {
            string fullPath = Path.GetFullPath(basePath);
            if (!File.Exists(fullPath))
            {
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    "Base flash BIN path does not exist for hexadecimal inspection.",
                    WorkbenchSlotIds.ReplaceBase);
                return false;
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length == 0)
            {
                issue = new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    "Base flash BIN must not be empty for hexadecimal inspection.",
                    WorkbenchSlotIds.ReplaceBase);
                return false;
            }

            snapshot = new WorkbenchGeneralReplaceBaseSnapshot(fullPath, bytes);
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

    /// <summary>Loads one immutable base image snapshot and exposes a UI-safe error message.</summary>
    public static bool TryLoadGeneralReplaceBaseSnapshot(
        string basePath,
        out WorkbenchGeneralReplaceBaseSnapshot? snapshot,
        out string? errorMessage)
    {
        bool loaded = TryLoadGeneralReplaceBaseSnapshot(basePath, out snapshot, out CompositionIssue? issue);
        errorMessage = issue?.Message;
        return loaded;
    }

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

        return viewportStart < 0
            ? CreateGeneralReplaceHexViewportFailure(new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                "Hexadecimal viewport start must be zero or greater.",
                WorkbenchSlotIds.ReplaceBase))
            : TryReadGeneralReplaceHexViewportBase(
            basePath,
            viewportStart,
            out long baseLength,
            out long alignedStart,
            out byte[]? before,
            out CompositionIssue? readIssue)
                ? CreateGeneralReplaceHexViewport(baseLength, alignedStart, before!, patches)
                : CreateGeneralReplaceHexViewportFailure(readIssue!);
    }

    /// <summary>Creates a hexadecimal viewport from one immutable in-memory base snapshot.</summary>
    public static WorkbenchGeneralReplaceHexViewport CreateGeneralReplaceHexViewport(
        WorkbenchGeneralReplaceBaseSnapshot snapshot,
        long viewportStart,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patches)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(patches);

        return viewportStart < 0
            ? CreateGeneralReplaceHexViewportFailure(new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                "Hexadecimal viewport start must be zero or greater.",
                WorkbenchSlotIds.ReplaceBase))
            : TryReadGeneralReplaceHexViewportBase(
            snapshot,
            viewportStart,
            out long alignedStart,
            out byte[]? before,
            out CompositionIssue? readIssue)
                ? CreateGeneralReplaceHexViewport(snapshot.Length, alignedStart, before!, patches)
                : CreateGeneralReplaceHexViewportFailure(readIssue!);
    }

    private static WorkbenchGeneralReplaceHexViewport CreateGeneralReplaceHexViewport(
        long baseLength,
        long alignedStart,
        byte[] before,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patches)
    {
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
        out long baseLength,
        out long alignedStart,
        out byte[]? bytes,
        out CompositionIssue? issue)
    {
        baseLength = 0;
        alignedStart = 0;
        bytes = null;
        try
        {
            string fullBasePath = Path.GetFullPath(basePath);
            if (!File.Exists(fullBasePath))
            {
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.InputArtifactReadFailed,
                    "Base flash BIN path does not exist for hexadecimal inspection.",
                    WorkbenchSlotIds.ReplaceBase);
                return false;
            }

            baseLength = new FileInfo(fullBasePath).Length;
            if (!TryCalculateGeneralReplaceHexViewportWindow(
                    baseLength,
                    requestedStart,
                    out alignedStart,
                    out int length,
                    out issue))
            {
                return false;
            }

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

    private static bool TryReadGeneralReplaceHexViewportBase(
        WorkbenchGeneralReplaceBaseSnapshot snapshot,
        long requestedStart,
        out long alignedStart,
        out byte[]? bytes,
        out CompositionIssue? issue)
    {
        alignedStart = 0;
        bytes = null;
        if (!TryCalculateGeneralReplaceHexViewportWindow(
                snapshot.Length,
                requestedStart,
                out alignedStart,
                out int length,
                out issue))
        {
            return false;
        }

        bytes = snapshot.AsSpan().Slice(checked((int)alignedStart), length).ToArray();
        return true;
    }

    private static bool TryCalculateGeneralReplaceHexViewportWindow(
        long baseLength,
        long requestedStart,
        out long alignedStart,
        out int length,
        out CompositionIssue? issue)
    {
        alignedStart = 0;
        length = 0;
        if (baseLength <= 0)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                "Base flash BIN must not be empty for hexadecimal inspection.",
                WorkbenchSlotIds.ReplaceBase);
            return false;
        }

        if (requestedStart >= baseLength)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"Hexadecimal address 0x{requestedStart:X} is outside the {baseLength}-byte base flash BIN.",
                WorkbenchSlotIds.ReplaceBase);
            return false;
        }

        long requestedAlignedStart = requestedStart - (requestedStart % GeneralReplaceHexViewportBytesPerRow);
        long contextualStart = Math.Max(
            0,
            requestedAlignedStart - (GeneralReplaceHexViewportContextRows * GeneralReplaceHexViewportBytesPerRow));
        alignedStart = Math.Min(
            contextualStart,
            (baseLength - 1) / GeneralReplaceHexViewportBytesPerRow * GeneralReplaceHexViewportBytesPerRow);
        length = checked((int)Math.Min(
            baseLength - alignedStart,
            (long)GeneralReplaceHexViewportBytesPerRow * GeneralReplaceHexViewportRowCount));
        issue = null;
        return true;
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
