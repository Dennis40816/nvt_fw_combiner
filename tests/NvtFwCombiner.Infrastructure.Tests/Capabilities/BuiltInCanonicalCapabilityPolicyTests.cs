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
    /// <summary>The checked-in NT51929 pilot policy loads with all decisions pinned.</summary>
    [Fact]
    public void LoadsCheckedInNt51929Policy()
    {
        CanonicalCapabilityPolicySnapshot policy =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute route = Assert.Single(
            policy.Routes,
            static candidate => StringComparer.Ordinal.Equals(
                candidate.Identity.WorkflowId,
                "standard-merge"));

        Assert.Equal("canonical-capability-policy", policy.CatalogId);
        Assert.Equal("1.2.0", policy.CatalogVersion);
        Assert.Equal(
            BuiltInCanonicalCapabilityPolicy.ExpectedSha256,
            policy.SourceSha256);
        Assert.Equal("NT51929", route.Identity.IcId);
        Assert.Equal("standard-merge", route.Identity.WorkflowId);
        Assert.Equal("selector-free", route.Identity.IcCountVariant);
        Assert.Equal("nt51929-standard-merge-256k", route.Identity.MapVariant);
        Assert.Equal(
            "894c809b9e928df044a89b95dedafd282966166bc45d00592dcd9cf56703dd93",
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
            BuiltInCanonicalCapabilityPolicy.RelativePath,
            NormalizePath((string?)content.Attribute("Link")));
        Assert.Equal("PreserveNewest", (string?)content.Attribute("CopyToOutputDirectory"));
        Assert.Equal("PreserveNewest", (string?)content.Attribute("CopyToPublishDirectory"));
    }

    /// <summary>Raw-byte identity rejects a CRLF rewrite of the reviewed LF policy.</summary>
    [Fact]
    public void RejectsLineEndingRewrite()
    {
        byte[] policy = ReadPolicy();
        byte[] rewritten = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(policy).ReplaceLineEndings("\r\n"));

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
