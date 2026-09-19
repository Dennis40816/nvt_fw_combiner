using System.Formats.Asn1;
using System.Security.Cryptography;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Strict native signature preconditions and verified-signer organization parsing.</summary>
public sealed class RuntimeTrustProbeTests
{
    /// <summary>Publisher matching uses the exact Organization OID, never the display common name.</summary>
    [Theory]
    [InlineData("Microsoft Corporation", "Windows Software Compatibility Publisher", true)]
    [InlineData("Other Publisher", "Microsoft Corporation", false)]
    [InlineData("microsoft corporation", "Microsoft Corporation", false)]
    [InlineData("Microsoft Corporation ", "Microsoft Corporation", false)]
    public void PublisherRequiresExactOrganization(string organization, string commonName, bool expected)
    {
        Assert.Equal(expected, WindowsRuntimeTrustProbe.HasMicrosoftOrganization(Subject(organization, commonName)));
    }

    /// <summary>Missing, duplicate, or malformed Organization claims cannot identify the required publisher.</summary>
    [Fact]
    public void AmbiguousOrMalformedPublisherIsRejected()
    {
        Assert.False(WindowsRuntimeTrustProbe.HasMicrosoftOrganization(Subject(null, "Microsoft Corporation")));
        Assert.False(WindowsRuntimeTrustProbe.HasMicrosoftOrganization(Subject("Microsoft Corporation", "Microsoft", duplicate: true)));
        Assert.False(WindowsRuntimeTrustProbe.HasMicrosoftOrganization(new byte[] { 0xFF }));
    }

    /// <summary>Missing and malformed input fails before native trust, without creating a file.</summary>
    [Fact]
    public void MissingFileAndInvalidPinFailClosed()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = TempWorkspace.Create();
        Assert.Equal("runtime.trust.hash-invalid", WindowsRuntimeTrustProbe.Verify(workspace.PathFor("missing.dll"), "bad"));
        Assert.Equal("runtime.trust.file-unavailable", WindowsRuntimeTrustProbe.Verify(workspace.PathFor("missing.dll"), new('a', 64)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Exact hashes cannot make unsigned or malformed bytes trusted, and changed bytes reject before trust.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsignedAndChangedBytesFailClosed(bool malformed)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = TempWorkspace.Create();
        byte[] bytes = malformed ? [0x4D, 0x5A, 0xA1, 0xB2] : File.ReadAllBytes(typeof(TempWorkspace).Assembly.Location);
        string path = workspace.Write("unsigned.dll", bytes);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Equal("runtime.trust.signature-invalid", WindowsRuntimeTrustProbe.Verify(path, hash));
        Assert.Equal("runtime.trust.hash-mismatch", WindowsRuntimeTrustProbe.Verify(path, new('a', 64)));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    /// <summary>A pre-existing write handle prevents verification from acquiring stable custody.</summary>
    [Fact]
    public void WritableOrOversizedFilesAreRejected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("candidate.dll", [0xA1]);
        using (var writable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            Assert.Equal("runtime.trust.file-unavailable", WindowsRuntimeTrustProbe.Verify(path, new('a', 64)));
        }

        using (var large = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            large.SetLength(8_388_609);
        }

        Assert.Equal("runtime.trust.file-too-large", WindowsRuntimeTrustProbe.Verify(path, new('a', 64)));
    }

    /// <summary>Records real bundled/system outcomes without requiring revocation networking or treating rejection as trust.</summary>
    [Fact]
    public void ObserveRealWindowsTrustWithoutPromotingUnavailableRevocation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string bundled = RepositoryPaths.FromRepositoryRoot("external-tools", "legacy-combiner", "1.13.0", "vcruntime140.dll");
        string system = Path.Combine(Environment.SystemDirectory, "vcruntime140.dll");
        foreach (string path in new[] { bundled, system }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                TestContext.Current.TestOutputHelper?.WriteLine($"OBSERVATION unavailable: {path}");
                continue;
            }

            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            string? result = WindowsRuntimeTrustProbe.Verify(path, hash);
            TestContext.Current.TestOutputHelper?.WriteLine($"OBSERVATION path={path}; sha256={hash}; result={result ?? "verified"}");
            Assert.True(result is null or "runtime.trust.revocation-unavailable" or "runtime.trust.revoked" or
                "runtime.trust.signature-invalid" or "runtime.trust.publisher-mismatch", result);
        }
    }

    private static byte[] Subject(string? organization, string commonName, bool duplicate = false)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            Attribute("2.5.4.3", commonName);
            if (organization is not null)
            {
                Attribute("2.5.4.10", organization);
                if (duplicate)
                {
                    Attribute("2.5.4.10", "Other Publisher");
                }
            }
        }

        return writer.Encode();

        void Attribute(string oid, string value)
        {
            using (writer.PushSetOf())
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(oid);
                writer.WriteCharacterString(UniversalTagNumber.UTF8String, value);
            }
        }
    }
}
