using System.Security.Cryptography;
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

    /// <summary>Verifies the current owner-approved policy satisfies its closed contract.</summary>
    [Fact]
    public void CurrentPolicySatisfiesClosedPublicationPolicyContract()
    {
        JsonObject policy = ReadPolicy();

        EvaluationResults result = EvaluatePolicy(policy);

        Assert.True(result.IsValid);
        ValidateSourceSemantics(policy);
        Assert.Equal(
            [
                (
                    "route-7-nt51919-13-general-merge-14-" +
                        "not-applicable-7-generic",
                    "test-only"),
                (
                    "route-7-nt51919-15-general-replace-14-" +
                        "not-applicable-7-generic",
                    "test-only"),
                (
                    "route-7-nt51950-8-ab-merge-4-1-ic-21-" +
                        "nt51950-ab-merge-512k-integrity-" +
                        "3f41ce1d441da78f311ca9f7b0b250716de0cdf6c8d49ed764521de07fa39c87",
                    "candidate"),
                (
                    "route-7-nt51950-8-ab-merge-9-2-plus-ic-22-" +
                        "nt51950-ab-merge-1024k-integrity-" +
                        "3f41ce1d441da78f311ca9f7b0b250716de0cdf6c8d49ed764521de07fa39c87",
                    "candidate"),
                (
                    "route-7-nt51951-8-ab-merge-13-selector-free-22-" +
                        "nt51951-ab-merge-1024k-integrity-" +
                        "1a34ef8bf35f8205d3326f556d8d6108cbe0bbb5ae0e59982ae801048904ae7a",
                    "candidate"),
            ],
            Decisions(policy));
    }

    /// <summary>The current policy names the exact preserved predecessor and replaced decisions.</summary>
    [Fact]
    public void CurrentPolicyDeclaresVerifiableVersionedLineage()
    {
        JsonObject prior = ReadPolicy("support-publication-policy-v1.0.0.json");
        JsonObject current = ReadPolicy();

        Assert.True(EvaluatePolicy(prior).IsValid);
        Assert.True(EvaluatePolicy(current).IsValid);
        ValidateSourceSemantics(prior);
        ValidateSourceSemantics(current);
        Assert.Equal("1.0.0", prior["policyVersion"]!.GetValue<string>());
        Assert.Equal("1.1.0", current["policyVersion"]!.GetValue<string>());
        Assert.Equal(
            prior["policyVersion"]!.GetValue<string>(),
            current["supersedesPolicyVersion"]!.GetValue<string>());
        string priorPath = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "support-publication-policy-v1.0.0.json");
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(priorPath)))
                .ToLowerInvariant(),
            current["supersedesPolicySha256"]!.GetValue<string>());
        Assert.Equal(
            [
                "nt51950-ab-merge-1-ic-candidate",
                "nt51950-ab-merge-2-plus-ic-candidate",
                "nt51951-ab-merge-candidate",
            ],
            Assert.IsType<JsonArray>(current["decisions"])
                .Select(Assert.IsType<JsonObject>)
                .SelectMany(static decision =>
                    decision["supersedesDecisionIds"] is JsonArray ids
                        ? ids.Select(static id => id!.GetValue<string>())
                        : [])
                .OrderBy(static id => id, StringComparer.Ordinal));
    }

    /// <summary>Verifies incomplete, blank, unknown, and invalid decision fields are rejected.</summary>
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

    /// <summary>Policy lineage requires both an exact predecessor version and SHA-256.</summary>
    [Theory]
    [InlineData("version-only")]
    [InlineData("hash-only")]
    public void PolicySchemaRejectsPartialSupersessionIdentity(string mutation)
    {
        JsonObject policy = ReadPolicy();
        if (mutation == "version-only")
        {
            Assert.True(policy.Remove("supersedesPolicySha256"));
        }
        else
        {
            Assert.True(policy.Remove("supersedesPolicyVersion"));
        }

        Assert.False(EvaluatePolicy(policy).IsValid);
    }

    /// <summary>Verifies source semantics reject duplicate decision and route identities.</summary>
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
                duplicate["routeId"] = "other-route";
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

    private static JsonObject ReadPolicy(
        string fileName = "support-publication-policy-v1.json")
    {
        return Assert.IsType<JsonObject>(JsonNode.Parse(
            File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
                "docs",
                "contracts",
                fileName))));
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
        foreach (JsonNode? node in Assert.IsType<JsonArray>(policy["decisions"]))
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

    private static IReadOnlyList<(string RouteId, string Status)> Decisions(JsonObject policy)
    {
        return
        [
            .. Assert.IsType<JsonArray>(policy["decisions"])
                .Select(Assert.IsType<JsonObject>)
                .Select(static decision => (
                    RequiredNonWhitespaceText(decision, "routeId"),
                    RequiredNonWhitespaceText(decision, "status")))
                .OrderBy(static decision => decision.Item1, StringComparer.Ordinal),
        ];
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
