using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Checks private evidence lookup with independent synthetic data.</summary>
public sealed class ConfidentialReferenceTestDataTests
{
    /// <summary>A matching inventory identity returns verified bytes.</summary>
    [Fact]
    public void MatchingInventoryDigestReturnsVerifiedBytes()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-match");
        byte[] expected = Encoding.UTF8.GetBytes("synthetic reference");
        string digest = Convert.ToHexStringLower(SHA256.HashData(expected));
        _ = workspace.Write("nfc/references/synthetic/reference.dat", expected);
        _ = workspace.Write("nfc/references/SHA256SUMS",
            Encoding.UTF8.GetBytes($"{digest}  synthetic/reference.dat\n"));

        Assert.Equal(expected, ConfidentialReferenceTestData.ReadVerifiedBytes(digest, workspace.Root));
    }

    /// <summary>Changed private bytes fail the pinned digest check.</summary>
    [Fact]
    public void MismatchingInventoryDigestRejectsPrivateBytes()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-drift");
        byte[] expected = Encoding.UTF8.GetBytes("synthetic reference");
        string digest = Convert.ToHexStringLower(SHA256.HashData(expected));
        _ = workspace.Write("nfc/references/synthetic/reference.dat", Encoding.UTF8.GetBytes("drifted"));
        _ = workspace.Write("nfc/references/SHA256SUMS",
            Encoding.UTF8.GetBytes($"{digest}  synthetic/reference.dat\n"));

        _ = Assert.Throws<InvalidDataException>(() =>
            ConfidentialReferenceTestData.ReadVerifiedBytes(digest, workspace.Root));
    }

    /// <summary>An inventory path cannot escape its reference root.</summary>
    [Fact]
    public void InventoryPathCannotEscapePrivateReferenceRoot()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-escape");
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic")));
        _ = workspace.Write("nfc/references/SHA256SUMS",
            Encoding.UTF8.GetBytes($"{digest}  ../outside.dat\n"));

        _ = Assert.Throws<InvalidOperationException>(() =>
            ConfidentialReferenceTestData.ReadVerifiedBytes(digest, workspace.Root));
    }
}
