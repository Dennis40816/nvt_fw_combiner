using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Application-level request for an approved staged external processor transform.</summary>
public sealed class ExternalProcessorRequest
{
    private readonly byte[] _inputBytes;
    private readonly ByteRange[] _allowedWriteRanges;
    private readonly ExternalProcessorStagedSource[] _stagedSources;
    private readonly ExternalProcessorStagedArtifact[] _stagedArtifacts;

    /// <summary>Creates a transform request over a host-controlled staging copy.</summary>
    public ExternalProcessorRequest(
        string runId,
        string processorId,
        string toolBindingId,
        ReadOnlyMemory<byte> inputBytes,
        IEnumerable<ByteRange> allowedWriteRanges,
        IcNumberSelection? icNumberSelection = null,
        IEnumerable<ExternalProcessorStagedSource>? stagedSources = null,
        IEnumerable<ExternalProcessorStagedArtifact>? stagedArtifacts = null,
        int? resolvedIcCount = null,
        ExternalProcessorProtocolPlan? protocolPlan = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolBindingId);
        ArgumentNullException.ThrowIfNull(allowedWriteRanges);

        if (!IsSafeId(runId))
        {
            throw new ArgumentException("Run id must be a plain identifier.", nameof(runId));
        }

        if (inputBytes.Length == 0)
        {
            throw new ArgumentException("External processor input must not be empty.", nameof(inputBytes));
        }

        if (resolvedIcCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedIcCount),
                resolvedIcCount,
                "Resolved IC Count must be positive when supplied.");
        }

        RunId = runId;
        ProcessorId = processorId;
        ToolBindingId = toolBindingId;
        IcNumberSelection = icNumberSelection;
        ResolvedIcCount = resolvedIcCount;
        ProtocolPlan = protocolPlan;
        _inputBytes = inputBytes.ToArray();
        _allowedWriteRanges = [.. allowedWriteRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        _stagedSources = [
            .. (stagedSources ?? [])
                .OrderBy(source => source.FirmwareRange.Start)
                .ThenBy(source => source.FirmwareRange.Length),
        ];
        ExternalProcessorStagedArtifact[] artifacts = [.. stagedArtifacts ?? []];
        foreach (ExternalProcessorStagedSource source in _stagedSources)
        {
            if (source.FirmwareRange.EndExclusive > inputBytes.Length)
            {
                throw new ArgumentException("External processor staged source range is outside the input image.", nameof(stagedSources));
            }
        }

        if (artifacts.Any(static artifact => artifact is null) ||
            artifacts.Select(static artifact => artifact.ArtifactId).Distinct(StringComparer.Ordinal).Count() !=
            artifacts.Length)
        {
            throw new ArgumentException(
                "External processor staged artifacts must be non-null with unique artifact ids.",
                nameof(stagedArtifacts));
        }

        _stagedArtifacts = [.. artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal)];
    }

    /// <summary>Stable execution id used to name the private staging directory.</summary>
    public string RunId { get; }

    /// <summary>Profile-selected processor id.</summary>
    public string ProcessorId { get; }

    /// <summary>Manifest binding id selected by the profile.</summary>
    public string ToolBindingId { get; }

    /// <summary>Bytes materialized as the staging work file.</summary>
    public ReadOnlyMemory<byte> InputBytes => _inputBytes;

    /// <summary>Declared byte ranges the external processor may change.</summary>
    public IReadOnlyList<ByteRange> AllowedWriteRanges => _allowedWriteRanges;

    /// <summary>Optional source bytes staged for the processor without pre-writing them into <see cref="InputBytes"/>.</summary>
    public IReadOnlyList<ExternalProcessorStagedSource> StagedSources => _stagedSources;

    /// <summary>Named immutable artifact bytes staged separately from the target image.</summary>
    public IReadOnlyList<ExternalProcessorStagedArtifact> StagedArtifacts => _stagedArtifacts;

    /// <summary>Optional IC number context used by IC-specific postbuild processors.</summary>
    public IcNumberSelection? IcNumberSelection { get; }

    /// <summary>Exact IC Count already resolved by the compiler from the admitted topology facts.</summary>
    public int? ResolvedIcCount { get; }

    /// <summary>Exact compiled adapter protocol plan; adapters must not reconstruct it from runtime hints.</summary>
    public ExternalProcessorProtocolPlan? ProtocolPlan { get; }

    private static bool IsSafeId(string value)
    {
        return value is not ("." or "..") &&
            value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.');
    }
}
