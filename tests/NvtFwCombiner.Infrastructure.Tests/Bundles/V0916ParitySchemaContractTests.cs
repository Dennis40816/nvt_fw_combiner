using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Json.Schema;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Runs the parity contracts through the repository's real Draft 2020-12 engine.</summary>
public sealed class V0916ParitySchemaContractTests
{
    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    /// <summary>Every parity schema that must satisfy the Draft 2020-12 meta-schema.</summary>
    public static TheoryData<string> Schemas =>
    [
        "v0916-baseline-executor-v1.schema.json",
        "v0916-baseline-executor-v2.schema.json",
        "v0916-candidate-source-executor-v1.schema.json",
        "v0916-nt51951-c2-diagnostic-v1.schema.json",
        "v0916-parity-1x-amendment-v1.schema.json",
        "v0916-parity-build-report-v1.schema.json",
        "v0916-parity-certification-v1.schema.json",
        "v0916-parity-comparison-v1.schema.json",
        "v0916-parity-evidence-v1.schema.json",
        "v0916-parity-finalize-v1.schema.json",
        "v0916-parity-owner-attestation-v1.schema.json",
        "v0916-parity-receipt-v1.schema.json",
        "v0916-parity-run-v1.schema.json",
        "v0916-parity-workflow-v1.schema.json",
    ];

    /// <summary>Committed contract instances and their exact schemas.</summary>
    public static TheoryData<string, string> Instances()
    {
        TheoryData<string, string> data = [];
        data.Add("v0916-baseline-executor-v1.schema.json", "v0916-baseline-executor-v1.json");
        data.Add("v0916-baseline-executor-v2.schema.json", "v0916-baseline-executor-v2.json");
        data.Add("v0916-candidate-source-executor-v1.schema.json", "v100-candidate-source-executor-v1.json");
        data.Add("v0916-nt51951-c2-diagnostic-v1.schema.json", "v0916-nt51951-c2-diagnostic-v1.json");
        data.Add("v0916-parity-1x-amendment-v1.schema.json", "v0916-parity-1x-amendment-v1.json");
        data.Add("v0916-parity-certification-v1.schema.json", "v0916-parity-certification-v1.json");
        data.Add("v0916-parity-workflow-v1.schema.json", "v0916-parity-workflow-v1.json");
        return data;
    }

    /// <summary>Rejects a parity schema that is not a valid Draft 2020-12 schema.</summary>
    [Theory]
    [MemberData(nameof(Schemas))]
    public void SchemaSatisfiesDraft202012MetaSchema(string schemaName)
    {
        _ = LoadSchema(schemaName);
    }

    /// <summary>Rejects a committed parity document that drifts from its closed schema.</summary>
    [Theory]
    [MemberData(nameof(Instances))]
    public void RepositoryInstanceSatisfiesItsClosedSchema(string schemaName, string instanceName)
    {
        JsonSchema schema = LoadSchema(schemaName);
        using var instance = JsonDocument.Parse(File.ReadAllText(ContractPath(instanceName)));

        Assert.True(
            schema.Evaluate(instance.RootElement, EvaluationOptions).IsValid,
            $"{instanceName} must satisfy {schemaName}.");
    }

    /// <summary>Executor v2 closes every object and the amendment binds its exact raw bytes.</summary>
    [Fact]
    public void V2ClosedObjectsAndRawAmendmentBindingAreEnforced()
    {
        JsonSchema schema = LoadSchema("v0916-baseline-executor-v2.schema.json");
        byte[] raw = File.ReadAllBytes(ContractPath("v0916-baseline-executor-v2.json"));
        JsonNode document = JsonNode.Parse(raw)!;
        foreach (JsonObject member in ObjectMembers(document))
        {
            member["unexpected"] = true;
            using var altered = JsonDocument.Parse(document.ToJsonString());
            Assert.False(schema.Evaluate(altered.RootElement, EvaluationOptions).IsValid);
            _ = member.Remove("unexpected");
        }

        JsonNode amendment = JsonNode.Parse(File.ReadAllText(ContractPath("v0916-parity-1x-amendment-v1.json")))!;
        JsonNode binding = amendment["baselineExecutor"]!["contract"]!;
        Assert.Equal("docs/contracts/v0916-baseline-executor-v2.json", binding["path"]!.GetValue<string>());
        Assert.Equal(raw.Length, binding["size"]!.GetValue<int>());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(raw)), binding["sha256"]!.GetValue<string>());
        JsonNode comparison = JsonNode.Parse(File.ReadAllText(ContractPath("predecessor-comparison-v1.json")))!;
        Assert.True(JsonNode.DeepEquals(document["compilerHost"], comparison["executor"]!["compilerHost"]));
    }

    /// <summary>Activation cannot omit compiler settings or bind a null baseline contract.</summary>
    [Fact]
    public void ExecutorActivationRequiresCompleteSettingsAndContract()
    {
        foreach (string name in new[] { "v0916-baseline-executor-v2", "predecessor-comparison-v1" })
        {
            JsonSchema schema = LoadSchema(name + ".schema.json");
            JsonNode document = JsonNode.Parse(File.ReadAllText(ContractPath(name + ".json")))!;
            JsonObject host = (name == "predecessor-comparison-v1" ? document["executor"]!["compilerHost"] : document["compilerHost"])!.AsObject();
            foreach (string key in host.Select(member => member.Key).Where(key => key != "status").ToArray())
            {
                JsonNode? setting = host[key];
                _ = host.Remove(key);
                using var altered = JsonDocument.Parse(document.ToJsonString());
                Assert.False(schema.Evaluate(altered.RootElement, EvaluationOptions).IsValid);
                host[key] = setting;
            }
        }

        JsonNode amendment = JsonNode.Parse(File.ReadAllText(ContractPath("v0916-parity-1x-amendment-v1.json")))!;
        amendment["baselineExecutor"]!["contract"] = null;
        using var invalid = JsonDocument.Parse(amendment.ToJsonString());
        Assert.False(LoadSchema("v0916-parity-1x-amendment-v1.schema.json").Evaluate(invalid.RootElement, EvaluationOptions).IsValid);
    }

    private static IEnumerable<JsonObject> ObjectMembers(JsonNode node)
    {
        if (node is JsonObject member)
        {
            yield return member;
            foreach (JsonNode? child in member.Select(property => property.Value).ToArray())
            {
                if (child is not null)
                {
                    foreach (JsonObject descendant in ObjectMembers(child))
                    {
                        yield return descendant;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    foreach (JsonObject descendant in ObjectMembers(child))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }

    private static JsonSchema LoadSchema(string schemaName)
    {
        string path = ContractPath(schemaName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(
            MetaSchemas.Draft202012.Evaluate(document.RootElement, EvaluationOptions).IsValid,
            $"{schemaName} must satisfy the Draft 2020-12 meta-schema.");
        return JsonSchema.FromText(document.RootElement.GetRawText(), new BuildOptions
        {
            SchemaRegistry = new SchemaRegistry(),
        });
    }

    private static string ContractPath(string fileName)
    {
        return RepositoryPaths.FromRepositoryRoot("docs", "contracts", fileName);
    }
}
