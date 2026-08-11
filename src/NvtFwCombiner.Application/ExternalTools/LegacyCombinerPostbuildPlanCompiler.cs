using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Lowers one already-selected owner-reviewed Combiner plan into the immutable compiled protocol contract.</summary>
public static partial class LegacyCombinerPostbuildPlanCompiler
{
    /// <summary>Closed protocol id consumed only by the matching Infrastructure adapter.</summary>
    public const string ProtocolId = "legacy-combiner-postbuild-v1";

    internal static ExternalProcessorProtocolPlan CompileProtocol(
        LegacyCombinerPostbuildProfile profile,
        IEnumerable<LegacyCombinerPostbuildCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(commands);
        return new ExternalProcessorProtocolPlan(
            ProtocolId,
            profile.FirmwareFileName,
            commands.Select(command => new ExternalProcessorProtocolCommand(
                command.CommandId,
                LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
                    command,
                    ExternalProcessorProtocolArgumentTokens.TargetFile,
                    ExternalProcessorProtocolArgumentTokens.StagedDirectory),
                command.Blocks.Select(static block => new ExternalProcessorProtocolBlock(
                    block.BlockId,
                    block.SourceKind switch
                    {
                        LegacyCombinerBlockSourceKind.FirmwareImage =>
                            ExternalProcessorProtocolBlockSourceKind.TargetImage,
                        LegacyCombinerBlockSourceKind.StagedFile =>
                            ExternalProcessorProtocolBlockSourceKind.StagedFile,
                        LegacyCombinerBlockSourceKind.StagedArtifact =>
                            ExternalProcessorProtocolBlockSourceKind.StagedArtifact,
                        _ => throw new ArgumentOutOfRangeException(nameof(block), "Unsupported postbuild block source kind."),
                    },
                    block.SourceFileName,
                    block.SourceOffset,
                    block.FirmwareRange,
                    block.StagedArtifactId)),
                command.Family == LegacyCombinerCommandFamily.MergeMode)));
    }
}
