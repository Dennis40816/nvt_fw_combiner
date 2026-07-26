using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Infrastructure.Support;

/// <summary>Loads the owner publication policy after its bytes match the reviewed hash.</summary>
internal static partial class BuiltInSupportPublicationPolicy
{
    private const string ExpectedSha256 =
        "eeffb9be1afba4bc834b17fea63f08d628e170847cc4d0e5f50cdd2f39e9009b";
    internal static IReadOnlyList<PinnedSupportPublicationPolicyFile>
        HistoryFiles
    { get; } =
        Array.AsReadOnly<PinnedSupportPublicationPolicyFile>(
        [
            new(
                "docs/contracts/support-publication-policy-v1.json",
                ExpectedSha256),
        ]);

    internal static LoadedSupportPublicationPolicy Load()
    {
        return LoadFromDirectory(AppContext.BaseDirectory, HistoryFiles);
    }

    internal static SupportPublicationPolicySnapshot Load(
        ReadOnlySpan<byte> bytes,
        string expectedSha256)
    {
        return LoadHistory(
        [
            new PinnedSupportPublicationPolicyDocument(
                bytes.ToArray(),
                expectedSha256),
        ]).Current;
    }

    internal static LoadedSupportPublicationPolicy LoadFromDirectory(
        string baseDirectory,
        IReadOnlyList<PinnedSupportPublicationPolicyFile> history)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(history);
        PinnedSupportPublicationPolicyDocument[] documents =
        [
            .. history.Select(source =>
                new PinnedSupportPublicationPolicyDocument(
                    File.ReadAllBytes(ResolveHistoryPath(
                        baseDirectory,
                        source.RelativePath)),
                    source.ExpectedSha256)),
        ];
        return LoadHistory(documents);
    }

    internal static LoadedSupportPublicationPolicy LoadHistory(
        IReadOnlyList<PinnedSupportPublicationPolicyDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0)
        {
            throw new ArgumentException(
                "At least one pinned publication policy document is required.",
                nameof(documents));
        }

        var snapshots =
            new List<SupportPublicationPolicySnapshot>(documents.Count);
        foreach (PinnedSupportPublicationPolicyDocument source in documents)
        {
            snapshots.Add(Parse(
                source.Bytes.Span,
                source.ExpectedSha256));
        }

        try
        {
            SupportPublicationPolicyValidator.ValidateHistory(snapshots);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "Built-in support publication policy violates snapshot invariants.",
                exception);
        }

        return new LoadedSupportPublicationPolicy(
            snapshots[^1],
            snapshots.Count > 1 ? snapshots[^2] : null);
    }

    private static SupportPublicationPolicySnapshot Parse(
        ReadOnlySpan<byte> bytes,
        string expectedSha256)
    {
        PolicyDocument document = PinnedJsonCatalogLoader.Load(
            bytes,
            expectedSha256,
            "Built-in support publication policy",
            "Built-in support publication policy has an invalid empty document.",
            SupportPublicationPolicyJsonContext.Default.PolicyDocument);
        bool isInvalid =
            !StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0") ||
            !StringComparer.Ordinal.Equals(
                document.PolicyId,
                "support-publication-policy") ||
            !IsIsoDate(document.IssuedOn) ||
            !IsSemanticVersion(document.PolicyVersion) ||
            (document.SupersedesPolicyVersion is not null &&
                !IsSemanticVersion(document.SupersedesPolicyVersion)) ||
            ((document.SupersedesPolicyVersion is null) !=
                (document.SupersedesPolicySha256 is null)) ||
            document.Decisions is null ||
            document.Decisions.Count == 0;
        return isInvalid
            ? throw Invalid(
                "schemaVersion, policyId, issuedOn, policyVersion, supersession identity, or decisions")
            : new SupportPublicationPolicySnapshot(
            document.PolicyId,
            document.PolicyVersion,
            PinnedJsonCatalogLoader.ComputeCanonicalSha256(bytes),
            document.Decisions!.Select(CreateDecision),
            document.SupersedesPolicyVersion,
            document.SupersedesPolicySha256);
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

    private static string ResolveHistoryPath(
        string baseDirectory,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string root = Path.GetFullPath(baseDirectory);
        string path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, path);
        bool escapesRoot =
            Path.IsPathRooted(relative) ||
            StringComparer.Ordinal.Equals(relative, "..") ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
        return escapesRoot
            ? throw new InvalidDataException(
                "Publication policy history path must stay inside its deployment root.")
            : path;
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
    [property: JsonPropertyName("supersedesPolicySha256")]
    string? SupersedesPolicySha256,
    [property: JsonPropertyName("decisions")]
    IReadOnlyList<DecisionDocument>? Decisions);

internal sealed record PinnedSupportPublicationPolicyDocument(
    ReadOnlyMemory<byte> Bytes,
    string ExpectedSha256);

internal sealed record LoadedSupportPublicationPolicy(
    SupportPublicationPolicySnapshot Current,
    SupportPublicationPolicySnapshot? SupersededPolicy);

internal sealed record PinnedSupportPublicationPolicyFile(
    string RelativePath,
    string ExpectedSha256);

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
