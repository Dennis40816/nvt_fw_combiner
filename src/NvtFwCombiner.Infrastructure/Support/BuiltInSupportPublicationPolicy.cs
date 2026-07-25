using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Infrastructure.Support;

/// <summary>Loads the owner publication policy after its bytes match the reviewed hash.</summary>
internal static partial class BuiltInSupportPublicationPolicy
{
    private const string RelativePath =
        "docs/contracts/support-publication-policy-v1.json";
    private const string ExpectedSha256 =
        "2a51e5f01f39991f892bca9e54006b507b84311119f82ba7716ec940e1fb23b5";

    internal static SupportPublicationPolicySnapshot Load()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return Load(File.ReadAllBytes(path), ExpectedSha256);
    }

    internal static SupportPublicationPolicySnapshot Load(
        ReadOnlySpan<byte> bytes,
        string expectedSha256)
    {
        PolicyDocument document = PinnedJsonCatalogLoader.Load(
            bytes,
            expectedSha256,
            "Built-in support publication policy",
            "Built-in support publication policy has an invalid empty document.",
            SupportPublicationPolicyJsonContext.Default.PolicyDocument);
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0") ||
            !StringComparer.Ordinal.Equals(
                document.PolicyId,
                "support-publication-policy") ||
            !IsIsoDate(document.IssuedOn) ||
            !IsSemanticVersion(document.PolicyVersion) ||
            (document.SupersedesPolicyVersion is not null &&
                !IsSemanticVersion(document.SupersedesPolicyVersion)) ||
            document.Decisions is null ||
            document.Decisions.Count == 0)
        {
            throw Invalid(
                "schemaVersion, policyId, issuedOn, policyVersion, supersedesPolicyVersion, or decisions");
        }

        var snapshot = new SupportPublicationPolicySnapshot(
            document.PolicyId,
            document.PolicyVersion,
            PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes),
            document.Decisions.Select(CreateDecision),
            document.SupersedesPolicyVersion);
        try
        {
            SupportPublicationPolicyValidator.Validate(snapshot);
            return snapshot;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "Built-in support publication policy violates snapshot invariants.",
                exception);
        }
    }

    private static SupportPublicationDecision CreateDecision(DecisionDocument source)
    {
        ProvenanceDocument provenance =
            source.Provenance ?? throw Invalid("decision provenance");
        return !IsIsoDate(provenance.RecordedOn)
            ? throw Invalid("decision provenance date")
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
            source.SupersedesDecisionIds ?? []);
    }

    private static bool IsIsoDate(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            DateOnly.TryParseExact(
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

    private static InvalidDataException Invalid(string field)
    {
        return new InvalidDataException(
            $"Built-in support publication policy has invalid {field}.");
    }
}

/// <summary>Generated metadata for the hash-pinned publication policy.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PolicyDocument))]
internal sealed partial class SupportPublicationPolicyJsonContext :
    JsonSerializerContext
{
}

internal sealed record PolicyDocument(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("policyId")] string PolicyId,
    [property: JsonPropertyName("policyVersion")] string PolicyVersion,
    [property: JsonPropertyName("issuedOn")] string IssuedOn,
    [property: JsonPropertyName("supersedesPolicyVersion")]
    string? SupersedesPolicyVersion,
    [property: JsonPropertyName("decisions")]
    IReadOnlyList<DecisionDocument>? Decisions);

internal sealed record DecisionDocument(
    [property: JsonPropertyName("decisionId")] string DecisionId,
    [property: JsonPropertyName("routeId")] string RouteId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("supersedesDecisionIds")]
    IReadOnlyList<string>? SupersedesDecisionIds,
    [property: JsonPropertyName("provenance")] ProvenanceDocument? Provenance);

internal sealed record ProvenanceDocument(
    [property: JsonPropertyName("authorityKind")] string AuthorityKind,
    [property: JsonPropertyName("recordedOn")] string RecordedOn,
    [property: JsonPropertyName("recordRef")] string RecordRef,
    [property: JsonPropertyName("rationale")] string Rationale);
