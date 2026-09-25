using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests that reusing validated schema content never changes a validation verdict.</summary>
public sealed partial class ProfileBundleSchemaValidatorTests
{
    /// <summary>Verifies schemas sharing one id but differing in content are validated independently.</summary>
    [Fact]
    public void ValidateEntriesKeepsSameIdSchemasWithDifferentContentIndependent()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            ProfileBundleSchemaValidator.ValidateEntries(
                Capture(Schema("integer"), /*lang=json,strict*/ "{\"value\":1}"),
                32);
            ProfileBundleSchemaValidator.ValidateEntries(
                Capture(Schema("string"), /*lang=json,strict*/ "{\"value\":\"text\"}"),
                32);

            _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
                Capture(Schema("integer"), /*lang=json,strict*/ "{\"value\":\"text\"}"),
                32));
            _ = Assert.Throws<InvalidDataException>(() => ProfileBundleSchemaValidator.ValidateEntries(
                Capture(Schema("string"), /*lang=json,strict*/ "{\"value\":1}"),
                32));
        }
    }

    /// <summary>Verifies an invalid schema is rejected on every attempt, not only the first.</summary>
    [Fact]
    public void ValidateEntriesRejectsAnInvalidSchemaOnEveryAttempt()
    {
        string schema = Schema("integer").Replace(
            "\"type\": \"object\",",
            "\"type\": \"object\", \"$id\": \"https://example.invalid/nested\",",
            StringComparison.Ordinal);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                ProfileBundleSchemaValidator.ValidateEntries(
                    Capture(schema, /*lang=json,strict*/ "{\"value\":1}"),
                    32));

            Assert.Contains("nested $id", exception.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies a previously accepted schema still honors a smaller JSON depth limit.</summary>
    [Fact]
    public void ValidateEntriesEnforcesTheDepthLimitForPreviouslyAcceptedSchemaContent()
    {
        ProfileBundleSchemaValidator.ValidateEntries(
            Capture(Schema("integer"), /*lang=json,strict*/ "{\"value\":1}"),
            32);

        _ = Assert.ThrowsAny<JsonException>(() => ProfileBundleSchemaValidator.ValidateEntries(
            Capture(Schema("integer"), /*lang=json,strict*/ "{\"value\":1}"),
            3));
    }
}
