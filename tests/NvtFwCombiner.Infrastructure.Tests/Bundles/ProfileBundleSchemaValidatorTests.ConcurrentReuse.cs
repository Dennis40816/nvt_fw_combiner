using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests that shared, already-built schemas give identical verdicts under concurrent use.</summary>
public sealed partial class ProfileBundleSchemaValidatorTests
{
    private const int ConcurrentWorkers = 8;
    private const int ConcurrentRounds = 6;

    /// <summary>Verifies concurrent evaluation of shared built schemas matches sequential verdicts.</summary>
    [Fact]
    public void SharedSchemasGiveSequentialVerdictsUnderConcurrentEvaluation()
    {
        JsonSchema profileSchema = ParseContractSchema("composition-profile-v2.17.schema.json");
        JsonSchema familySchema = ParseContractSchema("firmware-family-v1.schema.json");
        List<(JsonSchema Schema, JsonElement Document)> cases =
        [
            .. BuiltInDocuments("profiles").SelectMany(document => ValidAndInvalid(profileSchema, document)),
            .. BuiltInDocuments("families").SelectMany(document => ValidAndInvalid(familySchema, document)),
        ];
        bool[] expected = [.. cases.Select(item => ProfileBundleSchemaValidator.IsInstanceValid(item.Schema, item.Document))];
        Assert.Contains(true, expected);
        Assert.Contains(false, expected);

        var mismatches = new ConcurrentBag<int>();
        _ = Parallel.For(
            0,
            cases.Count * ConcurrentRounds,
            new ParallelOptions { MaxDegreeOfParallelism = ConcurrentWorkers },
            iteration =>
            {
                int index = (int)(iteration * 7919L % cases.Count);
                if (ProfileBundleSchemaValidator.IsInstanceValid(cases[index].Schema, cases[index].Document) !=
                    expected[index])
                {
                    mismatches.Add(index);
                }
            });

        Assert.Empty(mismatches);
    }

    /// <summary>Verifies concurrent meta-validation and build of one schema text yield equivalent schemas.</summary>
    [Fact]
    public void ConcurrentSchemaBuildsOfOneTextGiveEquivalentVerdicts()
    {
        string path = RepositoryPaths.FromRepositoryRoot("docs", "contracts", "composition-profile-v2.17.schema.json");
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(path));
        string schemaId = source.RootElement.GetProperty("$id").GetString()!;
        JsonElement root = source.RootElement.Clone();
        List<JsonElement> documents = [.. BuiltInDocuments("profiles").Take(6)];

        var built = new ConcurrentBag<JsonSchema>();
        _ = Parallel.For(
            0,
            ConcurrentWorkers * 2,
            new ParallelOptions { MaxDegreeOfParallelism = ConcurrentWorkers },
            _ => built.Add(ProfileBundleSchemaValidator.ParseSchema(path, schemaId, root)));

        bool[] expected = [.. documents.Select(document =>
            ProfileBundleSchemaValidator.IsInstanceValid(built.First(), document))];
        Assert.All(built, schema => Assert.Equal(
            expected,
            documents.Select(document => ProfileBundleSchemaValidator.IsInstanceValid(schema, document))));
    }

    private static JsonSchema ParseContractSchema(string fileName)
    {
        string path = RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return ProfileBundleSchemaValidator.ParseSchema(
            path,
            document.RootElement.GetProperty("$id").GetString()!,
            document.RootElement);
    }

    private static IEnumerable<JsonElement> BuiltInDocuments(string folder)
    {
        string root = RepositoryPaths.FromRepositoryRoot("profiles", "built-in");
        foreach (string file in Directory.EnumerateDirectories(root)
                     .Select(bundle => Path.Combine(bundle, folder))
                     .Where(Directory.Exists)
                     .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json"))
                     .Order(StringComparer.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            yield return document.RootElement.Clone();
        }
    }

    private static IEnumerable<(JsonSchema Schema, JsonElement Document)> ValidAndInvalid(
        JsonSchema schema,
        JsonElement document)
    {
        yield return (schema, document);
        JsonObject mutated = Assert.IsType<JsonObject>(JsonNode.Parse(document.GetRawText()));
        mutated["unexpectedConcurrencyProbe"] = true;
        using JsonDocument invalid = JsonDocument.Parse(mutated.ToJsonString());
        yield return (schema, invalid.RootElement.Clone());
    }
}
