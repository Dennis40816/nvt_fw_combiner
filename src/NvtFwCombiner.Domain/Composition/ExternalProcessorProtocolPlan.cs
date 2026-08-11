namespace NvtFwCombiner.Domain.Composition;

/// <summary>One fully selected protocol plan carried by a compiled processor invocation.</summary>
public sealed class ExternalProcessorProtocolPlan
{
    private readonly ExternalProcessorProtocolCommand[] _commands;

    /// <summary>Creates an immutable ordered protocol plan.</summary>
    public ExternalProcessorProtocolPlan(
        string protocolId,
        string targetFileName,
        IEnumerable<ExternalProcessorProtocolCommand> commands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolId);
        _ = RequirePlainFileName(targetFileName, nameof(targetFileName));
        _commands = ImmutableReferenceSnapshot.Create(
            commands,
            "External processor protocol commands must be non-null and nonempty.",
            parameterName: nameof(commands));
        DomainInvariant.Reject(
            _commands.Length == 0 ||
            _commands.Select(static command => command.CommandId)
                .Distinct(StringComparer.Ordinal).Count() != _commands.Length,
            "External processor protocol commands must be nonempty with unique ids.",
            nameof(commands));

        ProtocolId = protocolId;
        TargetFileName = targetFileName;
        Commands = Array.AsReadOnly(_commands);
    }

    /// <summary>Closed adapter protocol identifier.</summary>
    public string ProtocolId { get; }

    /// <summary>Plain target-image file name materialized in the private staging directory.</summary>
    public string TargetFileName { get; }

    /// <summary>Exact process commands in compiled execution order.</summary>
    public IReadOnlyList<ExternalProcessorProtocolCommand> Commands { get; }

    internal static string RequirePlainFileName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.IndexOfAny(['/', '\\', ':']) >= 0 ||
            value is "." or ".." ||
            Path.GetFileName(value) != value
                ? throw new ArgumentException("External processor staging file names must be plain file names.", parameterName)
                : value;
    }
}

/// <summary>One exact process invocation in a compiled external-processor protocol plan.</summary>
public sealed class ExternalProcessorProtocolCommand
{
    private readonly string[] _arguments;
    private readonly ExternalProcessorProtocolBlock[] _blocks;

    /// <summary>Creates one immutable ordered command.</summary>
    public ExternalProcessorProtocolCommand(
        string commandId,
        IEnumerable<string> arguments,
        IEnumerable<ExternalProcessorProtocolBlock> blocks,
        bool retainShortOutputTail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = [.. arguments];
        DomainInvariant.Reject(
            _arguments.Length == 0 || _arguments.Any(string.IsNullOrWhiteSpace),
            "External processor protocol arguments must be nonempty and nonblank.",
            nameof(arguments));
        _blocks = ImmutableReferenceSnapshot.Create(
            blocks,
            "External processor protocol blocks must not contain null entries.",
            parameterName: nameof(blocks));
        DomainInvariant.Reject(
            _blocks.Select(static block => block.BlockId)
                .Distinct(StringComparer.Ordinal).Count() != _blocks.Length,
            "External processor protocol block ids must be unique within one command.",
            nameof(blocks));

        CommandId = commandId;
        Arguments = Array.AsReadOnly(_arguments);
        Blocks = Array.AsReadOnly(_blocks);
        RetainShortOutputTail = retainShortOutputTail;
    }

    /// <summary>Stable command id used in diagnostics and audit.</summary>
    public string CommandId { get; }

    /// <summary>Exact argument vector supplied after the resolved executable path.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Exact staged-file blocks consumed by this command.</summary>
    public IReadOnlyList<ExternalProcessorProtocolBlock> Blocks { get; }

    /// <summary>Whether a shortened merge-mode output retains the untouched original tail.</summary>
    public bool RetainShortOutputTail { get; }
}

/// <summary>One compiled staged-file block consumed by an external processor command.</summary>
public sealed class ExternalProcessorProtocolBlock
{
    /// <summary>Creates one checked compiled block.</summary>
    public ExternalProcessorProtocolBlock(
        string blockId,
        ExternalProcessorProtocolBlockSourceKind sourceKind,
        string sourceFileName,
        long sourceOffset,
        ByteRange firmwareRange,
        string? stagedArtifactId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        ClosedEnum.ThrowIfUndefined(sourceKind, "Unknown external processor protocol block source kind.");
        _ = ExternalProcessorProtocolPlan.RequirePlainFileName(sourceFileName, nameof(sourceFileName));
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        if (sourceKind == ExternalProcessorProtocolBlockSourceKind.StagedArtifact && stagedArtifactId is null)
        {
            ExternalProcessorStagedArtifact.ValidateArtifactId(string.Empty, nameof(stagedArtifactId));
        }
        else if (stagedArtifactId is not null)
        {
            DomainInvariant.Reject(
                sourceKind is not (ExternalProcessorProtocolBlockSourceKind.StagedFile or
                    ExternalProcessorProtocolBlockSourceKind.StagedArtifact),
                "Only staged-file protocol blocks can declare a staged artifact id.",
                nameof(stagedArtifactId));
            ExternalProcessorStagedArtifact.ValidateArtifactId(stagedArtifactId, nameof(stagedArtifactId));
        }

        BlockId = blockId;
        SourceKind = sourceKind;
        SourceFileName = sourceFileName;
        SourceOffset = sourceOffset;
        FirmwareRange = firmwareRange;
        StagedArtifactId = stagedArtifactId;
    }

    /// <summary>Stable block id used in diagnostics.</summary>
    public string BlockId { get; }

    /// <summary>Compiled source kind for staging materialization.</summary>
    public ExternalProcessorProtocolBlockSourceKind SourceKind { get; }

    /// <summary>Plain file name materialized under the staged BIN directory.</summary>
    public string SourceFileName { get; }

    /// <summary>Source-file offset passed to the external tool.</summary>
    public long SourceOffset { get; }

    /// <summary>Target-image range associated with this block.</summary>
    public ByteRange FirmwareRange { get; }

    /// <summary>Optional immutable staged artifact supplying exact source-file bytes.</summary>
    public string? StagedArtifactId { get; }
}

/// <summary>Closed source kinds for compiled external-processor staged blocks.</summary>
public enum ExternalProcessorProtocolBlockSourceKind
{
    /// <summary>Read source bytes from the staged target image.</summary>
    TargetImage,

    /// <summary>Read a staged file projected from declared source bytes.</summary>
    StagedFile,

    /// <summary>Read a named immutable staged artifact.</summary>
    StagedArtifact,
}

/// <summary>Closed host-materialized path tokens allowed in compiled protocol argv.</summary>
public static class ExternalProcessorProtocolArgumentTokens
{
    /// <summary>Host-created staged target-image path.</summary>
    public const string TargetFile = "$target-file";

    /// <summary>Host-created staged BIN directory prefix.</summary>
    public const string StagedDirectory = "$staged-directory";
}
