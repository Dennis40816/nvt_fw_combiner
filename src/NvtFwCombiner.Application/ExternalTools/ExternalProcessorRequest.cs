using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Application-level request for an approved staged external processor transform.</summary>
public sealed class ExternalProcessorRequest
{
    private readonly byte[] _inputBytes;
    private readonly ByteRange[] _allowedWriteRanges;

    /// <summary>Creates a transform request over a host-controlled staging copy.</summary>
    public ExternalProcessorRequest(
        string runId,
        string processorId,
        string toolBindingId,
        ReadOnlyMemory<byte> inputBytes,
        IEnumerable<ByteRange> allowedWriteRanges,
        IcNumberSelection? icNumberSelection = null)
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

        RunId = runId;
        ProcessorId = processorId;
        ToolBindingId = toolBindingId;
        IcNumberSelection = icNumberSelection;
        _inputBytes = inputBytes.ToArray();
        _allowedWriteRanges = [.. allowedWriteRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
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

    /// <summary>Optional IC number context used by IC-specific postbuild processors.</summary>
    public IcNumberSelection? IcNumberSelection { get; }

    private static bool IsSafeId(string value)
    {
        return value is not ("." or "..") &&
            value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.');
    }
}
