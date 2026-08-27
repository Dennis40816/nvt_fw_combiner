using System.Text.Json;
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
        "v0916-candidate-source-executor-v1.schema.json",
        "v0916-nt51951-c2-diagnostic-v1.schema.json",
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
        data.Add("v0916-candidate-source-executor-v1.schema.json", "v100-candidate-source-executor-v1.json");
        data.Add("v0916-nt51951-c2-diagnostic-v1.schema.json", "v0916-nt51951-c2-diagnostic-v1.json");
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
