using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Infrastructure.Configuration;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Configuration;

/// <summary>Exercises actual schema, strict transport, filesystem persistence and source-byte provenance.</summary>
public sealed class EventBufferFormatConfigurationStorageTests
{
    private const string Valid = """
        {"schemaVersion":1,"scopeId":"test","entries":[{"uniqueId":"format-a","aliasName":"Alias","recognitionValues":["0xa6","0x97"]}]}
        """;

    /// <summary>Missing is distinct; reads hash original bytes and atomic writes round-trip.</summary>
    [Fact]
    public async Task MissingAndActualByteHashRoundTripAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.PathFor("config/event-buffer-format.v1.json");
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        Assert.Null(await storage.ReadAsync(TestContext.Current.CancellationToken));
        byte[] original = Encoding.UTF8.GetBytes(Valid + "\n  ");
        _ = workspace.Write("config/event-buffer-format.v1.json", original);
        EventBufferFormatStoredConfiguration loaded = (await storage.ReadAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(original)), loaded.SourceSha256);
        Assert.Equal("0xa6", loaded.Document.Entries[0].RecognitionValues[0]);
        string savedHash = await storage.WriteAsync(loaded.Document, TestContext.Current.CancellationToken);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), savedHash);
        Assert.NotEqual(loaded.SourceSha256, savedHash);
        Assert.Equal(savedHash, (await storage.ReadAsync(TestContext.Current.CancellationToken))!.SourceSha256);
        _ = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    /// <summary>Rejects ambiguous framing, wrong schema, duplicate keys and unknown properties.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":2,\"scopeId\":\"test\",\"entries\":[]}")]
    [InlineData("{\"SchemaVersion\":1,\"scopeId\":\"test\",\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[],\"effects\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"scopeId\":\"test\",\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":null}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[null]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[{\"uniqueId\":\"a\",\"recognitionValues\":null}]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[{\"uniqueId\":\"a\",\"recognitionValues\":[] ,\"output\":1}]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[{\"uniqueId\":\"a\",\"uniqueId\":\"b\",\"recognitionValues\":[]}]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[],}")]
    [InlineData("{/*comment*/\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"scopeId\":\"test\",\"entries\":[]} {}")]
    [InlineData("[[[[[[[[[0]]]]]]]]]")]
    public async Task InvalidDocumentsFailClosedAsync(string json)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(json));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(json, File.ReadAllText(path));
    }

    /// <summary>Both ingress and proposed saves apply the same exact lexical hex contract.</summary>
    [Theory]
    [InlineData("97")]
    [InlineData("0X97")]
    [InlineData("0x097")]
    [InlineData("0xG7")]
    [InlineData("0x 7")]
    [InlineData("0x97\n")]
    [InlineData("")]
    public async Task InvalidHexIsRejectedOnReadAndWriteAsync(string value)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        var document = new EventBufferFormatConfigurationDocument(1, "test", [new("a", null, [value])]);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.WriteAsync(document, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(Valid, File.ReadAllText(path));
        string invalid = Valid.Replace("\"0xa6\"", System.Text.Json.JsonSerializer.Serialize(value), StringComparison.Ordinal);
        _ = workspace.Write("config.json", Encoding.UTF8.GetBytes(invalid));
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Strict UTF-8 validation covers value payloads, not only object keys.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BomAndInvalidUtf8AreRejectedAsync(bool bom)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(Valid);
        if (bom)
        {
            bytes = [0xEF, 0xBB, 0xBF, .. bytes];
        }
        else
        {
            bytes[Valid.IndexOf("Alias", StringComparison.Ordinal)] = 0xFF;
        }

        string path = workspace.Write("config.json", bytes);
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Unpaired escaped surrogates are rejected as format errors before schema string evaluation.</summary>
    [Theory]
    [InlineData("\\uD800")]
    [InlineData("\\uDC00")]
    public async Task UnpairedEscapedSurrogateIsAFormatFailureAsync(string escaped)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid.Replace("Alias", escaped, StringComparison.Ordinal)));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Invalid escaped keys are rejected before duplicate-key string decoding.</summary>
    [Theory]
    [InlineData("\\uD800")]
    [InlineData("\\uDC00")]
    public async Task UnpairedEscapedKeyIsAFormatFailureAsync(string escaped)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid.Replace("aliasName", escaped, StringComparison.Ordinal)));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Saving invalid raw UTF-16 must not substitute a different persisted alias.</summary>
    [Fact]
    public async Task InvalidDraftUnicodePreservesOriginalFileAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        var document = new EventBufferFormatConfigurationDocument(1, "test", [new("format-a", "\uD800", ["0x97"])]);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.WriteAsync(document, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(Valid, File.ReadAllText(path));
    }

    /// <summary>A valid surrogate pair remains a valid Unicode alias on both read and save.</summary>
    [Fact]
    public async Task ValidUnicodeRoundTripsWithoutReplacementAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid.Replace("Alias", "\\uD83D\\uDE00", StringComparison.Ordinal)));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        EventBufferFormatStoredConfiguration stored = (await storage.ReadAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal("\U0001F600", stored.Document.Entries[0].AliasName);
        _ = await storage.WriteAsync(stored.Document, TestContext.Current.CancellationToken);
        Assert.Equal("\U0001F600", (await storage.ReadAsync(TestContext.Current.CancellationToken))!.Document.Entries[0].AliasName);
    }

    /// <summary>Documents larger than the agreed read bound never enter JSON admission.</summary>
    [Fact]
    public async Task OversizeFileIsRejectedBeforeParsingAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid.PadRight(65537)));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(65537, new FileInfo(path).Length);
    }

    /// <summary>Schema bounds apply before writing, while semantic membership stays outside this adapter.</summary>
    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    [InlineData("alias")]
    [InlineData("entries")]
    [InlineData("values")]
    public async Task SchemaBoundsRejectWithoutTouchingExistingFileAsync(string field)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        var entry = new EventBufferFormatConfigurationEntryDocument(field == "identity" ? new('x', 129) : "unknown-is-lexically-valid",
            field == "alias" ? new('x', 257) : null, [.. Enumerable.Repeat("0x00", field == "values" ? 257 : 1)]);
        var document = new EventBufferFormatConfigurationDocument(1, field == "scope" ? new('x', 129) : "test",
            [.. Enumerable.Repeat(entry, field == "entries" ? 65 : 1)]);
        _ = await Assert.ThrowsAsync<EventBufferFormatConfigurationFormatException>(() => storage.WriteAsync(document, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(Valid, File.ReadAllText(path));
    }

    /// <summary>The codec does not become a duplicate identity/membership registry.</summary>
    [Fact]
    public async Task UnknownIdentityAndDuplicateValuesRemainApplicationAdmissionAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), workspace.PathFor("config.json"));
        var document = new EventBufferFormatConfigurationDocument(1, "test", [new("unknown", null, ["0xA6", "0xa6"])]);
        _ = await storage.WriteAsync(document, TestContext.Current.CancellationToken);
        Assert.Equal(2, (await storage.ReadAsync(TestContext.Current.CancellationToken))!.Document.Entries[0].RecognitionValues.Count);
    }

    /// <summary>A real atomic replacement failure preserves the original document.</summary>
    [Fact]
    public async Task FailedAtomicWritePreservesOriginalBytesAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("config.json", Encoding.UTF8.GetBytes(Valid));
        var storage = new EventBufferFormatConfigurationStorage(new LocalFileStore(), path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.WriteAsync(new(1, "test", []), TestContext.Current.CancellationToken).AsTask());
        }

        Assert.Equal(Valid, File.ReadAllText(path));
        _ = Assert.Single(Directory.GetFiles(workspace.Root));
    }
}
