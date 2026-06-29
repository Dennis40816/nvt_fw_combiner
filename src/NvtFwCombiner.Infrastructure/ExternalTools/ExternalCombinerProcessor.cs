using System.Security.Cryptography;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Runs approved legacy combiner transforms inside a private staging directory.</summary>
public sealed class ExternalCombinerProcessor : IExternalProcessor
{
    private const string WorkFileName = "work.bin";
    private const string OutputFileName = "output.bin";

    private readonly ExternalCombinerToolRegistry _registry;
    private readonly string _toolRoot;
    private readonly string _stagingRoot;
    private readonly IExternalProcessRunner _processRunner;

    /// <summary>Creates a staged external combiner processor.</summary>
    public ExternalCombinerProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(processRunner);

        _registry = registry;
        _toolRoot = Path.GetFullPath(toolRoot);
        _stagingRoot = Path.GetFullPath(stagingRoot);
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public async ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryResolveManifest(request, out ExternalCombinerToolManifest? manifest, out CompositionIssue? manifestIssue))
        {
            return ExternalProcessorResult.Failed([manifestIssue!]);
        }

        if (!TryResolveExecutable(manifest!, out string? executablePath, out CompositionIssue? executableIssue))
        {
            return ExternalProcessorResult.Failed([executableIssue!]);
        }

        string runDirectory = Path.Combine(_stagingRoot, request.RunId);
        try
        {
            if (Directory.Exists(runDirectory))
            {
                return Fail("external-tool.staging.exists", "External processor staging directory already exists.");
            }

            _ = Directory.CreateDirectory(runDirectory);
            string workBin = Path.Combine(runDirectory, WorkFileName);
            string outputBin = Path.Combine(runDirectory, OutputFileName);
            await File.WriteAllBytesAsync(workBin, request.InputBytes.ToArray(), cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> arguments = ExpandArguments(manifest!.ArgumentTemplate, workBin, outputBin, runDirectory);
            var startInfo = new ExternalProcessStartInfo(
                executablePath!,
                runDirectory,
                arguments,
                TimeSpan.FromSeconds(manifest.TimeoutSeconds));
            ExternalProcessResult processResult = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (processResult.TimedOut)
            {
                return Fail("external-tool.process.timeout", "External processor timed out.");
            }

            if (processResult.ExitCode != 0)
            {
                return Fail("external-tool.process.failed", $"External processor exited with code {processResult.ExitCode}.");
            }

            CompositionIssue? unexpectedFileIssue = FindUnexpectedStagingFileIssue(runDirectory, manifest);
            if (unexpectedFileIssue is not null)
            {
                return ExternalProcessorResult.Failed([unexpectedFileIssue]);
            }

            string transformedPath = string.Equals(manifest.InputMode, "input-output-file", StringComparison.Ordinal)
                ? outputBin
                : workBin;
            if (!File.Exists(transformedPath))
            {
                return Fail("external-tool.output.missing", "External processor did not produce the expected output file.");
            }

            byte[] outputBytes = await File.ReadAllBytesAsync(transformedPath, cancellationToken).ConfigureAwait(false);
            if (outputBytes.LongLength != request.InputBytes.Length)
            {
                return Fail("external-tool.output-length.changed", "External processor changed the firmware image length.");
            }

            IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(request.InputBytes.Span, outputBytes);
            ChangedRangeVerdict verdict = new ChangedRangePolicy(request.AllowedWriteRanges).Evaluate(changedRanges);
            return verdict.IsAllowed
                ? ExternalProcessorResult.Success(outputBytes, changedRanges)
                : ExternalProcessorResult.Failed([
                    new CompositionIssue(
                        "external-tool.write-range.violation",
                        "External processor changed bytes outside declared write ranges."),
                ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(
                "external-tool.staging.io-failed",
                $"External processor staging failed ({exception.GetType().Name}).");
        }
        finally
        {
            TryDeleteDirectory(runDirectory);
        }
    }

    private bool TryResolveManifest(
        ExternalProcessorRequest request,
        out ExternalCombinerToolManifest? manifest,
        out CompositionIssue? issue)
    {
        try
        {
            manifest = _registry.Resolve(request.ToolBindingId);
            issue = null;
            return true;
        }
        catch (KeyNotFoundException)
        {
            manifest = null;
            issue = new CompositionIssue(
                "external-tool.binding.unknown",
                $"External combiner tool binding '{request.ToolBindingId}' is not registered.");
            return false;
        }
    }

    private bool TryResolveExecutable(
        ExternalCombinerToolManifest manifest,
        out string? executablePath,
        out CompositionIssue? issue)
    {
        executablePath = null;
        issue = null;

        if (!IsSafePathSegment(manifest.ToolId) || !IsSafePathSegment(manifest.ToolVersion))
        {
            issue = new CompositionIssue(
                "external-tool.binding.path-unsafe",
                "External combiner tool binding contains unsafe path segments.");
            return false;
        }

        string resolvedPath = Path.GetFullPath(Path.Combine(
            _toolRoot,
            manifest.ToolId,
            manifest.ToolVersion,
            manifest.ExecutableName));
        if (!IsInsideDirectory(_toolRoot, resolvedPath))
        {
            issue = new CompositionIssue(
                "external-tool.executable.path-escape",
                "External combiner executable path escapes the approved tool root.");
            return false;
        }

        if (!File.Exists(resolvedPath))
        {
            issue = new CompositionIssue(
                "external-tool.executable.missing",
                "External combiner executable was not found.");
            return false;
        }

        string actualSha256 = GetLowerSha256(resolvedPath);
        if (!string.Equals(actualSha256, manifest.Sha256, StringComparison.Ordinal))
        {
            issue = new CompositionIssue(
                "external-tool.executable-sha.mismatch",
                "External combiner executable SHA-256 does not match its manifest.");
            return false;
        }

        executablePath = resolvedPath;
        return true;
    }

    private static IReadOnlyList<string> ExpandArguments(
        IEnumerable<string> arguments,
        string workBin,
        string outputBin,
        string runDirectory)
    {
        return [
            .. arguments.Select(argument => argument
                .Replace("{staging.workBin}", workBin, StringComparison.Ordinal)
                .Replace("{staging.outputBin}", outputBin, StringComparison.Ordinal)
                .Replace("{staging.runDir}", runDirectory, StringComparison.Ordinal)),
        ];
    }

    private static CompositionIssue? FindUnexpectedStagingFileIssue(
        string runDirectory,
        ExternalCombinerToolManifest manifest)
    {
        HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            WorkFileName,
        };
        if (string.Equals(manifest.InputMode, "input-output-file", StringComparison.Ordinal))
        {
            _ = allowed.Add(OutputFileName);
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

    private static ExternalProcessorResult Fail(string code, string message)
    {
        return ExternalProcessorResult.Failed([new CompositionIssue(code, message)]);
    }

    private static bool IsSafePathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value != "." &&
               value != ".." &&
               value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
    }

    private static bool IsInsideDirectory(string root, string candidate)
    {
        string relative = Path.GetRelativePath(root, candidate);
        return relative != "." &&
               !relative.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    private static string GetLowerSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
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
