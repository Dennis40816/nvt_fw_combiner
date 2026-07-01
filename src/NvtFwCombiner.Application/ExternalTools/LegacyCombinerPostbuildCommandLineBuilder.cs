using System.Globalization;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Creates Combiner.exe argv sequences from normalized postbuild command data.</summary>
public static class LegacyCombinerPostbuildCommandLineBuilder
{
    /// <summary>Creates the argument list passed after the executable path.</summary>
    public static IReadOnlyList<string> CreateArguments(
        LegacyCombinerPostbuildCommand command,
        string firmwarePath,
        string binDirectory)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(firmwarePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(binDirectory);

        List<string> arguments = command.Family switch
        {
            LegacyCombinerCommandFamily.NormalMode => [command.ModeArgument, firmwarePath],
            LegacyCombinerCommandFamily.MergeMode => [command.ModeArgument, firmwarePath],
            LegacyCombinerCommandFamily.NtBasedNormalMode => [
                command.ModeArgument,
                command.CrcArgument!,
                firmwarePath,
                firmwarePath,
            ],
            LegacyCombinerCommandFamily.CrcOnlyMode => [
                command.ModeArgument,
                command.CrcArgument!,
                firmwarePath,
                firmwarePath,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(command), "Unsupported legacy combiner command family."),
        };

        foreach (LegacyCombinerBlockArgument block in command.Blocks)
        {
            string sourcePath = block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage
                ? firmwarePath
                : Path.Combine(binDirectory, block.SourceFileName);
            arguments.Add(sourcePath);
            arguments.Add(FormatHex(block.SourceOffset));
            arguments.Add(FormatHex(block.FirmwareRange.Start));
            arguments.Add(block.FirmwareRange.Length.ToString(CultureInfo.InvariantCulture));
        }

        return arguments;
    }

    private static string FormatHex(long value)
    {
        return FormattableString.Invariant($"0x{value:X}");
    }
}
