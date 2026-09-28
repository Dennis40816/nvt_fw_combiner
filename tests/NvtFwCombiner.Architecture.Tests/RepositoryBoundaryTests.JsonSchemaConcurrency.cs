namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private static readonly string[] JsonSchemaGlobalStateWrites =
    [
        "SchemaRegistry.Global",
        "DialectRegistry.Global",
        "VocabularyRegistry.Global",
        "FormatRegistry.Global",
        "Dialect.Default",
        "FormatKeyword.Annotate",
        "FormatKeyword.Validate",
        "Deserialize<JsonSchema>",
        "JsonSchema.FromFile(",
        "JsonSchema.FromStream(",
    ];

    private static readonly string[] ProductionSchemaBuilds =
    [
        "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleSchemaValidator.cs",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ReleaseManifestSchema.cs",
    ];

    /// <summary>
    /// Keeps JsonSchema.Net's shared state immutable so concurrent bundle preloading (ADR 0075) stays
    /// race-free: production code never touches the library's global registries, and every schema build
    /// uses its own registry. A new schema build must be reviewed against the JsonSchema.Net 8.0.5 audit.
    /// </summary>
    [Fact]
    public void ProductionCodeNeverWritesJsonSchemaGlobalState()
    {
        string source = Path.Combine(Root.FullName, "src");
        string separator = Path.DirectorySeparatorChar.ToString();
        var builds = new List<string>();
        foreach (string file in Directory
                     .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal) &&
                         !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            string text = File.ReadAllText(file);
            string relative = Path.GetRelativePath(Root.FullName, file).Replace('\\', '/');
            foreach (string write in JsonSchemaGlobalStateWrites)
            {
                Assert.False(text.Contains(write, StringComparison.Ordinal), $"{relative} uses {write}.");
            }

            for (int index = text.IndexOf("JsonSchema.FromText(", StringComparison.Ordinal);
                 index >= 0;
                 index = text.IndexOf("JsonSchema.FromText(", index + 1, StringComparison.Ordinal))
            {
                string call = text[index..Math.Min(text.Length, index + 240)];
                Assert.True(
                    call.Contains("SchemaRegistry = new SchemaRegistry()", StringComparison.Ordinal),
                    $"{relative} builds a schema without its own SchemaRegistry.");
                builds.Add(relative);
            }
        }

        Assert.Equal(ProductionSchemaBuilds, builds);
    }

    /// <summary>
    /// Keeps the other two JsonSchema.Net 8.0.5 audit conditions at every production schema build: the root
    /// declares the known Draft 2020-12 dialect and every reference stays a local fragment of one resource, so a
    /// built schema is fully resolved and never resolves or registers shared state (ADR 0075). The behavior is
    /// tested in Infrastructure.Tests for each builder.
    /// </summary>
    [Fact]
    public void EveryProductionSchemaBuildAdmitsOnlyLocalReferencesAndDraft202012()
    {
        foreach (string build in ProductionSchemaBuilds)
        {
            string source = ReadText(build);
            AssertContainsAll(
                source,
                "\"https://json-schema.org/draft/2020-12/schema\"",
                "property.Name is \"$ref\" or \"$dynamicRef\" or \"$recursiveRef\"",
                ".StartsWith('#')",
                "!isRoot && property.Name is \"$id\" or \"$schema\"");
        }
    }
}
