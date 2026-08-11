using System.Security.Cryptography;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Content-authoritative identity of accepted selected-file bytes. Paths,
/// display names, and filesystem timestamps are deliberately excluded.
/// </summary>
public readonly record struct FileStamp
{
    /// <summary>Creates one accepted length and SHA-256 identity.</summary>
    public FileStamp(long acceptedLength, string sha256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (sha256.Length != SHA256.HashSizeInBytes * 2 ||
            sha256.Any(static character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "File-stamp SHA-256 must contain 64 lowercase hexadecimal characters.",
                nameof(sha256));
        }

        AcceptedLength = acceptedLength;
        Sha256 = sha256;
    }

    /// <summary>Accepted byte length.</summary>
    public long AcceptedLength { get; }

    /// <summary>Lowercase SHA-256 of the accepted complete file bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an identity from one immutable byte view.</summary>
    public static FileStamp FromBytes(ReadOnlySpan<byte> bytes)
    {
        return new FileStamp(
            bytes.Length,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
