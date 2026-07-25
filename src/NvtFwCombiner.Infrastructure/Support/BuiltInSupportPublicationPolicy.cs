using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Infrastructure.Support;

/// <summary>Loads the owner-approved publication policy only after its shipped bytes match the reviewed hash.</summary>
internal static partial class BuiltInSupportPublicationPolicy
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
        PolicyDocument document = PinnedJsonCatalogLoader.Load(
            bytes,
            expectedSha256,
            "Built-in support publication policy",
            "Built-in support publication policy has an invalid empty document.",
            SupportPublicationPolicyJsonContext.Default.PolicyDocument);
        IReadOnlyList<DecisionDocument> decisions = document.Decisions ??
            throw Invalid("decisions");
        return !StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0") ||
            !StringComparer.Ordinal.Equals(document.PolicyId, "support-publication-policy") ||
            !IsIsoDate(document.IssuedOn) ||
            !IsSemanticVersion(document.PolicyVersion) ||
            (document.SupersedesPolicyVersion is not null && !IsSemanticVersion(document.SupersedesPolicyVersion))
            ? throw Invalid("schemaVersion, policyId, issuedOn, policyVersion, or supersedesPolicyVersion")
            : new SupportPublicationPolicySnapshot(
                document.PolicyId,
                document.PolicyVersion,
                PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes),
                decisions.Select(CreateDecision),
                document.SupersedesPolicyVersion);
    }

    private static SupportPublicationDecision CreateDecision(DecisionDocument source)
    {
        ProvenanceDocument provenance = source.Provenance ?? throw Invalid("decision provenance");
        IReadOnlyList<string> supersedesDecisionIds = source.SupersedesDecisionIds ?? [];
        return !IsIsoDate(provenance.RecordedOn) ||
            !ContractIdPattern().IsMatch(source.DecisionId) ||
            !ContractIdPattern().IsMatch(source.RouteId) ||
            supersedesDecisionIds.Any(static decisionId =>
                string.IsNullOrWhiteSpace(decisionId) || !ContractIdPattern().IsMatch(decisionId)) ||
            supersedesDecisionIds.Contains(source.DecisionId, StringComparer.Ordinal)
            ? throw Invalid("decision identity, supersession, or provenance")
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
                    provenance.Rationale),
                supersedesDecisionIds);
    }

    private static bool IsIsoDate(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static bool IsSemanticVersion(string? value)
    {
        return value is not null && SemanticVersionPattern().IsMatch(value);
    }

    [GeneratedRegex(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ContractIdPattern();

    private static InvalidDataException Invalid(string field)
    {
        return new InvalidDataException($"Built-in support publication policy has invalid {field}.");
    }

}

/// <summary>Strict source-generated JSON metadata for the hash-pinned support publication policy.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PolicyDocument))]
internal sealed partial class SupportPublicationPolicyJsonContext : JsonSerializerContext
{
}

internal sealed record PolicyDocument(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("policyId")] string PolicyId,
    [property: JsonPropertyName("policyVersion")] string PolicyVersion,
    [property: JsonPropertyName("issuedOn")] string IssuedOn,
    [property: JsonPropertyName("supersedesPolicyVersion")] string? SupersedesPolicyVersion,
    [property: JsonPropertyName("decisions")] IReadOnlyList<DecisionDocument>? Decisions);

internal sealed record DecisionDocument(
    [property: JsonPropertyName("decisionId")] string DecisionId,
    [property: JsonPropertyName("routeId")] string RouteId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("supersedesDecisionIds")] IReadOnlyList<string>? SupersedesDecisionIds,
    [property: JsonPropertyName("provenance")] ProvenanceDocument? Provenance);

internal sealed record ProvenanceDocument(
    [property: JsonPropertyName("authorityKind")] string AuthorityKind,
    [property: JsonPropertyName("recordedOn")] string RecordedOn,
    [property: JsonPropertyName("recordRef")] string RecordRef,
    [property: JsonPropertyName("rationale")] string Rationale);
