using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Infrastructure.Support;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Support;

/// <summary>Hash-closure tests for the built-in support publication policy adapter.</summary>
public sealed class BuiltInSupportPublicationPolicyTests
{
    /// <summary>Verifies the shipped policy loads only through its declared SHA-256 value.</summary>
    [Fact]
    public void LoadsTheCheckedInPolicyThroughItsPinnedHash()
    {
        SupportPublicationPolicySnapshot policy = BuiltInSupportPublicationPolicy.Load();

        Assert.Equal("support-publication-policy", policy.PolicyId);
        Assert.Equal("1.0.0", policy.PolicyVersion);
        Assert.Equal("af3feb72cf0db6d90a47199cd4e78d08ac62d15dc5057b9cbb0359cb23fb5851", policy.Sha256);
        Assert.Equal(5, policy.Decisions.Count);
        Assert.Contains(policy.Decisions, decision =>
            decision.RouteId == "nt51950-ab-merge-single" &&
            decision.Status == SupportPublicationStatus.Candidate);
    }

    /// <summary>Verifies a one-byte policy mutation fails before it can materialize any status.</summary>
    [Fact]
    public void RejectsAChangedPolicyByteBeforeMaterialization()
    {
        byte[] policy = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "support-publication-policy-v1.json"));
        policy[^2] ^= 0x01;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInSupportPublicationPolicy.Load(
                policy,
                "af3feb72cf0db6d90a47199cd4e78d08ac62d15dc5057b9cbb0359cb23fb5851"));

        Assert.Contains("hash mismatch", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies declared policy supersession metadata is retained through the strict generated JSON contract.</summary>
    [Fact]
    public void RetainsDeclaredPolicyAndDecisionSupersessionMetadata()
    {
        byte[] policyBytes = System.Text.Encoding.UTF8.GetBytes(CreatePolicyJson("1.0.0", "prior"));

        SupportPublicationPolicySnapshot policy = BuiltInSupportPublicationPolicy.Load(
            policyBytes,
            PinnedJsonCatalogLoader.ComputeCanonicalSha256(policyBytes));

        Assert.Equal("1.0.0", policy.SupersedesPolicyVersion);
        Assert.Equal(["prior"], Assert.Single(policy.Decisions).SupersedesDecisionIds);
    }

    /// <summary>Verifies generated metadata rejects both unknown and case-mismatched JSON fields.</summary>
    [Fact]
    public void RejectsUnknownAndCaseMismatchedPolicyPropertiesThroughGeneratedMetadata()
    {
        JsonObject unknownProperty = CreatePolicyObject();
        unknownProperty["unexpected"] = true;
        JsonObject caseMismatchedProperty = CreatePolicyObject();
        JsonNode? schemaVersion = caseMismatchedProperty["schemaVersion"];
        Assert.True(caseMismatchedProperty.Remove("schemaVersion"));
        caseMismatchedProperty["SchemaVersion"] = schemaVersion;

        AssertInvalidPolicy(unknownProperty.ToJsonString(), "JSON is invalid");
        AssertInvalidPolicy(caseMismatchedProperty.ToJsonString(), "JSON is invalid");
    }

    /// <summary>Verifies schema-shaped supersession fields are still validated by the runtime adapter.</summary>
    [Fact]
    public void RejectsMalformedPolicyAndDecisionSupersessionFields()
    {
        string malformedVersion = CreatePolicyJson("not-a-semver");
        string malformedDecisionId = CreatePolicyJson(null, "Not-An-Id");
        string selfSupersession = CreatePolicyJson(null, "current");

        AssertInvalidPolicy(malformedVersion, "supersedesPolicyVersion");
        AssertInvalidPolicy(malformedDecisionId, "supersession");
        AssertInvalidPolicy(selfSupersession, "supersession");
    }

    private static void AssertInvalidPolicy(string policyJson, string expectedMessage)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(policyJson);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            BuiltInSupportPublicationPolicy.Load(bytes, PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes)));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    private static string CreatePolicyJson(string? supersedesPolicyVersion = null, params string[] supersedesDecisionIds)
    {
        return CreatePolicyObject(supersedesPolicyVersion, supersedesDecisionIds).ToJsonString();
    }

    private static JsonObject CreatePolicyObject(
        string? supersedesPolicyVersion = null,
        params string[] supersedesDecisionIds)
    {
        var decision = new JsonObject
        {
            ["decisionId"] = "current",
            ["routeId"] = "nt51950-ab-merge-single",
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
            var decisionIds = new JsonArray();
            foreach (string decisionId in supersedesDecisionIds)
            {
                decisionIds.Add(decisionId);
            }

            decision["supersedesDecisionIds"] = decisionIds;
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
