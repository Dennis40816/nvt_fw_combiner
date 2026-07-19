using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NvtFwCombiner.TestSupport;

/// <summary>One fact-scoped alias resolved against its direct source case.</summary>
public sealed record CanonicalGoldenAlias(string CaseId, string Ic, string SourceCaseId, string SourceIc);

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
