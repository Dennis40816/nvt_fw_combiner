using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Configuration;

/// <summary>Strict finite JSON transport over the existing bounded/atomic local-file adapter.</summary>
internal sealed class EventBufferFormatConfigurationStorage(ILocalFileStore files, string path)
    : IEventBufferFormatConfigurationStorage
{
    private const int MaximumBytes = 64 * 1024;
    private const int MaximumDepth = 8;
    private readonly ILocalFileStore _files = files ?? throw new ArgumentNullException(nameof(files));
    private readonly string _path = Path.GetFullPath(path);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Lazy<JsonSchema> Schema = new(() => ProfileBundleSchemaValidator.LoadEmbeddedSchema(
        typeof(EventBufferFormatConfigurationStorage),
        "NvtFwCombiner.Infrastructure.Contracts.Schemas.event-buffer-format-configuration-v1.schema.json",
        "https://example.invalid/nfc/schemas/event-buffer-format-configuration-v1.schema.json",
        "Event Buffer Format configuration schema is missing."));

    public async ValueTask<EventBufferFormatStoredConfiguration?> ReadAsync(CancellationToken cancellationToken)
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
            return new(Decode(bytes), Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch (LocalFileNotFoundException)
        {
            return null;
        }
    }

    public async ValueTask<string> WriteAsync(
        EventBufferFormatConfigurationDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        byte[] bytes;
        try
        {
            ValidateDocumentText(document);
            bytes = JsonSerializer.SerializeToUtf8Bytes(document,
                EventBufferFormatConfigurationJsonContext.Default.EventBufferFormatConfigurationDocument);
            _ = Decode(bytes);
        }
        catch (Exception exception) when (exception is JsonException or EncoderFallbackException)
        {
            throw new EventBufferFormatConfigurationFormatException(exception);
        }

        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        await _files.WriteAsync(_path, bytes, cancellationToken).ConfigureAwait(false);
        // The adapter has committed. A newly cancelled token must not turn success into a failure.
        return hash;
    }

    private static EventBufferFormatConfigurationDocument Decode(byte[] bytes)
    {
        try
        {
            // JsonDocument can defer decoding string payloads; reject malformed UTF-8 anywhere.
            ValidateEncodedText(bytes);
            using JsonDocument json = StrictJsonDocumentReader.ParseOwnedSnapshot(bytes, MaximumBytes, MaximumDepth);
            return ProfileBundleSchemaValidator.IsInstanceValid(Schema.Value, json.RootElement)
                ? json.RootElement.Deserialize(
                    EventBufferFormatConfigurationJsonContext.Default.EventBufferFormatConfigurationDocument)
                    ?? throw new EventBufferFormatConfigurationFormatException()
                : throw new EventBufferFormatConfigurationFormatException();
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new EventBufferFormatConfigurationFormatException(exception);
        }
    }

    private static void ValidateEncodedText(byte[] bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new EventBufferFormatConfigurationFormatException();
        }

        _ = StrictUtf8.GetCharCount(bytes);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = MaximumDepth });
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
            {
                try
                {
                    // Escaped unpaired UTF-16 can be valid UTF-8 but is not a decodable JSON string.
                    _ = reader.GetString();
                }
                catch (InvalidOperationException exception)
                {
                    throw new EventBufferFormatConfigurationFormatException(exception);
                }
            }
        }
    }

    private static void ValidateDocumentText(EventBufferFormatConfigurationDocument document)
    {
        ValidateString(document.ScopeId);
        foreach (EventBufferFormatConfigurationEntryDocument? entry in document.Entries ?? [])
        {
            if (entry is null)
            {
                continue;
            }

            ValidateString(entry.UniqueId);
            ValidateString(entry.AliasName);
            foreach (string? value in entry.RecognitionValues ?? [])
            {
                ValidateString(value);
            }
        }
    }

    private static void ValidateString(string? value)
    {
        if (value is not null)
        {
            // The default serializer replaces invalid raw UTF-16; never persist a different alias.
            _ = StrictUtf8.GetByteCount(value);
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(EventBufferFormatConfigurationDocument))]
internal sealed partial class EventBufferFormatConfigurationJsonContext : JsonSerializerContext;
