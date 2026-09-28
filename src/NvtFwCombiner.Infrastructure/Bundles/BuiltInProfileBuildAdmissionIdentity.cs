using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.Infrastructure.Bundles;

internal enum BuildAdmissionIdentityField { TrustIndex, ManifestSet }
internal enum BuildAdmissionIdentityFailureKind { Missing, Duplicate, Invalid }
internal sealed record BuildAdmissionIdentityFailure(
    BuildAdmissionIdentityField Field, BuildAdmissionIdentityFailureKind Kind);

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
        return TryRead(attributes, out BuildAdmissionIdentityFailure? failure) ??
            throw new InvalidDataException($"Build admission identity {failure!.Field} is {failure.Kind}.");
    }

    internal static BuiltInProfileBuildAdmissionIdentity? TryRead(out BuildAdmissionIdentityFailure? failure)
    {
        return TryRead(typeof(BuiltInProfileBuildAdmissionIdentity).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(), out failure);
    }

    internal static BuiltInProfileBuildAdmissionIdentity? TryRead(
        IEnumerable<AssemblyMetadataAttribute> attributes, out BuildAdmissionIdentityFailure? failure)
    {
        AssemblyMetadataAttribute[] snapshot = [.. attributes];
        failure = ReadValue(TrustIndexKey, BuildAdmissionIdentityField.TrustIndex, out string? index);
        if (failure is not null) { return null; }
        failure = ReadValue(ManifestSetKey, BuildAdmissionIdentityField.ManifestSet, out string? manifests);
        return failure is null ? new(index!, manifests!) : null;

        BuildAdmissionIdentityFailure? ReadValue(string key, BuildAdmissionIdentityField field, out string? result)
        {
            string?[] values = [.. snapshot.Where(a => a.Key == key).Select(a => a.Value)];
            result = null;
            if (values.Length == 0) { return new(field, BuildAdmissionIdentityFailureKind.Missing); }
            if (values.Length != 1) { return new(field, BuildAdmissionIdentityFailureKind.Duplicate); }
            if (values[0] is not { Length: 64 } value ||
                !value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
            { return new(field, BuildAdmissionIdentityFailureKind.Invalid); }
            result = value;
            return null;
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
