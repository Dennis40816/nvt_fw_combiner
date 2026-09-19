using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Configuration;

/// <summary>Strict finite configuration JSON over stable bounded reads and atomic local-file writes.</summary>
internal sealed class ToolchainRuntimeConfigurationStorage(ILocalFileStore files, string path)
    : IToolchainRuntimeConfigurationStorage
{
    private const int MaximumBytes = 64 * 1024;
    private const int MaximumDepth = 8;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private readonly ILocalFileStore _files = files ?? throw new ArgumentNullException(nameof(files));
    private readonly string _path = Path.GetFullPath(path);

    public async ValueTask<ToolchainRuntimeConfigurationDocument?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await _files.ReadAsync(_path, MaximumBytes, async (stream, token) =>
            {
                if (stream.Length > MaximumBytes)
                {
                    throw new LocalFileTooLargeException("Configuration exceeds its byte limit.");
                }

                byte[] snapshot = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(snapshot, token).ConfigureAwait(false);
                return snapshot;
            }, cancellationToken).ConfigureAwait(false);
            return Decode(bytes);
        }
        catch (LocalFileNotFoundException)
        {
            return null;
        }
    }

    public async ValueTask WriteAsync(ToolchainRuntimeConfigurationDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        byte[] bytes;
        try
        {
            foreach (string? value in new[] { document.Source, document.Path, document.Sha256 })
            {
                if (value is not null)
                {
                    _ = StrictUtf8.GetByteCount(value);
                }
            }

            bytes = JsonSerializer.SerializeToUtf8Bytes(document,
                ToolchainRuntimeConfigurationJsonContext.Default.ToolchainRuntimeConfigurationDocument);
            _ = Decode(bytes);
        }
        catch (Exception exception) when (exception is JsonException or EncoderFallbackException)
        {
            throw new ToolchainRuntimeConfigurationFormatException(exception);
        }

        await _files.WriteAsync(_path, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static ToolchainRuntimeConfigurationDocument Decode(byte[] bytes)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(bytes);
            using JsonDocument json = StrictJsonDocumentReader.ParseOwnedSnapshot(bytes, MaximumBytes, MaximumDepth);
            ToolchainRuntimeConfigurationDocument document = json.RootElement.Deserialize(
                ToolchainRuntimeConfigurationJsonContext.Default.ToolchainRuntimeConfigurationDocument)
                ?? throw new ToolchainRuntimeConfigurationFormatException();
            return document;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            throw new ToolchainRuntimeConfigurationFormatException(exception);
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ToolchainRuntimeConfigurationDocument))]
internal sealed partial class ToolchainRuntimeConfigurationJsonContext : JsonSerializerContext;
