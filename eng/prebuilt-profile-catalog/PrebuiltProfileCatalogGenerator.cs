using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.PrebuiltProfileCatalogGeneration;

/// <summary>Build-only generator that reuses production admission and preserves admitted raw bytes.</summary>
public static class PrebuiltProfileCatalogGenerator
{
    /// <summary>Admits all indexed bundles against the compiled identity and atomically writes one pack.</summary>
    public static void Generate(string bundleRoot, string trustIndexPath, string outputPath)
    {
        Generate(bundleRoot, trustIndexPath, outputPath, null);
    }

    // Tests can supply a synthetic compiled identity; the shipped entry always reads Infrastructure metadata.
    internal static void Generate(string bundleRoot, string trustIndexPath, string outputPath,
        BuiltInProfileBuildAdmissionIdentity? expectedIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustIndexPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        string output = Path.GetFullPath(outputPath);
        if (!output.EndsWith(".pack", StringComparison.Ordinal) ||
            StringComparer.OrdinalIgnoreCase.Equals(output, Path.GetFullPath(trustIndexPath)))
        {
            throw new ArgumentException("Generator output must be a distinct .pack file.", nameof(outputPath));
        }
        File.Delete(output);
        string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            BuiltInProfileBuildAdmissionIdentity expected = expectedIdentity ?? BuiltInProfileBuildAdmissionIdentity.Read();
            ProfileBundlePackageTrustIndex index = ProfileBundlePackageTrustIndexLoader.Load(trustIndexPath);
            var inputs = new List<PrebuiltProfileCatalogBundleInput>();
            foreach (ProfileBundlePackageTrustEntry entry in index.Bundles.OrderBy(b => b.BundleDirectory, StringComparer.Ordinal))
            {
                TrustedProfileBundle bundle = ProfileBundleLoader.Load(
                    Path.Combine(bundleRoot, entry.BundleDirectory), "profile-bundle.json",
                    new ProfileBundleTrustAnchor(entry.ContentHash, index.TrustAnchorBindingId),
                    BuiltInProfileBundleAdmissionSettings.Limits);
                if (!StringComparer.Ordinal.Equals(bundle.Manifest.BundleVersion, entry.BundleVersion))
                {
                    throw new InvalidDataException("Bundle version does not match the package trust index.");
                }
                TrustedProfileBundleDocumentProjection projection = bundle.CreateDocumentProjection();
                inputs.Add(new(entry.BundleDirectory, projection.BundleVersion, projection.BundleContentHash,
                    projection.ManifestSnapshot, projection.Documents));
            }
            expected.Verify(index.ActualSha256, BuiltInProfileBuildAdmissionIdentity.CalculateManifestSet(
                inputs.Select(b => (b.BundleDirectory, b.Manifest.ActualSha256))));
            byte[] pack = PrebuiltProfileCatalogCodec.Encode(new(index.ActualSha256, index.TrustIndexId,
                index.TrustIndexVersion, index.TrustAnchorBindingId), inputs);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(pack);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, output);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
