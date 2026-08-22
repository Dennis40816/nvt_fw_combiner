namespace NvtFwCombiner.Application.Ports;

/// <summary>Commits successful build output bytes through an infrastructure adapter.</summary>
public interface ICompositionOutputWriter
{
    /// <summary>Writes output bytes atomically and returns typed delivery evidence.</summary>
    ValueTask<CompositionOutputCommitReceipt> CommitAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken);
}

/// <summary>Commits prepared dynamic artifacts in the same atomic bundle as the primary output.</summary>
public interface ICompositionOutputBundleWriter : ICompositionOutputWriter
{
    /// <summary>Stages the primary output and exact prepared additional artifacts before one promotion.</summary>
    ValueTask<CompositionOutputCommitReceipt> CommitBundleAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        IReadOnlyList<CompositionOutputBundleCommitArtifact> additionalArtifacts,
        CancellationToken cancellationToken);
}

/// <summary>One Application-prepared dynamic artifact committed within an atomic output bundle.</summary>
public sealed class CompositionOutputBundleCommitArtifact
{
    /// <summary>Creates one immutable dynamic bundle artifact.</summary>
    public CompositionOutputBundleCommitArtifact(
        string role,
        string bindingId,
        string suggestedFileName,
        ReadOnlyMemory<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        Role = role;
        BindingId = bindingId;
        SuggestedFileName = suggestedFileName;
        Bytes = bytes.ToArray();
    }

    /// <summary>Stable manifest role.</summary>
    public string Role { get; }

    /// <summary>Compiled declaration binding retained as provenance.</summary>
    public string BindingId { get; }

    /// <summary>Prepared plain filename before deterministic collision allocation.</summary>
    public string SuggestedFileName { get; }

    /// <summary>Immutable bytes sliced from the completed primary output.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }
}

/// <summary>Typed evidence returned after one primary output commit.</summary>
public sealed class CompositionOutputCommitReceipt
{
    /// <summary>Creates typed evidence for one legacy loose-file commit.</summary>
    public static CompositionOutputCommitReceipt CreateLoose(
        string outputId,
        string outputFileName,
        ReadOnlySpan<byte> bytes)
    {
        return new CompositionOutputCommitReceipt(
            outputId,
            outputFileName,
            bytes.Length,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
    }

    /// <summary>Creates one loose or bundled output receipt.</summary>
    public CompositionOutputCommitReceipt(
        string outputId,
        string outputFileName,
        long outputSize,
        string outputSha256,
        CompositionOutputBundleCommitReceipt? bundle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(outputSize);
        RequireSha256(outputSha256, nameof(outputSha256));
        OutputId = outputId;
        OutputFileName = outputFileName;
        OutputSize = outputSize;
        OutputSha256 = outputSha256;
        Bundle = bundle;
    }

    /// <summary>Adapter-owned primary output destination id.</summary>
    public string OutputId { get; }

    /// <summary>Delivered canonical primary filename.</summary>
    public string OutputFileName { get; }

    /// <summary>Delivered primary byte count.</summary>
    public long OutputSize { get; }

    /// <summary>Lowercase SHA-256 of delivered primary bytes.</summary>
    public string OutputSha256 { get; }

    /// <summary>Atomic bundle evidence, or null for the legacy loose output path.</summary>
    public CompositionOutputBundleCommitReceipt? Bundle { get; }

    internal static void RequireSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 || value.Any(static character =>
                character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f'))))
        {
            throw new ArgumentException(
                "SHA-256 must be one lowercase hexadecimal digest.",
                parameterName);
        }
    }
}

/// <summary>Typed evidence for one atomically promoted output bundle.</summary>
public sealed class CompositionOutputBundleCommitReceipt
{
    /// <summary>Creates one complete promoted-directory receipt.</summary>
    public CompositionOutputBundleCommitReceipt(
        string resolvedDirectory,
        IReadOnlyList<CompositionOutputBundleArtifactReceipt> artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedDirectory);
        ArgumentNullException.ThrowIfNull(artifacts);
        ResolvedDirectory = resolvedDirectory;
        Artifacts = Array.AsReadOnly([.. artifacts]);
    }

    /// <summary>Actual suffix-resolved promoted directory.</summary>
    public string ResolvedDirectory { get; }

    /// <summary>Actual delivered names and hashes: output, additional deliveries, then sources.</summary>
    public IReadOnlyList<CompositionOutputBundleArtifactReceipt> Artifacts { get; }
}

/// <summary>One actual file inside a promoted output bundle.</summary>
public sealed class CompositionOutputBundleArtifactReceipt
{
    /// <summary>Creates one delivered output, additional-delivery, or source artifact receipt.</summary>
    public CompositionOutputBundleArtifactReceipt(
        string role,
        string? bindingId,
        string deliveredFileName,
        long size,
        string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveredFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        CompositionOutputCommitReceipt.RequireSha256(sha256, nameof(sha256));
        Role = role;
        BindingId = bindingId;
        DeliveredFileName = deliveredFileName;
        Size = size;
        Sha256 = sha256;
    }

    /// <summary>Stable role: output, additional-delivery, or source.</summary>
    public string Role { get; }

    /// <summary>Canonical source or compiled-delivery binding id; null for the primary output.</summary>
    public string? BindingId { get; }

    /// <summary>Actual collision-resolved filename.</summary>
    public string DeliveredFileName { get; }

    /// <summary>Delivered byte count.</summary>
    public long Size { get; }

    /// <summary>Lowercase SHA-256 of delivered bytes.</summary>
    public string Sha256 { get; }
}
