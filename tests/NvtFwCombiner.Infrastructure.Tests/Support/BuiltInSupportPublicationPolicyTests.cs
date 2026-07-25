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
        string projectPath = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Infrastructure",
            "NvtFwCombiner.Infrastructure.csproj");
        XDocument project = XDocument.Load(projectPath);
        Assert.NotEmpty(BuiltInSupportPublicationPolicy.HistoryFiles);
        foreach (PinnedSupportPublicationPolicyFile source in
                 BuiltInSupportPublicationPolicy.HistoryFiles)
        {
            string deployedPath = Path.Combine(
                AppContext.BaseDirectory,
                source.RelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            XElement content = Assert.Single(
                project.Descendants(),
                element =>
                    element.Name.LocalName == "Content" &&
                    NormalizePath((string?)element.Attribute("Include")) ==
                        source.RelativePath);

            Assert.True(File.Exists(deployedPath), deployedPath);
            Assert.Equal(
                source.RelativePath,
                NormalizePath((string?)content.Attribute("Link")));
            Assert.Equal(
                "PreserveNewest",
                (string?)content.Attribute("CopyToOutputDirectory"));
            Assert.Equal(
                "PreserveNewest",
                (string?)content.Attribute("CopyToPublishDirectory"));
        }
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

    /// <summary>Policy and decision supersession load only through an ordered hash-pinned history.</summary>
    [Fact]
    public void RetainsValidSupersessionMetadata()
    {
        byte[] priorBytes = Encoding.UTF8.GetBytes(
            CreatePolicyObject(
                policyVersion: "1.0.0",
                decisionId: "prior-decision").ToJsonString());
        string priorSha256 =
            PinnedJsonCatalogLoader.ComputeCanonicalSha256(priorBytes);
        byte[] currentBytes = Encoding.UTF8.GetBytes(
            CreatePolicyObject(
                supersedesPolicyVersion: "1.0.0",
                supersedesPolicySha256: priorSha256,
                supersedesDecisionIds: ["prior-decision"]).ToJsonString());
        string currentSha256 =
            PinnedJsonCatalogLoader.ComputeCanonicalSha256(currentBytes);
        using TempWorkspace workspace =
            TempWorkspace.Create("nfc-support-policy-history");
        _ = workspace.Write("prior.json", priorBytes);
        _ = workspace.Write("current.json", currentBytes);

        SupportPublicationPolicySnapshot policy =
            BuiltInSupportPublicationPolicy.LoadFromDirectory(
                workspace.Root,
            [
                new PinnedSupportPublicationPolicyFile(
                    "prior.json",
                    priorSha256),
                new PinnedSupportPublicationPolicyFile(
                    "current.json",
                    currentSha256),
            ]);

        Assert.Equal("1.0.0", policy.SupersedesPolicyVersion);
        Assert.Equal(priorSha256, policy.SupersedesPolicySha256);
        Assert.Equal(
            ["prior-decision"],
            Assert.Single(policy.Decisions).SupersedesDecisionIds);
    }

    /// <summary>A predecessor with matching labels but different bytes cannot prove lineage.</summary>
    [Fact]
    public void RejectsSupersessionWhenPriorHashDoesNotMatchCurrentDeclaration()
    {
        byte[] priorBytes = Encoding.UTF8.GetBytes(
            CreatePolicyObject(
                policyVersion: "1.0.0",
                decisionId: "prior-decision").ToJsonString());
        string priorSha256 =
            PinnedJsonCatalogLoader.ComputeCanonicalSha256(priorBytes);
        byte[] currentBytes = Encoding.UTF8.GetBytes(
            CreatePolicyObject(
                supersedesPolicyVersion: "1.0.0",
                supersedesPolicySha256: new string('f', 64),
                supersedesDecisionIds: ["prior-decision"]).ToJsonString());
        using TempWorkspace workspace =
            TempWorkspace.Create("nfc-support-policy-history-mismatch");
        _ = workspace.Write("prior.json", priorBytes);
        _ = workspace.Write("current.json", currentBytes);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInSupportPublicationPolicy.LoadFromDirectory(
                workspace.Root,
            [
                new PinnedSupportPublicationPolicyFile(
                    "prior.json",
                    priorSha256),
                new PinnedSupportPublicationPolicyFile(
                    "current.json",
                    PinnedJsonCatalogLoader.ComputeCanonicalSha256(currentBytes)),
            ]));

        Assert.Contains(
            "snapshot invariants",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>History files cannot escape the configured deployment root.</summary>
    [Fact]
    public void RejectsHistoryPathOutsideDeploymentRoot()
    {
        using TempWorkspace workspace =
            TempWorkspace.Create("nfc-support-policy-history-path");

        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInSupportPublicationPolicy.LoadFromDirectory(
                workspace.Root,
                [new PinnedSupportPublicationPolicyFile(
                    "../outside.json",
                    new string('a', 64))]));
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
        string policyVersion = "2.0.0",
        string decisionId = "current-decision",
        string? supersedesPolicyVersion = null,
        string? supersedesPolicySha256 = null,
        string[]? supersedesDecisionIds = null)
    {
        var decision = new JsonObject
        {
            ["decisionId"] = decisionId,
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
        if (supersedesDecisionIds is { Length: > 0 })
        {
            decision["supersedesDecisionIds"] =
                new JsonArray(supersedesDecisionIds.Select(
                    static value => JsonValue.Create(value)).ToArray());
        }

        var policy = new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["policyId"] = "support-publication-policy",
            ["policyVersion"] = policyVersion,
            ["issuedOn"] = "2026-07-25",
            ["decisions"] = new JsonArray(decision),
        };
        if (supersedesPolicyVersion is not null)
        {
            policy["supersedesPolicyVersion"] = supersedesPolicyVersion;
        }
        if (supersedesPolicySha256 is not null)
        {
            policy["supersedesPolicySha256"] = supersedesPolicySha256;
        }

        return policy;
    }
}
