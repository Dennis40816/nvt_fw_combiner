using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Infrastructure.Support;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Support;

/// <summary>Hash and strict-contract tests for the built-in publication policy.</summary>
public sealed class BuiltInSupportPublicationPolicyTests
{
    private const string ExpectedSha256 =
        "e0e9c2dec7a5a3875806d2558d775d094420b305edb7fe63a6e7001290d634ae";

    /// <summary>The shipped policy loads only through its reviewed SHA-256.</summary>
    [Fact]
    public void LoadsCheckedInPolicyThroughPinnedHash()
    {
        SupportPublicationPolicySnapshot policy =
            BuiltInSupportPublicationPolicy.Load();

        Assert.Equal("support-publication-policy", policy.PolicyId);
        Assert.Equal("1.0.0", policy.PolicyVersion);
        Assert.Equal(ExpectedSha256, policy.Sha256);
        Assert.Equal(5, policy.Decisions.Count);
        Assert.Contains(policy.Decisions, decision =>
            decision.RouteId ==
                "nt51950-ab-merge-1-ic-nt51950-ab-merge-512k" &&
            decision.Status == SupportPublicationStatus.Candidate);
    }

    /// <summary>The reviewed policy is present in build output and retained for publish.</summary>
    [Fact]
    public void PolicyIsDeployedAndDeclaredForPublish()
    {
        string relativePath = Path.Combine(
            "docs",
            "contracts",
            "support-publication-policy-v1.json");
        string deployedPath = Path.Combine(
            AppContext.BaseDirectory,
            relativePath);
        string projectPath = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Infrastructure",
            "NvtFwCombiner.Infrastructure.csproj");
        XElement content = Assert.Single(
            XDocument.Load(projectPath).Descendants(),
            element =>
                element.Name.LocalName == "Content" &&
                NormalizePath((string?)element.Attribute("Include")) ==
                    "docs/contracts/support-publication-policy-v1.json");

        Assert.True(File.Exists(deployedPath), deployedPath);
        Assert.Equal(
            "docs/contracts/support-publication-policy-v1.json",
            NormalizePath((string?)content.Attribute("Link")));
        Assert.Equal(
            "PreserveNewest",
            (string?)content.Attribute("CopyToOutputDirectory"));
        Assert.Equal(
            "PreserveNewest",
            (string?)content.Attribute("CopyToPublishDirectory"));
    }

    /// <summary>A one-byte mutation fails before status materialization.</summary>
    [Fact]
    public void RejectsChangedPolicyByte()
    {
        byte[] policy = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "support-publication-policy-v1.json"));
        policy[^2] ^= 0x01;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInSupportPublicationPolicy.Load(policy, ExpectedSha256));

        Assert.Contains("hash mismatch", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Strict generated metadata rejects unknown and case-mismatched fields.</summary>
    [Fact]
    public void RejectsUnknownAndCaseMismatchedProperties()
    {
        JsonObject unknown = CreatePolicyObject();
        unknown["unexpected"] = true;
        JsonObject wrongCase = CreatePolicyObject();
        JsonNode? schemaVersion = wrongCase["schemaVersion"];
        Assert.True(wrongCase.Remove("schemaVersion"));
        wrongCase["SchemaVersion"] = schemaVersion;

        AssertInvalidPolicy(unknown.ToJsonString(), "JSON is invalid");
        AssertInvalidPolicy(wrongCase.ToJsonString(), "JSON is invalid");
    }

    /// <summary>Policy and decision supersession metadata remains immutable.</summary>
    [Fact]
    public void RetainsValidSupersessionMetadata()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(
            CreatePolicyObject("1.0.0", "prior-decision").ToJsonString());

        SupportPublicationPolicySnapshot policy =
            BuiltInSupportPublicationPolicy.Load(
                bytes,
                PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes));

        Assert.Equal("1.0.0", policy.SupersedesPolicyVersion);
        Assert.Equal(
            ["prior-decision"],
            Assert.Single(policy.Decisions).SupersedesDecisionIds);
    }

    /// <summary>Semantic, provenance, and supersession violations fail closed.</summary>
    [Theory]
    [InlineData("version")]
    [InlineData("self-version")]
    [InlineData("date")]
    [InlineData("authority")]
    [InlineData("self-decision")]
    [InlineData("duplicate-route")]
    public void RejectsInvalidPolicySemantics(string mutation)
    {
        JsonObject policy = CreatePolicyObject();
        JsonArray decisions = Assert.IsType<JsonArray>(policy["decisions"]);
        JsonObject decision = Assert.IsType<JsonObject>(decisions[0]);
        JsonObject provenance = Assert.IsType<JsonObject>(decision["provenance"]);
        switch (mutation)
        {
            case "version":
                policy["policyVersion"] = "not-semver";
                break;
            case "self-version":
                policy["supersedesPolicyVersion"] = "2.0.0";
                break;
            case "date":
                provenance["recordedOn"] = "2026-99-99";
                break;
            case "authority":
                provenance["authorityKind"] = "inferred";
                break;
            case "self-decision":
                decision["supersedesDecisionIds"] =
                    new JsonArray("current-decision");
                break;
            case "duplicate-route":
                JsonObject duplicate =
                    Assert.IsType<JsonObject>(decision.DeepClone());
                duplicate["decisionId"] = "duplicate-decision";
                decisions.Add(duplicate);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown mutation.");
        }

        AssertInvalidPolicy(policy.ToJsonString(), "policy");
    }

    private static void AssertInvalidPolicy(
        string policyJson,
        string expectedMessage)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(policyJson);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInSupportPublicationPolicy.Load(
                bytes,
                PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes)));

        Assert.Contains(
            expectedMessage,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = path.Replace('\\', '/');
        const string repositoryPrefix = "../../";
        return normalized.StartsWith(
            repositoryPrefix,
            StringComparison.Ordinal)
            ? normalized[repositoryPrefix.Length..]
            : normalized;
    }

    private static JsonObject CreatePolicyObject(
        string? supersedesPolicyVersion = null,
        params string[] supersedesDecisionIds)
    {
        var decision = new JsonObject
        {
            ["decisionId"] = "current-decision",
            ["routeId"] =
                "nt51950-ab-merge-1-ic-nt51950-ab-merge-512k",
            ["status"] = "candidate",
            ["provenance"] = new JsonObject
            {
                ["authorityKind"] = "owner-decision",
                ["recordedOn"] = "2026-07-25",
                ["recordRef"] = "owner-chat:test",
                ["rationale"] = "test",
            },
        };
        if (supersedesDecisionIds.Length != 0)
        {
            decision["supersedesDecisionIds"] =
                new JsonArray(supersedesDecisionIds.Select(
                    static value => JsonValue.Create(value)).ToArray());
        }

        var policy = new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["policyId"] = "support-publication-policy",
            ["policyVersion"] = "2.0.0",
            ["issuedOn"] = "2026-07-25",
            ["decisions"] = new JsonArray(decision),
        };
        if (supersedesPolicyVersion is not null)
        {
            policy["supersedesPolicyVersion"] = supersedesPolicyVersion;
        }

        return policy;
    }
}
