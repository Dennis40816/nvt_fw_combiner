using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Exact reviewed-byte identities must be present in the compiled admission assembly.</summary>
public sealed class PrebuiltProfileCatalogBuildIdentityTests
{
    /// <summary>Both identity fields expose missing, duplicate and invalid failures without parsing messages.</summary>
    [Theory]
    [InlineData(0, "Missing")]
    [InlineData(0, "Duplicate")]
    [InlineData(0, "Invalid")]
    [InlineData(1, "Missing")]
    [InlineData(1, "Duplicate")]
    [InlineData(1, "Invalid")]
    public void MetadataFailureIdentifiesFieldAndKind(int field, string kind)
    {
        var attributes = new List<AssemblyMetadataAttribute>
        {
            new(BuiltInProfileBuildAdmissionIdentity.TrustIndexKey, new string('a', 64)),
            new(BuiltInProfileBuildAdmissionIdentity.ManifestSetKey, new string('b', 64)),
        };
        if (kind == "Missing") { attributes.RemoveAt(field); }
        else if (kind == "Duplicate") { attributes.Add(attributes[field]); }
        else { attributes[field] = new(attributes[field].Key, "invalid"); }
        Assert.Null(BuiltInProfileBuildAdmissionIdentity.TryRead(attributes, out BuildAdmissionIdentityFailure? failure));
        Assert.NotNull(failure);
        Assert.Equal(field == 0 ? "TrustIndex" : "ManifestSet", failure.Field.ToString());
        Assert.Equal(kind, failure.Kind.ToString());
    }

    /// <summary>Independent canonical-array calculation verifies metadata was emitted before compilation.</summary>
    [Fact]
    public void CompiledAdmissionIdentityMatchesReviewedBytes()
    {
        string root = RepositoryPaths.FromRepositoryRoot("profiles", "built-in");
        byte[] index = File.ReadAllBytes(Path.Combine(root, "package-trust-index.json"));
        using var document = JsonDocument.Parse(index);
        string manifests = "[" + string.Join(',', document.RootElement.GetProperty("bundles").EnumerateArray()
            .Select(b => b.GetProperty("bundleDirectory").GetString()!).Order(StringComparer.Ordinal)
            .Select(d => "{\"bundleDirectory\":\"" + d + "\",\"manifestSha256\":\"" +
                Hash(File.ReadAllBytes(Path.Combine(root, d, "profile-bundle.json"))) + "\"}")) + "]";
        AssemblyMetadataAttribute[] attributes = [.. typeof(ProfileBundleLoader).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()];
        Assert.Equal(Hash(index), Assert.Single(attributes, a => a.Key == "NfcBuiltInTrustIndexSha256").Value);
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(manifests)),
            Assert.Single(attributes, a => a.Key == "NfcBuiltInManifestSetSha256").Value);
    }

    /// <summary>Metadata must contain exactly one lowercase SHA-256 for each designated key.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("uppercase")]
    [InlineData("short")]
    [InlineData("null")]
    public void RejectsMissingDuplicateOrNoncanonicalMetadata(string mutation)
    {
        var attributes = new List<AssemblyMetadataAttribute>
        {
            new(BuiltInProfileBuildAdmissionIdentity.TrustIndexKey, new string('a', 64)),
            new(BuiltInProfileBuildAdmissionIdentity.ManifestSetKey, new string('b', 64)),
        };
        switch (mutation)
        {
            case "missing": attributes.RemoveAt(0); break;
            case "duplicate": attributes.Add(attributes[0]); break;
            case "uppercase": attributes[0] = new(attributes[0].Key, new string('A', 64)); break;
            case "short": attributes[0] = new(attributes[0].Key, "a"); break;
            case "null": attributes[0] = new(attributes[0].Key, null); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        _ = Assert.Throws<InvalidDataException>(() => BuiltInProfileBuildAdmissionIdentity.Read(attributes));
    }

    /// <summary>Set identity is independent of enumeration order, but sensitive to addition and removal.</summary>
    [Fact]
    public void ManifestSetIdentitySortsDirectoriesAndDetectsSetDrift()
    {
        (string, string) first = ("a", new string('a', 64)), second = ("b", new string('b', 64));
        string expected = Hash(Encoding.UTF8.GetBytes("[{\"bundleDirectory\":\"a\",\"manifestSha256\":\"" + new string('a', 64) +
            "\"},{\"bundleDirectory\":\"b\",\"manifestSha256\":\"" + new string('b', 64) + "\"}]"));
        Assert.Equal(expected, BuiltInProfileBuildAdmissionIdentity.CalculateManifestSet([second, first]));
        Assert.NotEqual(expected, BuiltInProfileBuildAdmissionIdentity.CalculateManifestSet([first]));
        Assert.NotEqual(expected, BuiltInProfileBuildAdmissionIdentity.CalculateManifestSet([first, second, ("c", new string('c', 64))]));
        _ = Assert.Throws<InvalidDataException>(() => new BuiltInProfileBuildAdmissionIdentity(new string('d', 64), expected)
            .Verify(new string('e', 64), expected));
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
