namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>One Combiner.exe process invocation from a postbuild branch.</summary>
public sealed class LegacyCombinerPostbuildCommand
{
    private readonly LegacyCombinerBlockArgument[] _blocks;

    /// <summary>Creates a postbuild command declaration.</summary>
    public LegacyCombinerPostbuildCommand(
        string commandId,
        LegacyCombinerCommandFamily family,
        string modeArgument,
        string? crcArgument,
        IEnumerable<LegacyCombinerBlockArgument> blocks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeArgument);
        ArgumentNullException.ThrowIfNull(blocks);

        _blocks = [.. blocks];
        if (_blocks.Length == 0 && family != LegacyCombinerCommandFamily.CrcOnlyMode)
        {
            throw new ArgumentException("Postbuild command must contain at least one block unless it is CRC-only.", nameof(blocks));
        }

        if ((family == LegacyCombinerCommandFamily.NtBasedNormalMode ||
             family == LegacyCombinerCommandFamily.CrcOnlyMode) &&
            string.IsNullOrWhiteSpace(crcArgument))
        {
            throw new ArgumentException("This command family requires a CRC argument.", nameof(crcArgument));
        }

        if ((family == LegacyCombinerCommandFamily.NormalMode ||
             family == LegacyCombinerCommandFamily.MergeMode) &&
            !string.IsNullOrWhiteSpace(crcArgument))
        {
            throw new ArgumentException("This command family encodes CRC selection in the mode argument.", nameof(crcArgument));
        }

        CommandId = commandId;
        Family = family;
        ModeArgument = modeArgument;
        CrcArgument = string.IsNullOrWhiteSpace(crcArgument) ? null : crcArgument;
    }

    /// <summary>Stable command id used in diagnostics.</summary>
    public string CommandId { get; }

    /// <summary>Command family determining argv layout.</summary>
    public LegacyCombinerCommandFamily Family { get; }

    /// <summary>First Combiner.exe argument.</summary>
    public string ModeArgument { get; }

    /// <summary>CRC method argument for NT-based normal mode commands.</summary>
    public string? CrcArgument { get; }

    /// <summary>Block arguments in postbuild order.</summary>
    public IReadOnlyList<LegacyCombinerBlockArgument> Blocks => _blocks;
}
