namespace NvtFwCombiner.Contracts.ExternalTools;

/// <summary>Declarative launch contract for an approved legacy external combiner executable.</summary>
public sealed record ExternalCombinerToolManifest(
    string SchemaVersion,
    string ToolBindingId,
    string ToolId,
    string ToolVersion,
    string DisplayName,
    string Platform,
    string ExecutableName,
    string Sha256,
    string AdapterId,
    string InputMode,
    IReadOnlyList<string> ArgumentTemplate,
    string WorkingDirectoryPolicy,
    int TimeoutSeconds,
    IReadOnlyList<string> AllowedExtraOutputFiles);
