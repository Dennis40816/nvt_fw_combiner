using System.Globalization;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Infrastructure.Support;

/// <summary>Loads the owner-approved publication policy only after its shipped bytes match the reviewed hash.</summary>
internal static class BuiltInSupportPublicationPolicy
{
    private const string RelativePath = "docs/contracts/support-publication-policy-v1.json";
    private const string ExpectedSha256 = "af3feb72cf0db6d90a47199cd4e78d08ac62d15dc5057b9cbb0359cb23fb5851";

    internal static SupportPublicationPolicySnapshot Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return Load(File.ReadAllBytes(path), ExpectedSha256);
    }

    internal static SupportPublicationPolicySnapshot Load(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        PolicyDocument document = PinnedJsonCatalogLoader.Load<PolicyDocument>(
            bytes,
            expectedSha256,
            "Built-in support publication policy",
            "Built-in support publication policy has an invalid empty document.");
        IReadOnlyList<DecisionDocument> decisions = document.Decisions ??
            throw Invalid("decisions");
        return !StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0") ||
            !StringComparer.Ordinal.Equals(document.PolicyId, "support-publication-policy") ||
            !IsIsoDate(document.IssuedOn)
            ? throw Invalid("schemaVersion, policyId, or issuedOn")
            : new SupportPublicationPolicySnapshot(
                document.PolicyId,
                document.PolicyVersion,
                PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes),
                Array.AsReadOnly(decisions.Select(CreateDecision).ToArray()));
    }

    private static SupportPublicationDecision CreateDecision(DecisionDocument source)
    {
        ProvenanceDocument provenance = source.Provenance ?? throw Invalid("decision provenance");
        return !IsIsoDate(provenance.RecordedOn)
            ? throw Invalid("decision provenance")
            : new SupportPublicationDecision(
                source.DecisionId,
                source.RouteId,
                source.Status switch
                {
                    "supported" => SupportPublicationStatus.Supported,
                    "candidate" => SupportPublicationStatus.Candidate,
                    "internal" => SupportPublicationStatus.Internal,
                    "test-only" => SupportPublicationStatus.TestOnly,
                    "unclassified" => SupportPublicationStatus.Unclassified,
                    _ => throw Invalid("decision status"),
                },
                new SupportPublicationProvenance(
                    provenance.AuthorityKind,
                    provenance.RecordedOn,
                    provenance.RecordRef,
                    provenance.Rationale));
    }

    private static bool IsIsoDate(string value)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static InvalidDataException Invalid(string field)
    {
        return new InvalidDataException($"Built-in support publication policy has invalid {field}.");
    }

    private sealed record PolicyDocument(
        [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
        [property: JsonPropertyName("policyId")] string PolicyId,
        [property: JsonPropertyName("policyVersion")] string PolicyVersion,
        [property: JsonPropertyName("issuedOn")] string IssuedOn,
        [property: JsonPropertyName("decisions")] IReadOnlyList<DecisionDocument>? Decisions);

    private sealed record DecisionDocument(
        [property: JsonPropertyName("decisionId")] string DecisionId,
        [property: JsonPropertyName("routeId")] string RouteId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("provenance")] ProvenanceDocument? Provenance);

    private sealed record ProvenanceDocument(
        [property: JsonPropertyName("authorityKind")] string AuthorityKind,
        [property: JsonPropertyName("recordedOn")] string RecordedOn,
        [property: JsonPropertyName("recordRef")] string RecordRef,
        [property: JsonPropertyName("rationale")] string Rationale);
}
