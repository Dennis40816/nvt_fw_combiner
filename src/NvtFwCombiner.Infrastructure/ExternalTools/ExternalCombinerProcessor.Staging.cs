using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

public sealed partial class ExternalCombinerProcessor
{
    private static async ValueTask<IReadOnlyDictionary<string, string>> MaterializeStagedArtifactsAsync(
        ExternalProcessorRequest request,
        string runDirectory,
        CancellationToken cancellationToken)
    {
        var artifactPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ExternalProcessorStagedArtifact artifact in request.StagedArtifacts)
        {
            string path = Path.Combine(runDirectory, $"artifact-{artifact.ArtifactId}.bin");
            _ = artifactPaths.TryAdd(artifact.ArtifactId, path);
            await File.WriteAllBytesAsync(path, artifact.Bytes, cancellationToken).ConfigureAwait(false);
        }

        return artifactPaths;
    }

    private static bool TryExpandArguments(
        IEnumerable<string> arguments,
        string workBin,
        string outputBin,
        string runDirectory,
        IReadOnlyDictionary<string, string> stagedArtifactPaths,
        IReadOnlyList<ExternalProcessorStagedArtifact> stagedArtifacts,
        out IReadOnlyList<string>? expandedArguments,
        out CompositionIssue? issue)
    {
        var usedArtifactIds = new HashSet<string>(StringComparer.Ordinal);
        var expanded = new List<string>();
        foreach (string template in arguments)
        {
            string argument = template
                .Replace(ExternalCombinerStagingTokens.WorkBin, workBin, StringComparison.Ordinal)
                .Replace(ExternalCombinerStagingTokens.OutputBin, outputBin, StringComparison.Ordinal)
                .Replace(ExternalCombinerStagingTokens.RunDirectory, runDirectory, StringComparison.Ordinal);
            foreach ((string artifactId, string artifactPath) in stagedArtifactPaths)
            {
                string token = ExternalCombinerStagingTokens.Artifact(artifactId);
                if (argument.Contains(token, StringComparison.Ordinal))
                {
                    _ = usedArtifactIds.Add(artifactId);
                    argument = argument.Replace(token, artifactPath, StringComparison.Ordinal);
                }
            }

            if (argument.Contains("{staging.artifact.", StringComparison.Ordinal))
            {
                expandedArguments = null;
                issue = new CompositionIssue(
                    "external-tool.staged-artifact.unknown",
                    "External processor manifest references an artifact that the plan did not stage.");
                return false;
            }

            expanded.Add(argument);
        }

        ExternalProcessorStagedArtifact? unusedArtifact = stagedArtifacts.FirstOrDefault(
            artifact => !usedArtifactIds.Contains(artifact.ArtifactId));
        if (unusedArtifact is not null)
        {
            expandedArguments = null;
            issue = new CompositionIssue(
                "external-tool.staged-artifact.unused",
                $"External processor staged artifact '{unusedArtifact.ArtifactId}' is not referenced by the manifest.");
            return false;
        }

        expandedArguments = expanded;
        issue = null;
        return true;
    }

    private static CompositionIssue? FindUnexpectedStagingFileIssue(
        string runDirectory,
        string inputMode,
        ExternalCombinerToolManifest manifest,
        IEnumerable<string> stagedArtifactPaths)
    {
        HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            WorkFileName,
        };
        if (string.Equals(inputMode, "input-output-file", StringComparison.Ordinal))
        {
            _ = allowed.Add(OutputFileName);
        }

        foreach (string path in stagedArtifactPaths)
        {
            _ = allowed.Add(Path.GetFileName(path));
        }

        foreach (string name in manifest.AllowedExtraOutputFiles)
        {
            _ = allowed.Add(name);
        }

        foreach (string entry in Directory.EnumerateFileSystemEntries(runDirectory))
        {
            string name = Path.GetFileName(entry);
            if (!allowed.Contains(name) || Directory.Exists(entry))
            {
                return new CompositionIssue(
                    "external-tool.unexpected-output-file",
                    $"External processor produced unexpected staging entry '{name}'.");
            }
        }

        return null;
    }

    private static async ValueTask<CompositionIssue?> VerifyStagedArtifactsUnchangedAsync(
        IReadOnlyList<ExternalProcessorStagedArtifact> stagedArtifacts,
        IReadOnlyDictionary<string, string> stagedArtifactPaths,
        CancellationToken cancellationToken)
    {
        foreach (ExternalProcessorStagedArtifact artifact in stagedArtifacts)
        {
            string path = stagedArtifactPaths[artifact.ArtifactId];
            if (!File.Exists(path))
            {
                return new CompositionIssue(
                    "external-tool.staged-artifact.modified",
                    $"External processor removed staging artifact '{artifact.ArtifactId}'.");
            }

            if (!await StagedArtifactFileVerifier
                    .MatchesAsync(path, artifact.Bytes, cancellationToken)
                    .ConfigureAwait(false))
            {
                return new CompositionIssue(
                    "external-tool.staged-artifact.modified",
                    $"External processor modified staging artifact '{artifact.ArtifactId}'.");
            }
        }

        return null;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
