using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Exact reviewed-byte identity emitted before the admission implementation is compiled.</summary>
internal sealed record BuiltInProfileBuildAdmissionIdentity(string TrustIndexSha256, string ManifestSetSha256)
{
    internal const string TrustIndexKey = "NfcBuiltInTrustIndexSha256";
    internal const string ManifestSetKey = "NfcBuiltInManifestSetSha256";

    internal static BuiltInProfileBuildAdmissionIdentity Read()
    {
        return Read(typeof(BuiltInProfileBuildAdmissionIdentity).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>());
    }

    internal static BuiltInProfileBuildAdmissionIdentity Read(IEnumerable<AssemblyMetadataAttribute> attributes)
    {
        AssemblyMetadataAttribute[] snapshot = [.. attributes];
        return new(ReadValue(TrustIndexKey), ReadValue(ManifestSetKey));

        string ReadValue(string key)
        {
            string?[] values = [.. snapshot.Where(a => a.Key == key).Select(a => a.Value)];
            return values.Length == 1 && values[0] is { } value && value.Length == 64 &&
                value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
                    ? value : throw new InvalidDataException("Missing, duplicate or noncanonical build admission identity: " + key);
        }
    }

    internal static string CalculateManifestSet(IEnumerable<(string BundleDirectory, string ManifestSha256)> manifests)
    {
        var array = new JsonArray();
        foreach ((string directory, string hash) in manifests.OrderBy(m => m.BundleDirectory, StringComparer.Ordinal))
        {
            array.Add(new JsonObject { ["bundleDirectory"] = directory, ["manifestSha256"] = hash });
        }
        using var document = JsonDocument.Parse(array.ToJsonString());
        return Convert.ToHexStringLower(SHA256.HashData(PrebuiltProfileCatalogCanonicalJson.Encode(document.RootElement)));
    }

    internal void Verify(string indexHash, string manifestSetHash)
    {
        if (TrustIndexSha256 != indexHash || ManifestSetSha256 != manifestSetHash)
        {
            throw new InvalidDataException("Built-in profile inputs differ from the compiled admission identity.");
        }
    }
}
