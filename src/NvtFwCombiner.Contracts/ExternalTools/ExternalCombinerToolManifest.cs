namespace NvtFwCombiner.Contracts.ExternalTools;

/// <summary>Declarative launch contract for an approved legacy external combiner executable.</summary>
public sealed class ExternalCombinerToolManifest(
    string schemaVersion,
    string toolBindingId,
    string toolId,
    string toolVersion,
    string displayName,
    string platform,
    string executableName,
    string sha256,
    string adapterId,
    string inputMode,
    IReadOnlyList<string> argumentTemplate,
    string workingDirectoryPolicy,
    int timeoutSeconds,
    IReadOnlyList<string> allowedExtraOutputFiles)
{
    /// <summary>Manifest schema version.</summary>
    public string SchemaVersion { get; } = schemaVersion;

    /// <summary>Unique binding id used by composition profiles.</summary>
    public string ToolBindingId { get; } = toolBindingId;

    /// <summary>Stable logical tool family id.</summary>
    public string ToolId { get; } = toolId;

    /// <summary>Exact tool version string. Values such as 1.10 must not be parsed as floating-point numbers.</summary>
    public string ToolVersion { get; } = toolVersion;

    /// <summary>Human-readable tool name.</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>Runtime platform supported by the executable.</summary>
    public string Platform { get; } = platform;

    /// <summary>Plain executable file name inside the approved tool package.</summary>
    public string ExecutableName { get; } = executableName;

    /// <summary>Lowercase SHA-256 of the executable.</summary>
    public string Sha256 { get; } = sha256;

    /// <summary>Adapter id describing how to call and interpret the tool.</summary>
    public string AdapterId { get; } = adapterId;

    /// <summary>Tool input/output convention.</summary>
    public string InputMode { get; } = inputMode;

    /// <summary>Argument template using only approved host-expanded staging tokens.</summary>
    public IReadOnlyList<string> ArgumentTemplate { get; } = argumentTemplate;

    /// <summary>Policy for the process working directory.</summary>
    public string WorkingDirectoryPolicy { get; } = workingDirectoryPolicy;

    /// <summary>Maximum execution time in seconds.</summary>
    public int TimeoutSeconds { get; } = timeoutSeconds;

    /// <summary>Declared extra output files that the host may tolerate in the staging directory.</summary>
    public IReadOnlyList<string> AllowedExtraOutputFiles { get; } = allowedExtraOutputFiles;
}
