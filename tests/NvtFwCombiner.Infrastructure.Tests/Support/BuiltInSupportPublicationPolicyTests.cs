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
}
