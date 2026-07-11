using System.Text;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests bounded strict JSON parsing before bundle schema validation.</summary>
public sealed class StrictJsonDocumentReaderTests
{
    /// <summary>Verifies one valid nested JSON value is preserved.</summary>
    [Fact]
    public void ParseReturnsValidDocument()
    {
        using JsonDocument document = Parse(/*lang=json,strict*/ """
            { "name": "bundle", "entries": [{ "id": "one" }, { "id": "two" }] }
            """);

        Assert.Equal("bundle", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("entries").GetArrayLength());
    }

    /// <summary>Verifies duplicate keys fail in root and nested objects.</summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"id\":1,\"id\":2}")]
    [InlineData(/*lang=json,strict*/ "{\"entry\":{\"id\":1,\"id\":2}}")]
    [InlineData(/*lang=json,strict*/ "[{\"id\":1,\"id\":2}]")]
    public void ParseRejectsDuplicateKeys(string json)
    {
        JsonException exception = Assert.Throws<JsonException>(() => Parse(json));
        Assert.Contains("Duplicate JSON property 'id'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies escaped and literal spellings of one property remain duplicates.</summary>
    [Fact]
    public void ParseRejectsEscapedEquivalentKeys()
    {
        _ = Assert.Throws<JsonException>(() => Parse(/*lang=json,strict*/ "{\"id\":1,\"\\u0069d\":2}"));
    }

    /// <summary>Verifies equal keys in separate objects remain valid.</summary>
    [Fact]
    public void ParseAllowsKeysRepeatedAcrossObjects()
    {
        using JsonDocument document = Parse(/*lang=json,strict*/ "[{\"id\":1},{\"id\":2}]");
        Assert.Equal(2, document.RootElement.GetArrayLength());
    }

    /// <summary>Verifies returned documents cannot observe later caller-buffer mutation.</summary>
    [Fact]
    public void ParseSnapshotsCallerMemory()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"value\":1}");
        using JsonDocument document = StrictJsonDocumentReader.Parse(bytes, bytes.Length, 8);

        bytes[^2] = (byte)'2';

        Assert.Equal(1, document.RootElement.GetProperty("value").GetInt32());
        Assert.Equal(/*lang=json,strict*/ "{\"value\":1}", document.RootElement.GetRawText());
    }

    /// <summary>Verifies byte and depth bounds fail closed.</summary>
    [Fact]
    public void ParseRejectsInputsOutsideCallerBounds()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"id\":1}");
        byte[] nested = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"entry\":{}}");
        _ = Assert.Throws<JsonException>(() => StrictJsonDocumentReader.Parse(bytes, bytes.Length - 1, 8));
        _ = Assert.ThrowsAny<JsonException>(() => StrictJsonDocumentReader.Parse(nested, nested.Length, 1));
    }

    /// <summary>Verifies BOM, comments, trailing commas, and multiple roots are rejected.</summary>
    [Fact]
    public void ParseRejectsNoncanonicalJsonFraming()
    {
        byte[] withBom = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{}")];

        _ = Assert.Throws<JsonException>(() => StrictJsonDocumentReader.Parse(withBom, 16, 8));
        _ = Assert.ThrowsAny<JsonException>(() => Parse(/*lang=json*/ "{/* comment */\"id\":1}"));
        _ = Assert.ThrowsAny<JsonException>(() => Parse(/*lang=json*/ "{\"id\":1,}"));
        _ = Assert.ThrowsAny<JsonException>(() => Parse("{}{}"));
    }

    /// <summary>Verifies parser limits themselves must be positive.</summary>
    [Fact]
    public void ParseRejectsInvalidLimits()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("{}");
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => StrictJsonDocumentReader.Parse(bytes, 0, 8));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => StrictJsonDocumentReader.Parse(bytes, 8, 0));
    }

    private static JsonDocument Parse(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return StrictJsonDocumentReader.Parse(bytes, 1024, 16);
    }
}
