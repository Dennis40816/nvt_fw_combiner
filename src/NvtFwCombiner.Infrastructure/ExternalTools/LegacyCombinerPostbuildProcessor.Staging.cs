using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

public sealed partial class LegacyCombinerPostbuildProcessor
{
    private static CompositionIssue? ApplyStagedSourceOverrides(
        byte[] stagedSourceBytes,
        IReadOnlyList<ExternalProcessorStagedSource> stagedSources)
    {
        if (stagedSources.Count == 0)
        {
            return null;
        }

        bool[] written = new bool[stagedSourceBytes.Length];
        foreach (ExternalProcessorStagedSource source in stagedSources)
        {
            if (source.FirmwareRange.EndExclusive > stagedSourceBytes.LongLength)
            {
                return new CompositionIssue(
                    "legacy-combiner.staged-source.range-outside-input",
                    "Staged source bytes target a range outside the staged firmware image.");
            }

            ReadOnlySpan<byte> bytes = source.Bytes.Span;
            int targetStart = checked((int)source.FirmwareRange.Start);
            for (int index = 0; index < bytes.Length; index++)
            {
                int targetIndex = targetStart + index;
                if (written[targetIndex] && stagedSourceBytes[targetIndex] != bytes[index])
                {
                    return new CompositionIssue(
                        "legacy-combiner.staged-source.conflict",
                        $"Staged source bytes conflict at firmware offset 0x{targetIndex:X}.");
                }

                stagedSourceBytes[targetIndex] = bytes[index];
                written[targetIndex] = true;
            }
        }

        return null;
    }

    private static CompositionIssue? MaterializeStagedBlockFiles(
        IReadOnlyList<LegacyCombinerBlockArgument> blocks,
        byte[] firmwareBytes,
        string binDirectory)
    {
        Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, bool[]> written = new(StringComparer.OrdinalIgnoreCase);
        foreach (LegacyCombinerBlockArgument block in blocks
            .Where(block => block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
            .OrderBy(block => block.SourceFileName, StringComparer.Ordinal)
            .ThenBy(block => block.SourceOffset)
            .ThenBy(block => block.FirmwareRange.Start))
        {
            if (block.FirmwareRange.EndExclusive > firmwareBytes.LongLength)
            {
                return new CompositionIssue(
                    "legacy-combiner.staging.range-outside-input",
                    $"Postbuild block '{block.BlockId}' reads outside the staged firmware image.",
                    block.BlockId);
            }

            long requiredLength = checked(block.SourceOffset + block.FirmwareRange.Length);
            byte[] fileBytes = GetOrGrow(files, block.SourceFileName, requiredLength);
            bool[] writtenBytes = GetOrGrow(written, block.SourceFileName, requiredLength);
            ReadOnlySpan<byte> sourceBytes = firmwareBytes.AsSpan(
                (int)block.FirmwareRange.Start,
                (int)block.FirmwareRange.Length);
            int targetStart = checked((int)block.SourceOffset);
            for (int index = 0; index < sourceBytes.Length; index++)
            {
                int targetIndex = targetStart + index;
                if (writtenBytes[targetIndex] && fileBytes[targetIndex] != sourceBytes[index])
                {
                    return new CompositionIssue(
                        "legacy-combiner.staging.projection-conflict",
                        $"Postbuild block '{block.BlockId}' writes conflicting bytes to staged file '{block.SourceFileName}' at offset 0x{targetIndex:X}.",
                        block.BlockId);
                }

                fileBytes[targetIndex] = sourceBytes[index];
                writtenBytes[targetIndex] = true;
            }
        }

        foreach ((string fileName, byte[] bytes) in files)
        {
            File.WriteAllBytes(Path.Combine(binDirectory, fileName), bytes);
        }

        return null;
    }

    private static void ResetDirectory(string directory)
    {
        foreach (string filePath in Directory.EnumerateFiles(directory))
        {
            File.Delete(filePath);
        }
    }

    private static async ValueTask<CompositionIssue?> NormalizeShortenedFirmwareAsync(
        string firmwarePath,
        byte[] commandInputBytes,
        LegacyCombinerPostbuildCommand command,
        CancellationToken cancellationToken)
    {
        byte[] commandOutputBytes = await File.ReadAllBytesAsync(firmwarePath, cancellationToken).ConfigureAwait(false);
        if (commandOutputBytes.LongLength == commandInputBytes.LongLength)
        {
            return null;
        }

        if (commandOutputBytes.LongLength > commandInputBytes.LongLength)
        {
            return new CompositionIssue(
                "external-tool.output-length.changed",
                "External processor changed the firmware image length.",
                command.CommandId);
        }

        long minimumLength = GetMinimumCommandOutputLength(command, commandInputBytes.LongLength);
        if (commandOutputBytes.LongLength < minimumLength)
        {
            return new CompositionIssue(
                "external-tool.output-length.changed",
                $"External processor command '{command.CommandId}' shortened the firmware image below its declared write coverage.",
                command.CommandId);
        }

        byte[] normalizedBytes = [.. commandInputBytes];
        commandOutputBytes.CopyTo(normalizedBytes, 0);
        await File.WriteAllBytesAsync(firmwarePath, normalizedBytes, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static long GetMinimumCommandOutputLength(
        LegacyCombinerPostbuildCommand command,
        long originalLength)
    {
        return command.Blocks.Count == 0
            ? originalLength
            : command.Blocks.Max(block => block.FirmwareRange.EndExclusive);
    }

    private static byte[] GetOrGrow(Dictionary<string, byte[]> files, string fileName, long requiredLength)
    {
        if (requiredLength > int.MaxValue)
        {
            throw new IOException("Staged block file exceeds supported runtime length.");
        }

        int length = checked((int)requiredLength);
        if (!files.TryGetValue(fileName, out byte[]? bytes))
        {
            bytes = new byte[length];
            files.Add(fileName, bytes);
            return bytes;
        }

        if (bytes.Length >= length)
        {
            return bytes;
        }

        Array.Resize(ref bytes, length);
        files[fileName] = bytes;
        return bytes;
    }

    private static bool[] GetOrGrow(Dictionary<string, bool[]> files, string fileName, long requiredLength)
    {
        if (requiredLength > int.MaxValue)
        {
            throw new IOException("Staged block file exceeds supported runtime length.");
        }

        int length = checked((int)requiredLength);
        if (!files.TryGetValue(fileName, out bool[]? bytes))
        {
            bytes = new bool[length];
            files.Add(fileName, bytes);
            return bytes;
        }

        if (bytes.Length >= length)
        {
            return bytes;
        }

        Array.Resize(ref bytes, length);
        files[fileName] = bytes;
        return bytes;
    }

    private static CompositionIssue? ValidateStagingTree(
        string runDirectory,
        LegacyCombinerPostbuildProfile profile,
        ExternalCombinerToolManifest manifest,
        LegacyCombinerPostbuildCommandPlan commandPlan)
    {
        HashSet<string> allowedRelativePaths = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(OutputDirectoryName, profile.FirmwareFileName),
            Path.Combine(OutputDirectoryName, MapFileName),
        };
        foreach (string stagedFileName in LegacyCombinerPostbuildPlanner
            .GetStagedFileBlocks(commandPlan)
            .Select(block => block.SourceFileName)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _ = allowedRelativePaths.Add(Path.Combine(BinDirectoryName, stagedFileName));
        }

        foreach (string extraFileName in manifest.AllowedExtraOutputFiles)
        {
            _ = allowedRelativePaths.Add(Path.Combine(OutputDirectoryName, extraFileName));
        }

        HashSet<string> allowedRelativeDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            OutputDirectoryName,
            BinDirectoryName,
        };
        foreach (string directoryPath in Directory.EnumerateDirectories(runDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(runDirectory, directoryPath);
            if (!allowedRelativeDirectories.Contains(relativePath))
            {
                return new CompositionIssue(
                    "external-tool.staging.unexpected-directory",
                    $"External processor left unexpected staging directory '{relativePath}'.");
            }
        }

        foreach (string filePath in Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(runDirectory, filePath);
            if (!allowedRelativePaths.Contains(relativePath))
            {
                return new CompositionIssue(
                    "external-tool.staging.unexpected-file",
                    $"External processor left unexpected staging file '{relativePath}'.");
            }
        }

        return null;
    }
}
