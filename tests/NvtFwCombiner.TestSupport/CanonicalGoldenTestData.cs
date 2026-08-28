using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.TestSupport;

/// <summary>One fact-scoped alias resolved against its direct source case.</summary>
public sealed record CanonicalGoldenAlias(string CaseId, string Ic, string SourceCaseId, string SourceIc);

/// <summary>Closed test behavior attached to one canonical evidence case.</summary>
public enum CanonicalGoldenTestDispositionKind
{
    /// <summary>Compares the complete built output with the owner expected artifact.</summary>
    DirectFullOutput,
    /// <summary>Allows only case-local reviewed output byte ranges to differ.</summary>
    AllowedByteDifference,
    /// <summary>Validates artifacts and records independent typed route-blocking evidence.</summary>
    ArtifactIntegrityRouteBlocked,
    /// <summary>Validates immutable input evidence without claiming an expected output.</summary>
    InputOnlyEvidence,
    /// <summary>Tests a declared fact binding without copying physical payloads.</summary>
    FactScopedAlias,
}

/// <summary>Typed projection of a canonical case's fail-closed test disposition.</summary>
public sealed record CanonicalGoldenTestDisposition(
    CanonicalGoldenTestDispositionKind Kind,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<CanonicalGoldenDifferenceRange> AllowedDifferenceRanges,
    IReadOnlyList<string> RouteBlockingEvidenceRefs);

/// <summary>One reviewed half-open output-image range in an allowed-difference contract.</summary>
public sealed record CanonicalGoldenDifferenceRange(
    long Start,
    long EndExclusive,
    string Classification)
{
    /// <summary>Gets the range length.</summary>
    public long Length => checked(EndExclusive - Start);

    /// <summary>Returns whether one output-image offset is inside this range.</summary>
    public bool Contains(long offset)
    {
        return offset >= Start && offset < EndExclusive;
    }
}

/// <summary>Result of comparing complete output images under one case-local contract.</summary>
public sealed record CanonicalGoldenDifferenceResult(
    long DifferenceCount,
    IReadOnlyList<long> DifferenceCountByAllowedRange);

/// <summary>Projects direct cases from the closed canonical golden inventory for test execution.</summary>
public static class CanonicalGoldenTestData
{
    /// <summary>Gets the canonical inventory root.</summary>
    public static string Root => RepositoryPaths.FromRepositoryRoot("testdata", "golden", "canonical");

    /// <summary>
    /// Loads direct cases for one workflow into the established test-only case shape.
    /// Alias manifests are evidence records and never masquerade as executable direct goldens.
    /// </summary>
    public static JsonDocument LoadDirectWorkflowManifest(string workflow)
    {
        return LoadDirectWorkflowManifest(workflow, Root);
    }

    /// <summary>Loads direct cases from an explicit canonical root for focused contract tests.</summary>
    public static JsonDocument LoadDirectWorkflowManifest(string workflow, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var cases = new JsonArray();
        foreach (JsonElement entry in inventory.RootElement.GetProperty("cases").EnumerateArray())
        {
            string manifestPath = entry.GetProperty("manifestPath").GetString()!;
            using var document = JsonDocument.Parse(
                File.ReadAllText(RepositoryPaths.PathFromRelative(root, manifestPath)));
            JsonElement goldenCase = document.RootElement;
            if (!goldenCase.GetProperty("directGolden").GetBoolean() ||
                !StringComparer.Ordinal.Equals(goldenCase.GetProperty("workflow").GetString(), workflow))
            {
                continue;
            }

            _ = RequireDisposition(goldenCase, CanonicalGoldenTestDispositionKind.DirectFullOutput);

            cases.Add(ProjectDirectCase(goldenCase, root));
        }

        return JsonDocument.Parse(new JsonObject { ["cases"] = cases }.ToJsonString());
    }

    /// <summary>Loads one raw direct canonical case after validating all physical artifacts.</summary>
    public static JsonElement LoadDirectCase(string workflow, string caseId)
    {
        return LoadPhysicalCase(workflow, caseId, Root, directEvidence: false);
    }

    /// <summary>Loads one direct case from an explicit canonical root for focused contract tests.</summary>
    public static JsonElement LoadDirectCase(string workflow, string caseId, string root)
    {
        return LoadPhysicalCase(workflow, caseId, root, directEvidence: false);
    }

    /// <summary>Loads one input-only direct evidence case after validating all physical artifacts.</summary>
    public static JsonElement LoadDirectEvidenceCase(string workflow, string caseId)
    {
        return LoadPhysicalCase(workflow, caseId, Root, directEvidence: true);
    }

    /// <summary>Loads one input-only direct evidence case from an explicit canonical root.</summary>
    public static JsonElement LoadDirectEvidenceCase(string workflow, string caseId, string root)
    {
        return LoadPhysicalCase(workflow, caseId, root, directEvidence: true);
    }

    /// <summary>Gets one validated physical artifact path from a raw canonical case.</summary>
    public static string ArtifactPath(JsonElement artifact)
    {
        return ValidatedArtifactPath(artifact);
    }

    private static JsonElement LoadPhysicalCase(
        string workflow,
        string caseId,
        string root,
        bool directEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        foreach (JsonElement entry in inventory.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (!StringComparer.Ordinal.Equals(entry.GetProperty("caseId").GetString(), caseId))
            {
                continue;
            }

            string manifestPath = entry.GetProperty("manifestPath").GetString()!;
            using var document = JsonDocument.Parse(
                File.ReadAllText(RepositoryPaths.PathFromRelative(root, manifestPath)));
            JsonElement goldenCase = document.RootElement;
            bool isDirectEvidence =
                goldenCase.TryGetProperty("directEvidence", out JsonElement evidence) &&
                evidence.ValueKind == JsonValueKind.True;
            if ((directEvidence ? !isDirectEvidence : !goldenCase.GetProperty("directGolden").GetBoolean()) ||
                !StringComparer.Ordinal.Equals(goldenCase.GetProperty("workflow").GetString(), workflow))
            {
                throw new InvalidDataException(
                    $"Canonical case '{caseId}' is not direct {workflow} " +
                    (directEvidence ? "input evidence." : "golden evidence."));
            }

            foreach (JsonElement artifact in goldenCase.GetProperty("artifacts").EnumerateArray())
            {
                _ = ValidatedArtifactPath(artifact, root);
            }

            _ = directEvidence
                ? RequireDisposition(goldenCase, CanonicalGoldenTestDispositionKind.InputOnlyEvidence)
                : TestDisposition(goldenCase);

            return goldenCase.Clone();
        }

        throw new InvalidDataException($"Canonical {workflow} case '{caseId}' was not found.");
    }

    /// <summary>Loads fact-scoped aliases for one workflow and resolves each direct source IC.</summary>
    public static IReadOnlyList<CanonicalGoldenAlias> LoadWorkflowAliases(string workflow)
    {
        return LoadWorkflowAliases(workflow, Root);
    }

    /// <summary>Loads aliases from an explicit canonical root for focused contract tests.</summary>
    public static IReadOnlyList<CanonicalGoldenAlias> LoadWorkflowAliases(string workflow, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var directCases = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliases = new List<(string CaseId, string Ic, string SourceCaseId)>();
        foreach (JsonElement entry in inventory.RootElement.GetProperty("cases").EnumerateArray())
        {
            string manifestPath = entry.GetProperty("manifestPath").GetString()!;
            using var document = JsonDocument.Parse(
                File.ReadAllText(RepositoryPaths.PathFromRelative(root, manifestPath)));
            JsonElement goldenCase = document.RootElement;
            if (!StringComparer.Ordinal.Equals(goldenCase.GetProperty("workflow").GetString(), workflow))
            {
                continue;
            }

            CanonicalGoldenTestDisposition disposition = TestDisposition(goldenCase);

            string caseId = goldenCase.GetProperty("caseId").GetString()!;
            string ic = goldenCase.GetProperty("ic").GetString()!;
            if (goldenCase.GetProperty("directGolden").GetBoolean() ||
                (goldenCase.TryGetProperty("directEvidence", out JsonElement directEvidence) &&
                    directEvidence.ValueKind == JsonValueKind.True))
            {
                directCases.Add(caseId, ic);
            }
            else
            {
                if (disposition.Kind != CanonicalGoldenTestDispositionKind.FactScopedAlias)
                {
                    throw new InvalidDataException(
                        $"Canonical alias '{caseId}' must use the fact-scoped-alias disposition.");
                }

                aliases.Add((caseId, ic, goldenCase.GetProperty("alias").GetProperty("sourceCaseId").GetString()!));
            }
        }

        return
        [
            .. aliases.Select(alias => new CanonicalGoldenAlias(
                alias.CaseId,
                alias.Ic,
                alias.SourceCaseId,
                directCases.TryGetValue(alias.SourceCaseId, out string? sourceIc)
                    ? sourceIc
                    : throw new InvalidDataException(
                        $"Canonical alias '{alias.CaseId}' has no direct source '{alias.SourceCaseId}'."))),
        ];
    }

    /// <summary>Finds one named artifact in a raw canonical case.</summary>
    public static JsonElement Artifact(JsonElement goldenCase, string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        return goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(artifact => StringComparer.Ordinal.Equals(
                artifact.GetProperty("artifactId").GetString(),
                artifactId));
    }

    /// <summary>Parses the closed disposition vocabulary; unknown or incomplete values fail.</summary>
    public static CanonicalGoldenTestDisposition TestDisposition(JsonElement goldenCase)
    {
        JsonElement disposition = goldenCase.GetProperty("testDisposition");
        CanonicalGoldenTestDispositionKind kind = disposition.GetProperty("kind").GetString() switch
        {
            "direct-full-output" => CanonicalGoldenTestDispositionKind.DirectFullOutput,
            "allowed-byte-difference" => CanonicalGoldenTestDispositionKind.AllowedByteDifference,
            "artifact-integrity-route-blocked" => CanonicalGoldenTestDispositionKind.ArtifactIntegrityRouteBlocked,
            "input-only-evidence" => CanonicalGoldenTestDispositionKind.InputOnlyEvidence,
            "fact-scoped-alias" => CanonicalGoldenTestDispositionKind.FactScopedAlias,
            string value => throw new InvalidDataException(
                $"Canonical test disposition kind is unsupported: {value}"),
            null => throw new InvalidDataException("Canonical test disposition kind is required."),
        };
        string[] evidenceRefs =
        [
            .. disposition.GetProperty("evidenceRefs")
                .EnumerateArray()
                .Select(reference => reference.GetString() ?? throw new InvalidDataException(
                    "Canonical test disposition evidence reference is required.")),
        ];
        if (evidenceRefs.Length == 0)
        {
            throw new InvalidDataException("Canonical test disposition requires evidence references.");
        }

        string? differenceContractProperty = disposition.TryGetProperty(
            "differenceContractProperty",
            out JsonElement contractProperty)
            ? contractProperty.GetString()
            : null;
        IReadOnlyList<CanonicalGoldenDifferenceRange> allowedDifferenceRanges =
            kind == CanonicalGoldenTestDispositionKind.AllowedByteDifference
                ? ParseAllowedDifferenceRanges(goldenCase, differenceContractProperty)
                : [];
        string[] routeBlockingEvidenceRefs = disposition.TryGetProperty(
            "routeBlockingEvidenceRefs",
            out JsonElement routeRefs)
            ? [.. routeRefs.EnumerateArray().Select(reference => reference.GetString()!)]
            : [];
        return new CanonicalGoldenTestDisposition(
            kind,
            evidenceRefs,
            allowedDifferenceRanges,
            routeBlockingEvidenceRefs);
    }

    /// <summary>Fails unless the case declares the expected closed disposition kind.</summary>
    public static CanonicalGoldenTestDisposition RequireDisposition(
        JsonElement goldenCase,
        CanonicalGoldenTestDispositionKind expectedKind)
    {
        CanonicalGoldenTestDisposition disposition = TestDisposition(goldenCase);
        return disposition.Kind == expectedKind
            ? disposition
            : throw new InvalidDataException(
                $"Canonical case '{goldenCase.GetProperty("caseId").GetString()}' uses " +
                $"'{disposition.Kind}' but '{expectedKind}' is required by this runner.");
    }

    /// <summary>
    /// Compares complete output images and rejects every byte difference outside the
    /// case-local reviewed output-image ranges.
    /// </summary>
    public static CanonicalGoldenDifferenceResult AssertAllowedByteDifferences(
        JsonElement goldenCase,
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual)
    {
        CanonicalGoldenTestDisposition disposition = RequireDisposition(
            goldenCase,
            CanonicalGoldenTestDispositionKind.AllowedByteDifference);
        JsonElement expectedArtifact = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(artifact => StringComparer.Ordinal.Equals(
                artifact.GetProperty("role").GetString(),
                "expected"));
        long declaredSize = expectedArtifact.GetProperty("size").GetInt64();
        if (expected.Length != declaredSize || actual.Length != declaredSize)
        {
            throw new InvalidDataException(
                $"Canonical case '{goldenCase.GetProperty("caseId").GetString()}' output length " +
                $"must match the declared expected artifact size {declaredSize}.");
        }

        long[] differenceCountByRange = new long[disposition.AllowedDifferenceRanges.Count];
        long differenceCount = 0;
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] == actual[index])
            {
                continue;
            }

            int containingRange = -1;
            for (int rangeIndex = 0; rangeIndex < disposition.AllowedDifferenceRanges.Count; rangeIndex++)
            {
                if (disposition.AllowedDifferenceRanges[rangeIndex].Contains(index))
                {
                    containingRange = rangeIndex;
                    break;
                }
            }

            if (containingRange < 0)
            {
                throw new InvalidDataException(
                    $"Canonical case '{goldenCase.GetProperty("caseId").GetString()}' has an " +
                    $"unapproved output difference at 0x{index:X}.");
            }

            differenceCount++;
            differenceCountByRange[containingRange]++;
        }

        return new CanonicalGoldenDifferenceResult(differenceCount, differenceCountByRange);
    }

    /// <summary>Finds a direct canonical artifact path by workflow, IC, artifact id, and optional variant.</summary>
    public static string ArtifactPath(
        string workflow,
        string ic,
        string artifactId,
        string? variantOrVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ic);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);

        using JsonDocument manifest = LoadDirectWorkflowManifest(workflow);
        string normalizedIc = ic.StartsWith("NT", StringComparison.Ordinal) ? ic[2..] : ic;
        JsonElement goldenCase = manifest.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item =>
                StringComparer.Ordinal.Equals(item.GetProperty("ic").GetString(), normalizedIc) &&
                (variantOrVersion is null ||
                    StringComparer.Ordinal.Equals(item.GetProperty("variant").GetString(), variantOrVersion)));
        JsonElement artifact = artifactId == "expected-output"
            ? goldenCase.GetProperty("expectedOutput")
            : goldenCase.GetProperty("inputs").GetProperty(artifactId);
        return ValidatedArtifactPath(artifact);
    }

    private static string ValidatedArtifactPath(JsonElement artifact)
    {
        return ValidatedArtifactPath(artifact, Root);
    }

    private static List<CanonicalGoldenDifferenceRange> ParseAllowedDifferenceRanges(
        JsonElement goldenCase,
        string? differenceContractProperty)
    {
        if (string.IsNullOrWhiteSpace(differenceContractProperty) ||
            !goldenCase.TryGetProperty(differenceContractProperty, out JsonElement contract))
        {
            throw new InvalidDataException(
                "Allowed-byte-difference disposition requires its case-local difference contract.");
        }

        if (!StringComparer.Ordinal.Equals(
                contract.GetProperty("addressSpaceId").GetString(),
                "output-image"))
        {
            throw new InvalidDataException(
                "Allowed byte differences must name the output-image address space.");
        }

        long outputSize = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(artifact => StringComparer.Ordinal.Equals(
                artifact.GetProperty("role").GetString(),
                "expected"))
            .GetProperty("size")
            .GetInt64();
        var ranges = new List<CanonicalGoldenDifferenceRange>();
        long previousEnd = 0;
        foreach (JsonElement range in contract.GetProperty("allowedDifferenceRanges").EnumerateArray())
        {
            long start = ParseHexOffset(range.GetProperty("start").GetString(), "start");
            long endExclusive = ParseHexOffset(
                range.GetProperty("endExclusive").GetString(),
                "endExclusive");
            string classification = range.GetProperty("classification").GetString()
                ?? throw new InvalidDataException("Allowed difference classification is required.");
            if (start < previousEnd || endExclusive <= start || endExclusive > outputSize)
            {
                throw new InvalidDataException(
                    "Allowed difference ranges must be sorted, non-overlapping, non-empty, and bounded by the expected output.");
            }

            ranges.Add(new CanonicalGoldenDifferenceRange(start, endExclusive, classification));
            previousEnd = endExclusive;
        }

        return ranges.Count > 0
            ? ranges
            : throw new InvalidDataException(
                "Allowed-byte-difference contract requires at least one range.");
    }

    private static long ParseHexOffset(string? value, string propertyName)
    {
        long result = 0;
        bool isValid = value is not null && value.StartsWith("0x", StringComparison.Ordinal) &&
            long.TryParse(
                value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out result);
        return isValid
            ? result
            : throw new InvalidDataException(
                $"Allowed difference {propertyName} must be a non-negative hexadecimal offset.");
    }

    private static string ValidatedArtifactPath(JsonElement artifact, string root)
    {
        string path = RepositoryPaths.ManifestPath(root, artifact);
        var file = new FileInfo(path);
        if (file.Length != artifact.GetProperty("size").GetInt64())
        {
            throw new InvalidDataException($"Canonical artifact size drift: {artifact.GetProperty("path").GetString()}.");
        }

        using FileStream stream = File.OpenRead(path);
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));
        return !StringComparer.Ordinal.Equals(actualHash, artifact.GetProperty("sha256").GetString())
            ? throw new InvalidDataException($"Canonical artifact SHA-256 drift: {artifact.GetProperty("path").GetString()}.")
            : path;
    }

    private static JsonObject ProjectDirectCase(JsonElement goldenCase, string root)
    {
        var inputs = new JsonObject();
        JsonObject? expected = null;
        foreach (JsonElement artifact in goldenCase.GetProperty("artifacts").EnumerateArray())
        {
            string role = artifact.GetProperty("role").GetString()!;
            JsonObject projection = ProjectArtifact(artifact, root);
            if (StringComparer.Ordinal.Equals(role, "input"))
            {
                inputs.Add(artifact.GetProperty("artifactId").GetString()!, projection);
            }
            else if (StringComparer.Ordinal.Equals(role, "expected"))
            {
                if (expected is not null)
                {
                    throw new InvalidDataException($"Canonical case '{goldenCase.GetProperty("caseId").GetString()}' has multiple expected artifacts.");
                }

                expected = projection;
            }
        }

        return new JsonObject
        {
            ["caseId"] = goldenCase.GetProperty("caseId").GetString(),
            ["ic"] = goldenCase.GetProperty("ic").GetString()![2..],
            ["profileId"] = goldenCase.GetProperty("profileId").GetString(),
            ["variant"] = goldenCase.GetProperty("variantOrVersion").GetString(),
            ["inputs"] = inputs,
            ["expectedOutput"] = expected ?? throw new InvalidDataException(
                $"Canonical case '{goldenCase.GetProperty("caseId").GetString()}' has no expected artifact."),
        };
    }

    private static JsonObject ProjectArtifact(JsonElement artifact, string root)
    {
        _ = ValidatedArtifactPath(artifact, root);
        return new JsonObject
        {
            ["path"] = artifact.GetProperty("path").GetString(),
            ["size"] = artifact.GetProperty("size").GetInt64(),
            ["sha256"] = artifact.GetProperty("sha256").GetString(),
        };
    }
}
