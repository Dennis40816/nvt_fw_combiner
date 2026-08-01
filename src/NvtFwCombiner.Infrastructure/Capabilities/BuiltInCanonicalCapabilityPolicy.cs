using System.Globalization;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Infrastructure.Capabilities;

/// <summary>Loads the canonical capability decisions after exact source verification.</summary>
internal static class BuiltInCanonicalCapabilityPolicy
{
    internal const string RelativePath =
        "docs/contracts/canonical-capability-policy-v1.json";
    internal const string ExpectedSha256 =
        "15e0530858d93c7eccd564bb5e12f6f37ba87751097f1a7765e74b2d6dc92f8a";

    internal static CanonicalCapabilityPolicySnapshot Load()
    {
        return Load(
            File.ReadAllBytes(Path.Combine(
                AppContext.BaseDirectory,
                RelativePath.Replace('/', Path.DirectorySeparatorChar))),
            ExpectedSha256);
    }

    internal static CanonicalCapabilityPolicySnapshot Load(
        ReadOnlySpan<byte> bytes,
        string expectedSha256)
    {
        CanonicalCapabilityPolicyDocument document =
            PinnedJsonCatalogLoader.LoadExact(
                bytes,
                expectedSha256,
                "Built-in canonical capability policy",
                "Built-in canonical capability policy has an invalid empty document.",
                CanonicalCapabilityPolicyJsonContext.Default
                    .CanonicalCapabilityPolicyDocument);
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0") ||
            !StringComparer.Ordinal.Equals(
                document.CatalogId,
                "canonical-capability-policy") ||
            !StringComparer.Ordinal.Equals(document.CatalogVersion, "1.4.0") ||
            !IsIsoDate(document.IssuedOn) ||
            document.Routes is null ||
            document.Routes.Count == 0)
        {
            throw Invalid(
                "schemaVersion, catalogId, catalogVersion, issuedOn, or routes");
        }

        CanonicalCapabilityPolicyRoute[] routes =
        [
            .. document.Routes.Select(CreateRoute),
        ];
        return routes.Select(static route => route.Identity.RouteId)
            .Distinct(StringComparer.Ordinal).Count() != routes.Length
            ? throw Invalid("duplicate routeId")
            : new CanonicalCapabilityPolicySnapshot(
                document.CatalogId,
                document.CatalogVersion,
                PinnedJsonCatalogLoader.ComputeSha256(bytes),
                Array.AsReadOnly(routes));
    }

    private static CanonicalCapabilityPolicyRoute CreateRoute(
        CanonicalCapabilityRouteDocument source)
    {
        var identity = new CapabilityRouteIdentity(
            source.IcId,
            source.WorkflowId,
            source.IcCountVariant,
            source.MapVariant);
        if (!StringComparer.Ordinal.Equals(source.RouteId, identity.RouteId))
        {
            throw Invalid("routeId");
        }

        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring =
            CreateDecision(
                source.Authoring,
                source.RouteId,
                source.CapabilityFingerprint,
                static value => value switch
                {
                    "available" => CapabilityAuthoringAvailability.Available,
                    "unavailable" => CapabilityAuthoringAvailability.Unavailable,
                    _ => throw Invalid("authoring value"),
                },
                "authoring");
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication =
            CreateDecision(
                source.Publication,
                source.RouteId,
                source.CapabilityFingerprint,
                static value => value switch
                {
                    "unclassified" => CapabilityPublicationStatus.Unclassified,
                    "supported" => CapabilityPublicationStatus.Supported,
                    "candidate" => CapabilityPublicationStatus.Candidate,
                    "internal" => CapabilityPublicationStatus.Internal,
                    "test-only" => CapabilityPublicationStatus.TestOnly,
                    _ => throw Invalid("publication value"),
                },
                "publication");
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence =
            CreateDecision(
                source.Evidence,
                source.RouteId,
                source.CapabilityFingerprint,
                static value => value switch
                {
                    "direct-golden" => CapabilityEvidenceStatus.DirectGolden,
                    "approved-alias" => CapabilityEvidenceStatus.ApprovedAlias,
                    "synthetic-oracle" => CapabilityEvidenceStatus.SyntheticOracle,
                    "contract-only" => CapabilityEvidenceStatus.ContractOnly,
                    "missing" => CapabilityEvidenceStatus.Missing,
                    _ => throw Invalid("evidence value"),
                },
                "evidence");
        return new CanonicalCapabilityPolicyRoute(
            identity,
            source.CapabilityFingerprint,
            authoring,
            publication,
            evidence);
    }

    private static PinnedCapabilityDecision<TValue> CreateDecision<TValue>(
        CanonicalCapabilityDecisionDocument? source,
        string routeId,
        string capabilityFingerprint,
        Func<string, TValue> parse,
        string label)
        where TValue : struct, Enum
    {
        CanonicalCapabilityDecisionDocument pinned =
            source is not null &&
            StringComparer.Ordinal.Equals(source.RouteId, routeId) &&
            StringComparer.Ordinal.Equals(
                source.CapabilityFingerprint,
                capabilityFingerprint)
                ? source
                : throw Invalid($"{label} pin");

        return new PinnedCapabilityDecision<TValue>(
            pinned.DecisionId,
            pinned.RouteId,
            pinned.CapabilityFingerprint,
            parse(pinned.Value),
            pinned.SourceReference);
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

    private static InvalidDataException Invalid(string field)
    {
        return new InvalidDataException(
            $"Built-in canonical capability policy has invalid {field}.");
    }
}

/// <summary>Typed policy facts ready to be joined with compiler output.</summary>
internal sealed record CanonicalCapabilityPolicySnapshot(
    string CatalogId,
    string CatalogVersion,
    string SourceSha256,
    IReadOnlyList<CanonicalCapabilityPolicyRoute> Routes);

/// <summary>One exact-route policy row without duplicated firmware execution facts.</summary>
internal sealed record CanonicalCapabilityPolicyRoute(
    CapabilityRouteIdentity Identity,
    string CapabilityFingerprint,
    PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring,
    PinnedCapabilityDecision<CapabilityPublicationStatus> Publication,
    PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence);

/// <summary>Generated metadata for the exact hash-pinned capability policy.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(CanonicalCapabilityPolicyDocument))]
internal sealed partial class CanonicalCapabilityPolicyJsonContext :
    JsonSerializerContext
{
}

internal sealed record CanonicalCapabilityPolicyDocument(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("catalogId")] string CatalogId,
    [property: JsonPropertyName("catalogVersion")] string CatalogVersion,
    [property: JsonPropertyName("issuedOn")] string IssuedOn,
    [property: JsonPropertyName("routes")]
    IReadOnlyList<CanonicalCapabilityRouteDocument>? Routes);

internal sealed record CanonicalCapabilityRouteDocument(
    [property: JsonPropertyName("icId")] string IcId,
    [property: JsonPropertyName("workflowId")] string WorkflowId,
    [property: JsonPropertyName("icCountVariant")] string IcCountVariant,
    [property: JsonPropertyName("mapVariant")] string MapVariant,
    [property: JsonPropertyName("routeId")] string RouteId,
    [property: JsonPropertyName("capabilityFingerprint")]
    string CapabilityFingerprint,
    [property: JsonPropertyName("authoring")]
    CanonicalCapabilityDecisionDocument? Authoring,
    [property: JsonPropertyName("publication")]
    CanonicalCapabilityDecisionDocument? Publication,
    [property: JsonPropertyName("evidence")]
    CanonicalCapabilityDecisionDocument? Evidence);

internal sealed record CanonicalCapabilityDecisionDocument(
    [property: JsonPropertyName("decisionId")] string DecisionId,
    [property: JsonPropertyName("routeId")] string RouteId,
    [property: JsonPropertyName("capabilityFingerprint")]
    string CapabilityFingerprint,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("sourceReference")] string SourceReference);
