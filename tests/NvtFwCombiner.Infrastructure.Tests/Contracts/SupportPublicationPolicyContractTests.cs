using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Contracts;

/// <summary>Locks the reviewed source contract for publication-only route decisions.</summary>
public sealed class SupportPublicationPolicyContractTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/support-publication-policy-v1.schema.json";

    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    /// <summary>Verifies the initial owner-approved policy satisfies its closed contract.</summary>
    [Fact]
    public void InitialPolicySatisfiesClosedPublicationPolicyContract()
    {
        JsonObject policy = ReadPolicy();
        EvaluationResults result = EvaluatePolicy(policy);

        Assert.True(result.IsValid);
        ValidateSourceSemantics(policy);
    }

    /// <summary>Verifies the schema rejects incomplete, blank, unknown, and invalid decision fields.</summary>
    [Theory]
    [InlineData("route-id")]
    [InlineData("provenance")]
    [InlineData("record-ref")]
    [InlineData("rationale")]
    [InlineData("unknown-property")]
    [InlineData("invalid-status")]
    public void PolicySchemaRejectsInvalidDecisionFields(string mutation)
    {
        JsonObject policy = ReadPolicy();
        JsonObject decision = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(policy["decisions"])[0]);
        JsonObject provenance = Assert.IsType<JsonObject>(decision["provenance"]);
        _ = mutation switch
        {
            "route-id" => decision.Remove("routeId"),
            "provenance" => decision.Remove("provenance"),
            "record-ref" => provenance["recordRef"] = "   ",
            "rationale" => provenance["rationale"] = "\t",
            "unknown-property" => decision["unexpected"] = true,
            "invalid-status" => decision["status"] = "future-status",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown policy mutation."),
        };

        EvaluationResults result = EvaluatePolicy(policy);

        Assert.False(result.IsValid);
    }

    /// <summary>Verifies the source contract rejects duplicate canonical decisions before materialization exists.</summary>
    [Theory]
    [InlineData("decision-id")]
    [InlineData("route-id")]
    public void PolicySourceRejectsDuplicateDecisionIdentity(string mutation)
    {
        JsonObject policy = ReadPolicy();
        JsonArray decisions = Assert.IsType<JsonArray>(policy["decisions"]);
        JsonObject first = Assert.IsType<JsonObject>(decisions[0]);
        JsonObject duplicate = Assert.IsType<JsonObject>(first.DeepClone());
        switch (mutation)
        {
            case "decision-id":
                duplicate["routeId"] = "nt51919-general-merge-generic-other";
                break;
            case "route-id":
                duplicate["decisionId"] = "nt51919-general-merge-test-only-other";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown policy mutation.");
        }

        decisions.Add(duplicate);
        Assert.True(EvaluatePolicy(policy).IsValid);

        _ = Assert.Throws<InvalidDataException>(() => ValidateSourceSemantics(policy));
    }

    private static EvaluationResults EvaluatePolicy(JsonObject policy)
    {
        using var document = JsonDocument.Parse(policy.ToJsonString());
        return LoadSchema().Evaluate(document.RootElement, EvaluationOptions);
    }

    private static JsonObject ReadPolicy()
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(
            File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
                "docs",
                "contracts",
                "support-publication-policy-v1.json"))));
    }

    private static JsonSchema LoadSchema()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "support-publication-policy-v1.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ProfileBundleSchemaValidator.ParseSchema(path, SchemaId, document.RootElement);
    }

    private static void ValidateSourceSemantics(JsonObject policy)
    {
        var decisionIds = new HashSet<string>(StringComparer.Ordinal);
        var routeIds = new HashSet<string>(StringComparer.Ordinal);
        JsonArray decisions = Assert.IsType<JsonArray>(policy["decisions"]);
        foreach (JsonNode? node in decisions)
        {
            JsonObject decision = Assert.IsType<JsonObject>(node);
            string decisionId = RequiredNonWhitespaceText(decision, "decisionId");
            string routeId = RequiredNonWhitespaceText(decision, "routeId");
            JsonObject provenance = Assert.IsType<JsonObject>(decision["provenance"]);
            _ = RequiredNonWhitespaceText(provenance, "recordRef");
            _ = RequiredNonWhitespaceText(provenance, "rationale");

            if (!decisionIds.Add(decisionId))
            {
                throw new InvalidDataException($"Publication policy repeats decision id '{decisionId}'.");
            }

            if (!routeIds.Add(routeId))
            {
                throw new InvalidDataException($"Publication policy repeats route id '{routeId}'.");
            }
        }
    }

    private static string RequiredNonWhitespaceText(JsonObject document, string propertyName)
    {
        return document[propertyName] is not JsonValue value ||
            !value.TryGetValue(out string? text) ||
            string.IsNullOrWhiteSpace(text)
            ? throw new InvalidDataException($"Publication policy requires non-whitespace '{propertyName}'.")
            : text;
    }

}
