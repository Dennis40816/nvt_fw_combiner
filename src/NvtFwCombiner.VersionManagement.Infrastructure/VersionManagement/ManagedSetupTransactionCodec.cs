using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Single strict bounded codec shared by the Setup writer and recovery reader.</summary>
internal static class ManagedSetupTransactionCodec
{
    internal const int MaximumDocumentBytes = 64 * 1024;
    internal const string StagingPhase = "staging";
    internal const string RootPromotedPhase = "root-promoted";
    internal const string BootstrapLaunchRecordedPhase = "bootstrap-launch-recorded";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 16,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);

    internal static ManagedSetupTransactionDocument? Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is < 1 or > MaximumDocumentBytes)
        {
            return null;
        }
        try
        {
            using JsonDocument document = EmbeddedVersionManagementSchema.ParseStrict(
                bytes,
                maximumDepth: 16);
            return ManagedSetupTransactionSchema.IsValid(document.RootElement)
                ? JsonSerializer.Deserialize(
                    document.RootElement,
                    JsonContext.ManagedSetupTransactionDocument)
                : null;
        }
        catch (Exception exception) when (exception is
            JsonException or InvalidDataException or InvalidOperationException)
        {
            return null;
        }
    }

    internal static byte[] Serialize(ManagedSetupTransactionDocument marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            marker,
            JsonContext.ManagedSetupTransactionDocument);
        return bytes.Length <= MaximumDocumentBytes && Parse(bytes) is not null
            ? bytes
            : throw new InvalidDataException("Setup marker violated its canonical schema.");
    }

    internal static async ValueTask<ManagedSetupTransactionDocument?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        if (!stream.CanRead || !stream.CanSeek || stream.Length is < 1 or > MaximumDocumentBytes)
        {
            return null;
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.Position = 0;
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return stream.Position == stream.Length ? Parse(bytes) : null;
    }
}
