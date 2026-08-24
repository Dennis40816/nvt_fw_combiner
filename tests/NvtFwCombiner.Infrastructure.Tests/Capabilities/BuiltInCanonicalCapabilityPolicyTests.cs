using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Capabilities;

/// <summary>Tests the exact, hash-pinned canonical capability policy source.</summary>
public sealed class BuiltInCanonicalCapabilityPolicyTests
{
    /// <summary>The checked-in full policy retains the reviewed NT51929 Standard Merge decision.</summary>
    [Fact]
    public void LoadsCheckedInNt51929Policy()
    {
        CanonicalCapabilityPolicySnapshot policy =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute route = Assert.Single(
            policy.Routes,
            static candidate => StringComparer.Ordinal.Equals(
                candidate.Identity.IcId,
                "NT51929") &&
                StringComparer.Ordinal.Equals(
                candidate.Identity.WorkflowId,
                "standard-merge") &&
                StringComparer.Ordinal.Equals(
                    candidate.Identity.MapVariant,
                    "nt51929-standard-merge-256k"));

        Assert.Equal("canonical-capability-policy", policy.CatalogId);
        Assert.Equal("1.9.0", policy.CatalogVersion);
        Assert.Equal(
            BuiltInCanonicalCapabilityPolicy.ExpectedSha256,
            policy.SourceSha256);
        Assert.Equal("NT51929", route.Identity.IcId);
        Assert.Equal("standard-merge", route.Identity.WorkflowId);
        Assert.Equal("selector-free", route.Identity.IcCountVariant);
        Assert.Equal("nt51929-standard-merge-256k", route.Identity.MapVariant);
        Assert.Equal(
            "7241e513c36122a60f2535836fd3b5625dc4cafe304d002e713a1525a949ac68",
            route.CapabilityFingerprint);
        Assert.Equal(CapabilityAuthoringAvailability.Available, route.Authoring.Value);
        Assert.Equal(CapabilityPublicationStatus.Supported, route.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.DirectGolden, route.Evidence.Value);
        Assert.Equal("nt51929-standard-merge-authoring-v2", route.Authoring.DecisionId);
        Assert.Equal(
            "owner-approved:github-issue-186",
            route.Authoring.SourceReference);
        Assert.Equal("nt51929-standard-merge-publication-v2", route.Publication.DecisionId);
        Assert.Equal(
            "owner-approved:github-issue-186",
            route.Publication.SourceReference);
        Assert.Equal("nt51929-standard-merge-evidence-v2", route.Evidence.DecisionId);
    }

    /// <summary>All retained DP Replace routes are hidden by one authoring-only owner decision.</summary>
    [Fact]
    public void DpReplaceAuthoringIsUnavailableWithoutChangingRetainedEvidence()
    {
        CanonicalCapabilityPolicySnapshot policy =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute[] routes =
        [
            .. policy.Routes.Where(static route =>
                StringComparer.Ordinal.Equals(
                    route.Identity.WorkflowId,
                    "dp-replace")),
        ];

        Assert.Equal(14, routes.Length);
        Assert.All(routes, static route =>
        {
            Assert.Equal(
                CapabilityAuthoringAvailability.Unavailable,
                route.Authoring.Value);
            Assert.Equal(
                "owner-decision:2026-08-24:dp-replace-hidden-until-1.1.0",
                route.Authoring.SourceReference);
            Assert.NotEqual(CapabilityEvidenceStatus.Missing, route.Evidence.Value);
            Assert.Equal(
                route.CapabilityFingerprint,
                route.Evidence.CapabilityFingerprint);
        });
    }

    /// <summary>The retired unclassified state migrates only its 72 exact routes to explicit internal decisions.</summary>
    [Fact]
    public void UnclassifiedRetirementPreservesReviewedPublicationDecisions()
    {
        CanonicalCapabilityPolicySnapshot policy =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute[] internalRoutes =
        [
            .. policy.Routes.Where(static route =>
                route.Publication.Value == CapabilityPublicationStatus.Internal),
        ];

        Assert.Equal(78, policy.Routes.Count);
        Assert.Equal(72, internalRoutes.Length);
        Assert.Equal(
            49,
            internalRoutes.Count(static route =>
                route.Publication.DecisionId.EndsWith(
                    "-publication-v3",
                    StringComparison.Ordinal)));
        Assert.Equal(
            23,
            internalRoutes.Count(static route =>
                route.Publication.DecisionId.EndsWith(
                    "-publication-v4",
                    StringComparison.Ordinal)));
        Assert.All(
            internalRoutes,
            static route => Assert.Equal(
                "owner-approved:github-issue-195",
                route.Publication.SourceReference));
        Assert.Equal(
            2,
            policy.Routes.Count(static route =>
                route.Publication.Value == CapabilityPublicationStatus.Supported));
        Assert.Equal(
            3,
            policy.Routes.Count(static route =>
                route.Publication.Value == CapabilityPublicationStatus.Candidate));
        _ = Assert.Single(
            policy.Routes,
            static route =>
                route.Publication.Value == CapabilityPublicationStatus.TestOnly);
    }

    /// <summary>The reviewed policy is copied to both build and publish outputs.</summary>
    [Fact]
    public void PolicyIsDeployedAndDeclaredForPublish()
    {
        string projectPath = RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Infrastructure",
            "NvtFwCombiner.Infrastructure.csproj");
        var project = XDocument.Load(projectPath);
        XElement content = Assert.Single(
            project.Descendants(),
            element =>
                element.Name.LocalName == "Content" &&
                NormalizePath((string?)element.Attribute("Include")) ==
                    BuiltInCanonicalCapabilityPolicy.RelativePath);
        string deployedPath = Path.Combine(
            AppContext.BaseDirectory,
            BuiltInCanonicalCapabilityPolicy.RelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));

        Assert.True(File.Exists(deployedPath), deployedPath);
        Assert.Equal(
            BuiltInCanonicalCapabilityPolicy.ExpectedSha256,
            PinnedJsonCatalogLoader.ComputeSha256(
                File.ReadAllBytes(deployedPath)));
        Assert.Equal(
            BuiltInCanonicalCapabilityPolicy.RelativePath,
            NormalizePath((string?)content.Attribute("Link")));
        Assert.Equal("PreserveNewest", (string?)content.Attribute("CopyToOutputDirectory"));
        Assert.Equal("PreserveNewest", (string?)content.Attribute("CopyToPublishDirectory"));
    }

    /// <summary>The pinned hash is the LF byte identity produced by Git clean export.</summary>
    [Fact]
    public void PinnedHashMatchesGitCleanExportBytes()
    {
        byte[] policy = ReadPolicy();
        string text = Encoding.UTF8.GetString(policy);
        byte[] cleanExportBytes = Encoding.UTF8.GetBytes(
            text.Replace("\r\n", "\n", StringComparison.Ordinal));
        string attributes = File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot(".gitattributes"));

        Assert.Contains("*.json text eol=lf", attributes, StringComparison.Ordinal);
        Assert.Equal(
            BuiltInCanonicalCapabilityPolicy.ExpectedSha256,
            PinnedJsonCatalogLoader.ComputeSha256(cleanExportBytes));
    }

    /// <summary>Raw-byte identity rejects rewriting the reviewed line endings.</summary>
    [Fact]
    public void RejectsLineEndingRewrite()
    {
        byte[] policy = ReadPolicy();
        string text = Encoding.UTF8.GetString(policy);
        byte[] rewritten = Encoding.UTF8.GetBytes(
            text.Contains("\r\n", StringComparison.Ordinal)
                ? text.Replace("\r\n", "\n", StringComparison.Ordinal)
                : text.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.NotEqual(policy, rewritten);
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInCanonicalCapabilityPolicy.Load(
                rewritten,
                BuiltInCanonicalCapabilityPolicy.ExpectedSha256));

        Assert.Contains("hash mismatch", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Unknown JSON properties fail before a route can be materialized.</summary>
    [Fact]
    public void RejectsUnknownProperty()
    {
        JsonObject policy = ParsePolicy();
        policy["unexpected"] = true;
        byte[] bytes = Encoding.UTF8.GetBytes(policy.ToJsonString());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInCanonicalCapabilityPolicy.Load(
                bytes,
                PinnedJsonCatalogLoader.ComputeSha256(bytes)));

        Assert.Contains("JSON is invalid", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Declared route identity must equal the identity derived from its selection axes.</summary>
    [Fact]
    public void RejectsRouteIdDrift()
    {
        JsonObject policy = ParsePolicy();
        JsonObject route = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(policy["routes"])[0]);
        route["routeId"] = "route-drift";
        byte[] bytes = Encoding.UTF8.GetBytes(policy.ToJsonString());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInCanonicalCapabilityPolicy.Load(
                bytes,
                PinnedJsonCatalogLoader.ComputeSha256(bytes)));

        Assert.Contains("routeId", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing publication decision is a policy integrity error, not implicit unavailability.</summary>
    [Fact]
    public void RejectsMissingPublicationDecision()
    {
        JsonObject policy = ParsePolicy();
        JsonObject route = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(policy["routes"])[0]);
        Assert.True(route.Remove("publication"));
        byte[] bytes = Encoding.UTF8.GetBytes(policy.ToJsonString());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInCanonicalCapabilityPolicy.Load(
                bytes,
                PinnedJsonCatalogLoader.ComputeSha256(bytes)));

        Assert.Contains("publication pin", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Every closed policy discriminator and root identity fails closed when changed.</summary>
    [Theory]
    [InlineData("schema")]
    [InlineData("catalog-id")]
    [InlineData("catalog-version")]
    [InlineData("date")]
    [InlineData("empty-routes")]
    [InlineData("duplicate-route")]
    [InlineData("authoring-value")]
    [InlineData("publication-value")]
    [InlineData("retired-publication-value")]
    [InlineData("evidence-value")]
    [InlineData("evidence-pin")]
    public void RejectsInvalidPolicySemantics(string mutation)
    {
        JsonObject policy = ParsePolicy();
        JsonArray routes = Assert.IsType<JsonArray>(policy["routes"]);
        JsonObject route = Assert.IsType<JsonObject>(routes[0]);
        switch (mutation)
        {
            case "schema":
                policy["schemaVersion"] = "2.0";
                break;
            case "catalog-id":
                policy["catalogId"] = "other";
                break;
            case "catalog-version":
                policy["catalogVersion"] = "2.0.0";
                break;
            case "date":
                policy["issuedOn"] = "2026-99-99";
                break;
            case "empty-routes":
                policy["routes"] = new JsonArray();
                break;
            case "duplicate-route":
                routes.Add(route.DeepClone());
                break;
            case "authoring-value":
                Assert.IsType<JsonObject>(route["authoring"])["value"] =
                    "unknown";
                break;
            case "publication-value":
                Assert.IsType<JsonObject>(route["publication"])["value"] =
                    "unknown";
                break;
            case "retired-publication-value":
                Assert.IsType<JsonObject>(route["publication"])["value"] =
                    "unclassified";
                break;
            case "evidence-value":
                Assert.IsType<JsonObject>(route["evidence"])["value"] =
                    "unknown";
                break;
            case "evidence-pin":
                Assert.IsType<JsonObject>(route["evidence"])
                    ["capabilityFingerprint"] = new string('0', 64);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unknown mutation.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(policy.ToJsonString());
        _ = Assert.Throws<InvalidDataException>(() =>
            BuiltInCanonicalCapabilityPolicy.Load(
                bytes,
                PinnedJsonCatalogLoader.ComputeSha256(bytes)));
    }

    private static byte[] ReadPolicy()
    {
        return File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            BuiltInCanonicalCapabilityPolicy.RelativePath.Split('/')));
    }

    private static JsonObject ParsePolicy()
    {
        return Assert.IsType<JsonObject>(
            JsonNode.Parse(ReadPolicy()));
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
}
