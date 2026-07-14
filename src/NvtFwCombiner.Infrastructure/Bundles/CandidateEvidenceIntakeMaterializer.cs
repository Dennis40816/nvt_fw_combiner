using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Creates one closed candidate-only evidence set from a declared intake request.</summary>
internal static class CandidateEvidenceIntakeMaterializer
{
    private const int MaximumJsonBytes = 262144;
    private const int MaximumJsonDepth = 64;
    private const int MaximumArtifactBytes = 16777216;
    private const int MaximumAggregateArtifactBytes = 67108864;
    private const int MaximumDirectoryCount = 64;
    private const string CandidateRootDirectoryName = "candidate-root";
    private const string RootManifestFileName = "candidate-root-manifest.json";
    private const string ValidationReportFileName = "candidate-validation-report.json";
    private const string SourceBundleEntryId = "candidate-source-bundle";
    private const string SchemaEntryId = "candidate-schema";
    private const string ChecklistEntryId = "candidate-checklist";

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
    };

    internal static CandidateEvidenceMaterializationResult Materialize(
        CandidateEvidenceMaterializationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string sourceRoot = ResolveLocalExistingRoot(request.SourceRoot, nameof(request.SourceRoot));
        string outputDirectory = ResolveCandidateOutputDirectory(request.OutputDirectory, sourceRoot);
        CandidateEvidenceFileSnapshot requestSnapshot = ReadRequest(request.RequestPath);
        JsonObject intakeRequest = ParseIntakeRequest(requestSnapshot);
        CandidateEvidenceSourceArtifact[] sourceArtifacts = CaptureSourceArtifacts(intakeRequest, sourceRoot);

        string candidateSetParent = Path.GetDirectoryName(outputDirectory) ?? throw new ArgumentException(
            "Candidate output directory must have a parent directory.",
            nameof(request));
        string temporarySet = Path.Combine(
            candidateSetParent,
            $".{Path.GetFileName(outputDirectory)}-{Guid.NewGuid():N}");

        try
        {
            _ = Directory.CreateDirectory(temporarySet);
            string candidateRoot = Path.Combine(temporarySet, CandidateRootDirectoryName);
            _ = Directory.CreateDirectory(candidateRoot);

            JsonObject sourceBundle = CreateSourceBundle(intakeRequest, requestSnapshot.Sha256, sourceArtifacts, request.GeneratedAtUtc);
            CandidateEvidenceRootEntry[] entries = WriteCandidateRoot(candidateRoot, sourceBundle, sourceArtifacts);
            JsonObject rootManifest = CreateRootManifest(intakeRequest, entries);
            CandidateEvidenceFileSnapshot rootManifestSnapshot = WriteJson(
                Path.Combine(candidateRoot, RootManifestFileName),
                rootManifest);
            CandidateEvidenceSchemaValidator.ValidateDocument(
                rootManifestSnapshot.Content,
                RootManifestFileName,
                MaximumJsonBytes,
                MaximumJsonDepth);

            ClosedContentRootInventoryVerifier.VerifyClosedInventory(
                candidateRoot,
                RootManifestFileName,
                [.. entries.Select(static entry => entry.Path)],
                MaximumDirectoryCount);

            JsonObject validationReport = CreateValidationReport(
                intakeRequest,
                requestSnapshot.Sha256,
                rootManifestSnapshot.Sha256,
                entries,
                rootManifest);
            CandidateEvidenceFileSnapshot reportSnapshot = WriteJson(
                Path.Combine(temporarySet, ValidationReportFileName),
                validationReport);
            CandidateEvidenceSchemaValidator.ValidateDocument(
                reportSnapshot.Content,
                ValidationReportFileName,
                MaximumJsonBytes,
                MaximumJsonDepth);

            Directory.Move(temporarySet, outputDirectory);
            return new CandidateEvidenceMaterializationResult(
                Path.Combine(outputDirectory, CandidateRootDirectoryName),
                Path.Combine(outputDirectory, ValidationReportFileName),
                StringValue(rootManifest, "contentHash"));
        }
        catch (Exception exception)
        {
            try
            {
                if (Directory.Exists(temporarySet))
                {
                    Directory.Delete(temporarySet, recursive: true);
                }
            }
            catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    "Candidate evidence staging cleanup failed.",
                    new AggregateException(exception, cleanupException));
            }

            throw;
        }
    }

    private static string ResolveLocalExistingRoot(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        RejectNetworkPath(fullPath, parameterName);
        return FileSystemPathGuard.ResolveExistingRoot(fullPath);
    }

    private static string ResolveCandidateOutputDirectory(string outputDirectory, string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        string fullPath = Path.GetFullPath(outputDirectory);
        RejectNetworkPath(fullPath, nameof(outputDirectory));
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            throw new IOException("Candidate output directory must not already exist.");
        }

        string parent = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException(
            "Candidate output directory must have a parent directory.",
            nameof(outputDirectory));
        _ = ResolveLocalExistingRoot(parent, nameof(outputDirectory));
        return IsSameOrDescendant(fullPath, sourceRoot) || IsSameOrDescendant(sourceRoot, fullPath)
            ? throw new ArgumentException(
                "Candidate output directory must not contain or be contained by the source root.",
                nameof(outputDirectory))
            : fullPath;
    }

    private static CandidateEvidenceFileSnapshot ReadRequest(string requestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);
        string fullPath = Path.GetFullPath(requestPath);
        string parent = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException(
            "Candidate request must have a parent directory.",
            nameof(requestPath));
        string localParent = ResolveLocalExistingRoot(parent, nameof(requestPath));
        string resolved = FileSystemPathGuard.ResolveExistingFileUnderRoots(fullPath, [localParent]);
        RegularFileGuard.RequirePath(resolved);
        return ReadBoundedFile(resolved, "intake-request.json", MaximumJsonBytes);
    }

    private static JsonObject ParseIntakeRequest(CandidateEvidenceFileSnapshot requestSnapshot)
    {
        CandidateEvidenceSchemaValidator.ValidateDocument(
            requestSnapshot.Content,
            requestSnapshot.DisplayPath,
            MaximumJsonBytes,
            MaximumJsonDepth);
        JsonNode node = JsonNode.Parse(requestSnapshot.Content.Span) ?? throw new InvalidDataException(
            "Candidate intake request cannot be null.");
        JsonObject request = node as JsonObject ?? throw new InvalidDataException(
            "Candidate intake request root must be an object.");
        return StringComparer.Ordinal.Equals(StringValue(request, "documentKind"), "intake-request")
            ? request
            : throw new InvalidDataException("Candidate intake request must use documentKind 'intake-request'.");
    }

    private static CandidateEvidenceSourceArtifact[] CaptureSourceArtifacts(JsonObject intakeRequest, string sourceRoot)
    {
        JsonArray artifacts = ArrayValue(intakeRequest, "sourceArtifacts");
        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var snapshots = new CandidateEvidenceSourceArtifact[artifacts.Count];
        int aggregateBytes = 0;
        for (int index = 0; index < artifacts.Count; index++)
        {
            JsonObject artifact = ObjectValue(artifacts[index], "sourceArtifacts item");
            string artifactId = StringValue(artifact, "artifactId");
            string sourcePath = StringValue(artifact, "sourcePath");
            string logicalName = StringValue(artifact, "logicalName");
            int declaredSize = IntValue(artifact, "sizeBytes");
            string declaredHash = StringValue(artifact, "contentHash");
            if (!artifactIds.Add(artifactId) || artifactId is SchemaEntryId or SourceBundleEntryId or ChecklistEntryId)
            {
                throw new InvalidDataException($"Candidate source artifact id '{artifactId}' is duplicated or reserved.");
            }

            ValidateWindowsSafeSegments(sourcePath);
            ValidateWindowsSafeSegments(logicalName);
            string expectedName = sourcePath[(sourcePath.LastIndexOf('/') + 1)..];
            if (!StringComparer.Ordinal.Equals(expectedName, logicalName))
            {
                throw new InvalidDataException(
                    $"Candidate source artifact '{artifactId}' logicalName must preserve the source filename.");
            }

            string destinationPath = $"artifacts/{artifactId}/{logicalName}";
            if (!outputPaths.Add(destinationPath))
            {
                throw new InvalidDataException(
                    $"Candidate source artifact '{artifactId}' produces a case-colliding output path.");
            }

            string resolvedPath = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(sourcePath, sourceRoot);
            CandidateEvidenceFileSnapshot snapshot = ReadBoundedFile(resolvedPath, sourcePath, MaximumArtifactBytes);
            if (snapshot.Length != declaredSize || !StringComparer.Ordinal.Equals(snapshot.Sha256, declaredHash))
            {
                throw new InvalidDataException(
                    $"Candidate source artifact '{artifactId}' does not match its declared size or SHA-256.");
            }

            aggregateBytes = checked(aggregateBytes + snapshot.Length);
            if (aggregateBytes > MaximumAggregateArtifactBytes)
            {
                throw new InvalidDataException(
                    $"Candidate source artifacts exceed the {MaximumAggregateArtifactBytes}-byte aggregate limit.");
            }

            snapshots[index] = new CandidateEvidenceSourceArtifact(
                artifactId,
                StringValue(artifact, "sourceKind"),
                logicalName,
                destinationPath,
                snapshot);
        }

        ValidateFactCitations(intakeRequest, artifactIds);
        return snapshots;
    }

    private static void ValidateFactCitations(JsonObject intakeRequest, HashSet<string> artifactIds)
    {
        var factIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonNode? factNode in ArrayValue(intakeRequest, "facts"))
        {
            JsonObject fact = ObjectValue(factNode, "fact");
            string factId = StringValue(fact, "factId");
            if (!factIds.Add(factId))
            {
                throw new InvalidDataException($"Candidate fact id '{factId}' is duplicated.");
            }

            var citations = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonNode? citationNode in ArrayValue(fact, "citations"))
            {
                JsonObject citation = ObjectValue(citationNode, "fact citation");
                string artifactId = StringValue(citation, "artifactId");
                string location = StringValue(citation, "location");
                if (!artifactIds.Contains(artifactId))
                {
                    throw new InvalidDataException(
                        $"Candidate fact '{factId}' cites undeclared artifact '{artifactId}'.");
                }

                if (!citations.Add($"{artifactId}\n{location}"))
                {
                    throw new InvalidDataException($"Candidate fact '{factId}' contains a duplicate citation.");
                }
            }
        }

        var gapIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonNode? gapNode in ArrayValue(intakeRequest, "missingEvidence"))
        {
            string gapId = StringValue(ObjectValue(gapNode, "missing evidence"), "gapId");
            if (!gapIds.Add(gapId))
            {
                throw new InvalidDataException($"Candidate missing-evidence id '{gapId}' is duplicated.");
            }
        }
    }

    private static JsonObject CreateSourceBundle(
        JsonObject intakeRequest,
        string requestHash,
        IReadOnlyList<CandidateEvidenceSourceArtifact> sourceArtifacts,
        DateTimeOffset generatedAtUtc)
    {
        var sourceArtifactNodes = new JsonArray();
        foreach (CandidateEvidenceSourceArtifact artifact in sourceArtifacts)
        {
            sourceArtifactNodes.Add(new JsonObject
            {
                ["artifactId"] = artifact.ArtifactId,
                ["sourceKind"] = artifact.SourceKind,
                ["logicalName"] = artifact.LogicalName,
                ["contentHash"] = artifact.Snapshot.Sha256,
                ["sizeBytes"] = artifact.Snapshot.Length,
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "candidate-source-bundle",
            ["bundleId"] = StringValue(intakeRequest, "requestId"),
            ["requestId"] = StringValue(intakeRequest, "requestId"),
            ["requestContentHashAlgorithm"] = "sha256-raw-utf8-v1",
            ["requestContentHash"] = requestHash,
            ["manifestId"] = StringValue(intakeRequest, "manifestId"),
            ["manifestVersion"] = StringValue(intakeRequest, "manifestVersion"),
            ["requestedAtUtc"] = StringValue(intakeRequest, "requestedAtUtc"),
            ["owner"] = StringValue(intakeRequest, "owner"),
            ["scope"] = NodeValue(intakeRequest, "scope").DeepClone(),
            ["sourceArtifacts"] = sourceArtifactNodes,
            ["facts"] = NodeValue(intakeRequest, "facts").DeepClone(),
            ["missingEvidenceDisposition"] = StringValue(intakeRequest, "missingEvidenceDisposition"),
            ["missingEvidence"] = NodeValue(intakeRequest, "missingEvidence").DeepClone(),
            ["intakeProvenance"] = new JsonObject
            {
                ["toolId"] = "nvt-fw-combiner",
                ["toolVersion"] = "0.9.4",
                ["generatedAtUtc"] = ToUtcString(generatedAtUtc),
                ["candidateOnly"] = true,
            },
            ["runtimeAuthority"] = "none",
        };
    }

    private static CandidateEvidenceRootEntry[] WriteCandidateRoot(
        string candidateRoot,
        JsonObject sourceBundle,
        CandidateEvidenceSourceArtifact[] sourceArtifacts)
    {
        var entries = new List<CandidateEvidenceRootEntry>(sourceArtifacts.Length + 3)
        {
            WriteBytes(
            candidateRoot,
            SchemaEntryId,
            "schema",
            "schemas/candidate-evidence-v1.schema.json",
            CandidateEvidenceSchema.Utf8Content),
            WriteJsonEntry(
            candidateRoot,
            SourceBundleEntryId,
            "source-bundle",
            "source/candidate-source-bundle.json",
            sourceBundle)
        };
        foreach (CandidateEvidenceSourceArtifact artifact in sourceArtifacts)
        {
            entries.Add(WriteBytes(
                candidateRoot,
                artifact.ArtifactId,
                "artifact",
                artifact.DestinationPath,
                artifact.Snapshot.Content));
        }

        entries.Add(WriteBytes(
            candidateRoot,
            ChecklistEntryId,
            "checklist",
            "evidence/NEXT_STEPS.md",
            CreateChecklist(sourceBundle)));
        return [.. entries];
    }

    private static JsonObject CreateRootManifest(
        JsonObject intakeRequest,
        IReadOnlyList<CandidateEvidenceRootEntry> entries)
    {
        CandidateEvidenceEntryHashInput[] hashInputs = [.. entries.Select(static entry => entry.HashInput)];
        string contentHash = CandidateEvidenceEntryArrayHasher.CalculateContentHash(hashInputs);
        var entryNodes = new JsonArray();
        foreach (CandidateEvidenceRootEntry entry in entries)
        {
            entryNodes.Add(new JsonObject
            {
                ["entryId"] = entry.EntryId,
                ["kind"] = entry.Kind,
                ["path"] = entry.Path,
                ["contentHash"] = entry.Snapshot.Sha256,
                ["sizeBytes"] = entry.Snapshot.Length,
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "candidate-root-manifest",
            ["rootId"] = StringValue(intakeRequest, "requestId"),
            ["sourceBundleEntryId"] = SourceBundleEntryId,
            ["contractSchemaEntryId"] = SchemaEntryId,
            ["hashAlgorithm"] = "sha256-rfc8785-candidate-entry-array-v1",
            ["contentHash"] = contentHash,
            ["entries"] = entryNodes,
            ["runtimeAuthority"] = "none",
        };
    }

    private static JsonObject CreateValidationReport(
        JsonObject intakeRequest,
        string requestHash,
        string rootManifestHash,
        IReadOnlyList<CandidateEvidenceRootEntry> entries,
        JsonObject rootManifest)
    {
        var validatedEntryIds = new JsonArray();
        foreach (CandidateEvidenceRootEntry entry in entries)
        {
            validatedEntryIds.Add(entry.EntryId);
        }

        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "candidate-validation-report",
            ["reportId"] = StringValue(intakeRequest, "requestId"),
            ["rootId"] = StringValue(rootManifest, "rootId"),
            ["requestContentHashAlgorithm"] = "sha256-raw-utf8-v1",
            ["requestContentHash"] = requestHash,
            ["rootContentHash"] = StringValue(rootManifest, "contentHash"),
            ["rootManifestHashAlgorithm"] = "sha256-raw-utf8-v1",
            ["rootManifestSha256"] = rootManifestHash,
            ["validatedEntryIds"] = validatedEntryIds,
            ["checks"] = new JsonArray(
                PassedCheck("request-schema", "The declared intake request satisfies candidate evidence v1."),
                PassedCheck("source-artifacts", "Declared source artifact snapshots match their size and SHA-256."),
                PassedCheck("root-inventory", "The candidate root contains exactly its declared manifest entries.")),
            ["validationOutcome"] = "passed",
            ["missingEvidenceDisposition"] = StringValue(intakeRequest, "missingEvidenceDisposition"),
            ["missingEvidence"] = NodeValue(intakeRequest, "missingEvidence").DeepClone(),
            ["runtimeAuthority"] = "none",
        };
    }

    private static JsonObject PassedCheck(string checkId, string summary)
    {
        return new JsonObject
        {
            ["checkId"] = checkId,
            ["outcome"] = "passed",
            ["summary"] = summary,
        };
    }

    private static byte[] CreateChecklist(JsonObject sourceBundle)
    {
        var lines = new List<string>
        {
            "# Candidate Evidence Next Steps",
            string.Empty,
            "Candidate-only evidence is not a runtime profile, map, or support registration.",
            string.Empty,
            "## Declared Missing Evidence",
        };
        JsonArray gaps = ArrayValue(sourceBundle, "missingEvidence");
        if (gaps.Count == 0)
        {
            lines.Add("- Owner declared no known missing evidence; this is not a promotion decision.");
        }
        else
        {
            foreach (JsonNode? gapNode in gaps)
            {
                JsonObject gap = ObjectValue(gapNode, "missing evidence");
                lines.Add($"- {StringValue(gap, "gapId")}: {StringValue(gap, "statement")}");
            }
        }

        lines.AddRange([
            string.Empty,
            "## Required Review",
            string.Empty,
            "- Verify source artifact hashes, citations, and unresolved evidence.",
            "- Promote only through a separately reviewed V2 profile bundle and registration change.",
        ]);
        return Encoding.UTF8.GetBytes(string.Join('\n', lines) + '\n');
    }

    private static CandidateEvidenceRootEntry WriteJsonEntry(
        string candidateRoot,
        string entryId,
        string kind,
        string relativePath,
        JsonObject document)
    {
        CandidateEvidenceFileSnapshot snapshot = WriteJson(
            ResolveCandidatePath(candidateRoot, relativePath),
            document);
        CandidateEvidenceSchemaValidator.ValidateDocument(
            snapshot.Content,
            relativePath,
            MaximumJsonBytes,
            MaximumJsonDepth);
        return new CandidateEvidenceRootEntry(entryId, kind, relativePath, snapshot);
    }

    private static CandidateEvidenceRootEntry WriteBytes(
        string candidateRoot,
        string entryId,
        string kind,
        string relativePath,
        ReadOnlyMemory<byte> content)
    {
        CandidateEvidenceFileSnapshot snapshot = WriteBytes(ResolveCandidatePath(candidateRoot, relativePath), content, relativePath);
        return new CandidateEvidenceRootEntry(entryId, kind, relativePath, snapshot);
    }

    private static CandidateEvidenceFileSnapshot WriteJson(string path, JsonObject document)
    {
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(document, IndentedJsonOptions);
        return WriteBytes(path, content, Path.GetFileName(path));
    }

    private static CandidateEvidenceFileSnapshot WriteBytes(
        string path,
        ReadOnlyMemory<byte> content,
        string displayPath)
    {
        string? parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new InvalidOperationException("Candidate output path must have a parent directory.");
        }

        _ = Directory.CreateDirectory(parent);
        using (var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.WriteThrough,
        }))
        {
            stream.Write(content.Span);
            stream.Flush(flushToDisk: true);
        }

        return new CandidateEvidenceFileSnapshot(displayPath, content.ToArray());
    }

    private static string ResolveCandidatePath(string candidateRoot, string relativePath)
    {
        string[] segments = relativePath.Split('/');
        return Path.Combine([candidateRoot, .. segments]);
    }

    private static CandidateEvidenceFileSnapshot ReadBoundedFile(string path, string displayPath, int maximumBytes)
    {
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan,
            BufferSize = 4096,
        });
        RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, displayPath);
        long length = stream.Length;
        if (length <= 0 || length > maximumBytes)
        {
            throw new InvalidDataException($"Candidate file '{displayPath}' exceeds its allowed size.");
        }

        byte[] content = new byte[checked((int)length)];
        stream.ReadExactly(content);
        return stream.ReadByte() != -1 || stream.Length != length
            ? throw new IOException($"Candidate file '{displayPath}' changed while it was being read.")
            : new CandidateEvidenceFileSnapshot(displayPath, content);
    }

    private static void RejectNetworkPath(string fullPath, string parameterName)
    {
        if (OperatingSystem.IsWindows() && fullPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Candidate paths must use local volumes, not UNC/network paths.", parameterName);
        }
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return StringComparer.FromComparison(comparison).Equals(normalizedCandidate, normalizedRoot) ||
            normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static void ValidateWindowsSafeSegments(string path)
    {
        foreach (string segment in path.Split('/'))
        {
            if (segment.StartsWith("~$", StringComparison.Ordinal) ||
                segment.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Candidate path declares an Office or tool lock file.");
            }
            if (segment.EndsWith(' ') || segment.EndsWith('.') || IsWindowsReservedDeviceName(segment.Split('.')[0]))
            {
                throw new InvalidDataException("Candidate path contains a Windows alias-ambiguous segment.");
            }
        }
    }

    private static bool IsWindowsReservedDeviceName(string value)
    {
        return value.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (value.Length == 4 &&
                (value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                value[3] is >= '1' and <= '9');
    }

    private static string ToUtcString(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static JsonObject ObjectValue(JsonNode? node, string label)
    {
        return node as JsonObject ?? throw new InvalidDataException($"Candidate {label} must be an object.");
    }

    private static JsonNode NodeValue(JsonObject document, string propertyName)
    {
        return document[propertyName] ?? throw new InvalidDataException(
            $"Candidate document is missing '{propertyName}'.");
    }

    private static JsonArray ArrayValue(JsonObject document, string propertyName)
    {
        return NodeValue(document, propertyName) as JsonArray ?? throw new InvalidDataException(
            $"Candidate document property '{propertyName}' must be an array.");
    }

    private static string StringValue(JsonObject document, string propertyName)
    {
        return NodeValue(document, propertyName).GetValue<string>();
    }

    private static int IntValue(JsonObject document, string propertyName)
    {
        return NodeValue(document, propertyName).GetValue<int>();
    }
}

/// <summary>Declares one candidate-only materialization request.</summary>
internal sealed record CandidateEvidenceMaterializationRequest(
    string RequestPath,
    string SourceRoot,
    string OutputDirectory,
    DateTimeOffset GeneratedAtUtc);

/// <summary>Identifies a published candidate root and its sidecar validation report.</summary>
internal sealed record CandidateEvidenceMaterializationResult(
    string CandidateRootDirectory,
    string ValidationReportPath,
    string RootContentHash);

internal sealed record CandidateEvidenceSourceArtifact(
    string ArtifactId,
    string SourceKind,
    string LogicalName,
    string DestinationPath,
    CandidateEvidenceFileSnapshot Snapshot);

internal sealed record CandidateEvidenceRootEntry(
    string EntryId,
    string Kind,
    string Path,
    CandidateEvidenceFileSnapshot Snapshot)
{
    internal CandidateEvidenceEntryHashInput HashInput => new(
        EntryId,
        Kind,
        Path,
        Snapshot.Sha256,
        Snapshot.Length);
}

internal sealed class CandidateEvidenceFileSnapshot
{
    private readonly byte[] _content;

    internal CandidateEvidenceFileSnapshot(string displayPath, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPath);
        ArgumentNullException.ThrowIfNull(content);
        DisplayPath = displayPath;
        _content = content;
        Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    internal string DisplayPath { get; }

    internal int Length => _content.Length;

    internal string Sha256 { get; }

    internal ReadOnlyMemory<byte> Content => _content;
}
