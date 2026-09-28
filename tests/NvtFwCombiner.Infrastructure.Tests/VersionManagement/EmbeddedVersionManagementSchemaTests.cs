using System.Reflection;
using System.Text.Json;
using Json.Schema;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Tests that embedded VersionManagement schemas build only as fully resolved Draft 2020-12 schemas.</summary>
public sealed class EmbeddedVersionManagementSchemaTests
{
    private const string SchemaId =
        "https://schemas.example.invalid/nvt_fw_combiner/synthetic-v1.schema.json";

    private const string Draft202012 = "https://json-schema.org/draft/2020-12/schema";

    private const string LocalReference = /*lang=json,strict*/ """{ "$ref": "#/$defs/value" }""";

    /// <summary>Every shipped embedded schema satisfies the rule and builds a working validator.</summary>
    [Fact]
    public void EveryShippedSchemaLoadsAsFullyResolvedDraft202012Resource()
    {
        Assembly assembly = typeof(ReleaseManifestSchema).Assembly;
        string[] resources =
        [
            .. assembly.GetManifestResourceNames()
                .Where(static name => name.EndsWith(".schema.json", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
        using var emptyObject = JsonDocument.Parse("{}");

        Assert.Equal(
            [
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.launcher-bootstrap-v1.schema.json",
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.managed-setup-payload-admission-v1.schema.json",
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.managed-setup-transaction-v1.schema.json",
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.release-manifest-v1.schema.json",
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.update-catalog-v1.schema.json",
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.update-catalog-v2.schema.json",
                "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.update-source-registry-v1.schema.json",
            ],
            resources);
        foreach (string resource in resources)
        {
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            using var document = JsonDocument.Parse(stream);
            string id = document.RootElement.GetProperty("$id").GetString()!;

            JsonSchema schema = EmbeddedVersionManagementSchema.Load(
                typeof(ReleaseManifestSchema),
                resource,
                id,
                "Embedded schema is missing.");

            Assert.False(EmbeddedVersionManagementSchema.IsValid(schema, emptyObject.RootElement), resource);
        }
    }

    /// <summary>Local fragment references build and evaluate as one fully resolved resource.</summary>
    [Fact]
    public void BuildAcceptsLocalFragmentReferences()
    {
        using var document = JsonDocument.Parse(Schema(Draft202012, LocalReference));
        using var valid = JsonDocument.Parse("""{ "value": 1 }""");
        using var invalid = JsonDocument.Parse("""{ "value": "wrong" }""");

        JsonSchema schema = EmbeddedVersionManagementSchema.Build(document.RootElement, SchemaId);

        Assert.True(EmbeddedVersionManagementSchema.IsValid(schema, valid.RootElement));
        Assert.False(EmbeddedVersionManagementSchema.IsValid(schema, invalid.RootElement));
    }

    /// <summary>A schema whose identity differs from its declared authority is rejected.</summary>
    [Fact]
    public void BuildRejectsAnotherSchemaIdentity()
    {
        using var document = JsonDocument.Parse(Schema(Draft202012, LocalReference));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EmbeddedVersionManagementSchema.Build(
                document.RootElement,
                "https://schemas.example.invalid/nvt_fw_combiner/other-v1.schema.json"));

        Assert.Equal("Canonical embedded schema identity is invalid.", exception.Message);
    }

    /// <summary>A reference that can resolve outside the schema's own resource is rejected before any build.</summary>
    [Theory]
    [InlineData("$ref", "https://example.invalid/remote.schema.json")]
    [InlineData("$ref", "remote.schema.json#/$defs/value")]
    [InlineData("$dynamicRef", "https://example.invalid/remote.schema.json#anchor")]
    [InlineData("$recursiveRef", "https://example.invalid/remote.schema.json")]
    public void BuildRejectsNonLocalReferences(string keyword, string reference)
    {
        using var document = JsonDocument.Parse(
            Schema(Draft202012, $$"""{ "{{keyword}}": "{{reference}}" }"""));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EmbeddedVersionManagementSchema.Build(document.RootElement, SchemaId));

        Assert.Equal("Canonical embedded schema must use only local fragment references.", exception.Message);
    }

    /// <summary>A subschema cannot open a nested resource with its own identifier or dialect.</summary>
    [Theory]
    [InlineData("$id", "https://example.invalid/nested.schema.json")]
    [InlineData("$schema", Draft202012)]
    public void BuildRejectsNestedResourceDeclarations(string keyword, string value)
    {
        using var document = JsonDocument.Parse(
            Schema(Draft202012, $$"""{ "{{keyword}}": "{{value}}" }"""));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EmbeddedVersionManagementSchema.Build(document.RootElement, SchemaId));

        Assert.Equal("Canonical embedded schema cannot declare nested $id or $schema.", exception.Message);
    }

    /// <summary>Only the known Draft 2020-12 dialect is admitted, so a build never resolves a meta-schema.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("https://json-schema.org/draft/2019-09/schema")]
    [InlineData("https://example.invalid/custom-dialect.schema.json")]
    public void BuildRejectsMissingOrUnknownDialect(string? dialect)
    {
        using var document = JsonDocument.Parse(Schema(dialect, LocalReference));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EmbeddedVersionManagementSchema.Build(document.RootElement, SchemaId));

        Assert.Equal("Canonical embedded schema must declare the Draft 2020-12 dialect.", exception.Message);
    }

    private static string Schema(string? dialect, string subschema)
    {
        string dialectMember = dialect is null ? string.Empty : $"\"$schema\": \"{dialect}\",";
        return $$"""
            {
              {{dialectMember}}
              "$id": "{{SchemaId}}",
              "allOf": [ {{subschema}} ],
              "$defs": {
                "value": {
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["value"],
                  "properties": { "value": { "type": "integer" } }
                }
              }
            }
            """;
    }
}
