using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Tests the exact two-value release license rule in the verifier and the embedded schema.</summary>
public sealed class ReleaseManifestLicenseTests
{
    private const string Version = "1.2.5";

    private const string ManifestTemplate = /*lang=json,strict*/ """
        {
          "schemaVersion": "1.1",
          "product": "NVT FW Combiner",
          "version": "1.2.5",
          "sourceCommit": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "sourceTag": "v1.2.5",
          "runtimeIdentifier": "win-x64",
          "licenseSpdx": "MIT",
          "workerProtocolVersions": ["1.0"],
          "approvedProcessorIds": [],
          "processorBundleSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
          "embeddedProfileCatalogSha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
          "embeddedSchemaBundleSha256": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
          "files": [
            { "path": "NvtFwCombiner.exe", "size": 1, "sha256": "1111111111111111111111111111111111111111111111111111111111111111", "role": "application" },
            { "path": "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe", "size": 1, "sha256": "2222222222222222222222222222222222222222222222222222222222222222", "role": "externalTool" },
            { "path": "THIRD-PARTY-NOTICES.txt", "size": 1, "sha256": "3333333333333333333333333333333333333333333333333333333333333333", "role": "notices" },
            { "path": "LICENSE.txt", "size": 1, "sha256": "4444444444444444444444444444444444444444444444444444444444444444", "role": "license" },
            { "path": "README.txt", "size": 1, "sha256": "5555555555555555555555555555555555555555555555555555555555555555", "role": "readme" },
            { "path": "docs/contracts/canonical-capability-policy-v1.json", "size": 1, "sha256": "6666666666666666666666666666666666666666666666666666666666666666", "role": "capabilityPolicy" },
            { "path": "profiles/built-in/package-trust-index.json", "size": 1, "sha256": "7777777777777777777777777777777777777777777777777777777777777777", "role": "builtInProfile" }
          ],
          "sbomAsset": "NvtFwCombiner-v1.2.5-win-x64.spdx.json",
          "provenanceAsset": "NvtFwCombiner-v1.2.5-win-x64.provenance.json"
        }
        """;

    /// <summary>The verifier accepts MIT and the future proprietary license.</summary>
    [Theory]
    [InlineData("MIT")]
    [InlineData("LicenseRef-Proprietary")]
    public void VerifierAcceptsExactReleaseLicenses(string license)
    {
        Assert.True(ManagedPackageVerifier.IsAcceptedLicense(license));
    }

    /// <summary>The verifier rejects every other value, including case and whitespace variants.</summary>
    [Theory]
    [InlineData("Proprietary")]
    [InlineData("mit")]
    [InlineData(" MIT")]
    [InlineData("LicenseRef-proprietary")]
    [InlineData("")]
    [InlineData(null)]
    public void VerifierRejectsOtherReleaseLicenses(string? license)
    {
        Assert.False(ManagedPackageVerifier.IsAcceptedLicense(license));
    }

    /// <summary>The embedded schema and the full manifest read accept both exact licenses.</summary>
    [Theory]
    [InlineData("MIT")]
    [InlineData("LicenseRef-Proprietary")]
    public void SchemaAndCanonicalManifestReadAcceptExactReleaseLicenses(string license)
    {
        byte[] manifest = CreateManifest(license);
        using JsonDocument document = JsonDocument.Parse(manifest);

        Assert.True(ReleaseManifestSchema.IsValid(document.RootElement));
        Assert.True(ManagedPackageVerifier.TryReadCanonicalManifest(
            manifest,
            ManagedAppVersion.Parse(Version),
            archivePaths: null,
            out ReleaseManifestDocument? parsed));
        Assert.Equal(license, parsed.LicenseSpdx);
    }

    /// <summary>The embedded schema and the full manifest read reject every other license value.</summary>
    [Theory]
    [InlineData("Proprietary")]
    [InlineData("mit")]
    [InlineData(" MIT")]
    [InlineData("LicenseRef-proprietary")]
    [InlineData("")]
    [InlineData(null)]
    public void SchemaAndCanonicalManifestReadRejectOtherReleaseLicenses(string? license)
    {
        byte[] manifest = CreateManifest(license);
        using JsonDocument document = JsonDocument.Parse(manifest);

        Assert.False(ReleaseManifestSchema.IsValid(document.RootElement));
        Assert.False(ManagedPackageVerifier.TryReadCanonicalManifest(
            manifest,
            ManagedAppVersion.Parse(Version),
            archivePaths: null,
            out _));
    }

    private static byte[] CreateManifest(string? license)
    {
        JsonObject manifest = JsonNode.Parse(ManifestTemplate)!.AsObject();
        manifest["licenseSpdx"] = license;
        return JsonSerializer.SerializeToUtf8Bytes(manifest);
    }
}
