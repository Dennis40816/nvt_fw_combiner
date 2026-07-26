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
        CanonicalCapabilityPolicyRoute route = Assert.Single(policy.Routes);

        Assert.Equal("canonical-capability-policy", policy.CatalogId);
        Assert.Equal("1.0.0", policy.CatalogVersion);
        Assert.Equal(
            BuiltInCanonicalCapabilityPolicy.ExpectedSha256,
            policy.SourceSha256);
        Assert.Equal("NT51929", route.Identity.IcId);
        Assert.Equal("standard-merge", route.Identity.WorkflowId);
        Assert.Equal("selector-free", route.Identity.IcCountVariant);
        Assert.Equal("nt51929-standard-merge-256k", route.Identity.MapVariant);
        Assert.Equal(
            "f719dc53dfd6d5aeeb7f7168937c1920e5b2d4f4f266f3404ba2a7b94a504d67",
            route.CapabilityFingerprint);
        Assert.Equal(CapabilityAuthoringAvailability.Available, route.Authoring.Value);
        Assert.Equal(CapabilityPublicationStatus.Supported, route.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.DirectGolden, route.Evidence.Value);
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
