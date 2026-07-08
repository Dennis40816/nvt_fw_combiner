using System.Security.Cryptography;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Runs IC-specific CtrlRAM postbuild command sequences through approved legacy Combiner.exe binaries.</summary>
public sealed partial class LegacyCombinerPostbuildProcessor : IExternalProcessor
{
    private const string BinDirectoryName = "BIN";
    private const string OutputDirectoryName = "output";
    private const string MapFileName = "map.txt";

    private readonly ExternalCombinerToolRegistry _registry;
    private readonly Dictionary<string, LegacyCombinerPostbuildProfile> _profilesByProcessorId;
    private readonly string _toolRoot;
    private readonly string _stagingRoot;
    private readonly IExternalProcessRunner _processRunner;

    /// <summary>Creates a staged postbuild processor with approved tool and IC command profiles.</summary>
    public LegacyCombinerPostbuildProcessor(
        ExternalCombinerToolRegistry registry,
        IEnumerable<LegacyCombinerPostbuildProfile> postbuildProfiles,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(postbuildProfiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(processRunner);

        _registry = registry;
        _profilesByProcessorId = BuildProfileIndex(postbuildProfiles);
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

        if (!_profilesByProcessorId.TryGetValue(request.ProcessorId, out LegacyCombinerPostbuildProfile? profile))
        {
            return Fail(
                "legacy-combiner.postbuild-profile.unknown",
                $"Legacy combiner postbuild profile '{request.ProcessorId}' is not registered.");
        }

        if (!string.Equals(profile.ToolBindingId, request.ToolBindingId, StringComparison.Ordinal))
        {
            return Fail(
                "legacy-combiner.tool-binding.mismatch",
                "External processor request tool binding does not match the postbuild profile.");
        }

        if (!TryResolveManifest(request, out ExternalCombinerToolManifest? manifest, out CompositionIssue? manifestIssue))
        {
            return ExternalProcessorResult.Failed([manifestIssue!]);
        }

        if (!TryResolveExecutable(manifest!, out string? executablePath, out CompositionIssue? executableIssue))
        {
            return ExternalProcessorResult.Failed([executableIssue!]);
        }

        LegacyCombinerPostbuildCommandPlan commandPlan;
        try
        {
            commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(profile, request.IcNumberSelection);
        }
        catch (ArgumentException exception)
        {
            return Fail("legacy-combiner.branch.invalid", exception.Message);
        }

        string runDirectory = Path.GetFullPath(Path.Combine(_stagingRoot, request.RunId));
        if (!IsInsideDirectory(_stagingRoot, runDirectory))
        {
            return Fail(
                "external-tool.staging.path-escape",
                "External processor staging directory escapes the approved staging root.");
        }

        try
        {
            if (Directory.Exists(runDirectory))
            {
                return Fail("external-tool.staging.exists", "External processor staging directory already exists.");
            }

            string outputDirectory = Path.Combine(runDirectory, OutputDirectoryName);
            string binDirectory = Path.Combine(runDirectory, BinDirectoryName);
            _ = Directory.CreateDirectory(outputDirectory);
            _ = Directory.CreateDirectory(binDirectory);

            string firmwarePath = Path.Combine(outputDirectory, profile.FirmwareFileName);
            byte[] inputBytes = request.InputBytes.ToArray();
            await File.WriteAllBytesAsync(firmwarePath, inputBytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory, MapFileName), [], cancellationToken)
                .ConfigureAwait(false);

            // Staged BIN files use selected replacement bytes as source material without
            // pre-writing those bytes into the firmware image given to Combiner.exe.
            byte[] stagedSourceBytes = [.. inputBytes];
            CompositionIssue? stagedSourceIssue = ApplyStagedSourceOverrides(stagedSourceBytes, request.StagedSources);
            if (stagedSourceIssue is not null)
            {
                return ExternalProcessorResult.Failed([stagedSourceIssue]);
            }

            foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
            {
                byte[] commandInputBytes = await File.ReadAllBytesAsync(firmwarePath, cancellationToken).ConfigureAwait(false);
                if (commandInputBytes.LongLength != inputBytes.LongLength)
                {
                    return Fail("external-tool.output-length.changed", "External processor changed the firmware image length.");
                }

                ResetDirectory(binDirectory);
                CompositionIssue? stagingIssue = MaterializeStagedBlockFiles(command.Blocks, stagedSourceBytes, binDirectory);
                if (stagingIssue is not null)
                {
                    return ExternalProcessorResult.Failed([stagingIssue]);
                }

                IReadOnlyList<string> arguments = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
                    command,
                    firmwarePath,
                    binDirectory);
                var startInfo = new ExternalProcessStartInfo(
                    executablePath!,
                    runDirectory,
                    arguments,
                    TimeSpan.FromSeconds(manifest!.TimeoutSeconds));
                ExternalProcessResult processResult = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
                if (processResult.TimedOut)
                {
                    return Fail("external-tool.process.timeout", $"External processor command '{command.CommandId}' timed out.");
                }

                if (processResult.ExitCode != 0)
                {
                    return Fail(
                        "external-tool.process.failed",
                        $"External processor command '{command.CommandId}' exited with code {processResult.ExitCode}. {FormatProcessOutput(processResult)}");
                }

                CompositionIssue? lengthIssue = await NormalizeShortenedFirmwareAsync(
                        firmwarePath,
                        commandInputBytes,
                        command,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (lengthIssue is not null)
                {
                    return ExternalProcessorResult.Failed([lengthIssue]);
                }

                CompositionIssue? perCommandUnexpectedFileIssue = ValidateStagingTree(runDirectory, profile, manifest!, commandPlan);
                if (perCommandUnexpectedFileIssue is not null)
                {
                    return ExternalProcessorResult.Failed([perCommandUnexpectedFileIssue]);
                }
            }

            if (!File.Exists(firmwarePath))
            {
                return Fail("external-tool.output.missing", "External processor did not leave the staged firmware file.");
            }

            CompositionIssue? unexpectedFileIssue = ValidateStagingTree(runDirectory, profile, manifest!, commandPlan);
            if (unexpectedFileIssue is not null)
            {
                return ExternalProcessorResult.Failed([unexpectedFileIssue]);
            }

            byte[] outputBytes = await File.ReadAllBytesAsync(firmwarePath, cancellationToken).ConfigureAwait(false);
            if (outputBytes.LongLength != inputBytes.LongLength)
            {
                return Fail("external-tool.output-length.changed", "External processor changed the firmware image length.");
            }

            IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(inputBytes, outputBytes);
            ChangedRangeVerdict verdict = new ChangedRangePolicy(request.AllowedWriteRanges).Evaluate(changedRanges);
            return verdict.IsAllowed
                ? ExternalProcessorResult.Success(outputBytes, changedRanges)
                : ExternalProcessorResult.Failed([
                    new CompositionIssue(
                        "external-tool.write-range.violation",
                        $"External processor changed bytes outside declared write ranges: {FormatRanges(verdict.ViolatingRanges)}."),
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

    private static Dictionary<string, LegacyCombinerPostbuildProfile> BuildProfileIndex(
        IEnumerable<LegacyCombinerPostbuildProfile> profiles)
    {
        Dictionary<string, LegacyCombinerPostbuildProfile> byProcessorId = new(StringComparer.Ordinal);
        foreach (LegacyCombinerPostbuildProfile profile in profiles)
        {
            if (!byProcessorId.TryAdd(profile.ProcessorId, profile))
            {
                throw new ArgumentException(
                    $"Legacy combiner postbuild profile '{profile.ProcessorId}' is declared more than once.",
                    nameof(profiles));
            }
        }

        return byProcessorId;
    }

    private static string FormatProcessOutput(ExternalProcessResult processResult)
    {
        string stderr = Shorten(processResult.StandardError);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return $"stderr: {stderr}";
        }

        string stdout = Shorten(processResult.StandardOutput);
        return string.IsNullOrWhiteSpace(stdout)
            ? "No process output was captured."
            : $"stdout: {stdout}";
    }

    private static string FormatRanges(IReadOnlyList<ByteRange> ranges)
    {
        return ranges.Count == 0
            ? "none"
            : string.Join(
                ", ",
                ranges
                    .Take(12)
                    .Select(range => FormattableString.Invariant(
                        $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})"))) +
            (ranges.Count > 12
                ? FormattableString.Invariant($" ... {ranges.Count - 12} more")
                : string.Empty);
    }

    private static string Shorten(string value)
    {
        string compact = value.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 240 ? compact : $"{compact[..240]}...";
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
