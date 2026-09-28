namespace NvtFwCombiner.Infrastructure.Bundles;

internal enum PrebuiltProfileCatalogDecodeFailure { Format, BodyIntegrity, ManifestIntegrity, DocumentIntegrity }

/// <summary>ADR 0077 byte-container limits, independent of runtime admission.</summary>
internal static class PrebuiltProfileCatalogFormat
{
    internal const int MaximumFileBytes = 4_194_304;
    internal const int MaximumHeaderBytes = 1_048_576;
    internal const int PrefixBytes = 12;
    internal const int MinimumHeaderBytes = 1;
    internal const int MaximumBodyBytes = MaximumFileBytes - PrefixBytes - MinimumHeaderBytes;
}

/// <summary>Raw transport inputs. Possessing these snapshots does not grant bundle admission.</summary>
internal sealed record PrebuiltProfileCatalogBodyInput(
    string BundleDirectory,
    ProfileBundleFileSnapshot Manifest,
    IReadOnlyList<ProfileBundleEntrySnapshot> Documents);

/// <summary>One half-open body-relative range; a null entryId denotes that bundle's manifest.</summary>
internal sealed record PrebuiltProfileCatalogBodyRange(
    string BundleDirectory,
    string? EntryId,
    int Offset,
    int Length);

/// <summary>Privately owned encoded body with an immutable ordered range table, not a complete pack.</summary>
internal sealed class PrebuiltProfileCatalogBody
{
    private readonly byte[] _content;

    internal PrebuiltProfileCatalogBody(byte[] ownedContent, IEnumerable<PrebuiltProfileCatalogBodyRange> ranges)
    {
        _content = ownedContent;
        Ranges = Array.AsReadOnly(ranges.ToArray());
    }

    internal ReadOnlySpan<byte> Content => _content;

    internal IReadOnlyList<PrebuiltProfileCatalogBodyRange> Ranges { get; }
}

/// <summary>Format identities only; none conveys admission authority.</summary>
internal sealed record PrebuiltProfileCatalogTrustIdentity(
    string Sha256, string TrustIndexId, string TrustIndexVersion, string TrustAnchorBindingId);

/// <summary>Raw bundle transport descriptor, supplied only after admission by the generator.</summary>
internal sealed record PrebuiltProfileCatalogBundleInput(
    string BundleDirectory, string BundleVersion, string ContentHash,
    ProfileBundleFileSnapshot Manifest, IReadOnlyList<ProfileBundleEntrySnapshot> Documents);

/// <summary>Validated transport snapshot. Runtime trust acceptance remains a separate responsibility.</summary>
internal sealed class PrebuiltProfileCatalogSnapshot
{
    private readonly byte[] _body;
    internal PrebuiltProfileCatalogSnapshot(System.Text.Json.JsonElement header, byte[] body)
    {
        Header = header.Clone();
        _body = body;
    }
    internal System.Text.Json.JsonElement Header { get; }
    internal ReadOnlySpan<byte> Body => _body;
}
