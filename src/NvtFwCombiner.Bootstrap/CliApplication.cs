using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Runs the command-line application through the production composition services.</summary>
public static class CliApplication
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;

    private static readonly Dictionary<string, string> InputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            ["dp-input"] = "--dp",
            ["tp-input"] = "--tp",
            ["ld-input"] = "--ld",
        };

    /// <summary>Runs one command-line invocation and returns the process exit code.</summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args is ["--version"] or ["version"])
        {
            await output.WriteLineAsync(Version).ConfigureAwait(false);
            return Success;
        }

        if (args is ["doctor"])
        {
            await output.WriteLineAsync("NVT FW Combiner repository bootstrap is healthy.").ConfigureAwait(false);
            await output.WriteLineAsync($"CLI assembly version: {Version}").ConfigureAwait(false);
            await output.WriteLineAsync("Composition core command surface is available.").ConfigureAwait(false);
            return Success;
        }

        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        try
        {
            return args[0] switch
            {
                "profiles" => await RunProfilesAsync(args[1..], output, error).ConfigureAwait(false),
                "standard-merge" => await RunStandardMergeAsync(args[1..], output, error, cancellationToken)
                    .ConfigureAwait(false),
                "dp-replace" or "ctrlram-replace" or "general-replace" =>
                    await ReplaceCliCommandHandler.RunAsync(args[0], args[1..], output, error, cancellationToken)
                        .ConfigureAwait(false),
                _ => await UnknownCommandAsync(args[0], error).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("error: operation canceled").ConfigureAwait(false);
            return SoftwareError;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync($"error: {exception.Message}").ConfigureAwait(false);
            return SoftwareError;
        }
    }

    private static string Version => (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ??
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString() ??
        "unknown";

    private static async Task<int> RunProfilesAsync(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Length > 1 || args is ["--help"])
        {
            await WriteProfilesUsageAsync(output).ConfigureAwait(false);
            return args is ["--help"] ? Success : UsageError;
        }

        if (args.Length == 1 && args[0] != "list")
        {
            await error.WriteLineAsync($"error: unknown profiles command '{args[0]}'").ConfigureAwait(false);
            return UsageError;
        }

        await output.WriteLineAsync("Built-in standard merge profiles:").ConfigureAwait(false);
        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                     .OrderBy(profile => profile.IcId, StringComparer.Ordinal))
        {
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            string inputs = compile.IsSuccess
                ? string.Join(", ", compile.Plan!.RequiredInputAddressSpaceIds)
                : "compile-error";
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  default-output={profile.DefaultOutputFileName}"))
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("Built-in replace profiles:").ConfigureAwait(false);
        foreach (CompositionProfileDefinition profile in BuiltInReplaceProfiles.All
                     .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal))
        {
            ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
            string inputs = compile.IsSuccess
                ? string.Join(", ", compile.Plan!.RequiredInputAddressSpaceIds)
                : "compile-error";
            await output.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{profile.ProfileId}  ic={profile.IcId}  inputs={inputs}  ic-num={profile.IcNumberInputMode?.ToString() ?? "none"}  default-output={profile.DefaultOutputFileName}"))
                .ConfigureAwait(false);
        }

        return Success;
    }

    private static async Task<int> RunStandardMergeAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteStandardMergeUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown standard-merge command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = ["--profile", "--dp", "--tp", "--ld", "--output", "--report"];
        string[] flagOptions = action == "build" ? ["--overwrite"] : [];
        if (!TryParseOptions(args[1..], valueOptions, flagOptions, error, out ParsedOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryFindStandardMergeProfile(profileSelector, out CompositionProfileDefinition? selectedProfile))
        {
            await error.WriteLineAsync($"error: unknown standard merge profile '{profileSelector}'").ConfigureAwait(false);
            return UsageError;
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(selectedProfile, []);
        if (!compile.IsSuccess)
        {
            await PrintIssuesAsync(error, compile.Issues).ConfigureAwait(false);
            return SoftwareError;
        }

        CompositionPlan plan = compile.Plan!;
        if (!TryCreateBindings(plan, options, error, out IReadOnlyList<InputArtifactBinding> bindings))
        {
            return UsageError;
        }

        OutputTarget outputTarget = ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            selectedProfile.DefaultOutputFileName);
        if (action == "build")
        {
            EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
        }

        EnsureReportDoesNotAliasProtectedPaths(options, bindings, outputTarget, action == "build");

        string[] inputRoots = [.. bindings
            .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)];
        var reader = new FileArtifactReader(inputRoots);
        AtomicFileCompositionOutputWriter? writer = action == "build"
            ? new AtomicFileCompositionOutputWriter(outputTarget.OutputDirectory, options.Flags.Contains("--overwrite"))
            : null;
        var service = new CompositionRunService(reader, new SystemClock(), writer, ExternalProcessorFactory.CreateOrNull());
        var request = new CompositionRunRequest(
            CreateRunId(action),
            ToRunProfile(selectedProfile),
            plan,
            bindings,
            outputTarget.FileName);

        CompositionRunResult result = action == "preview"
            ? await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false)
            : await BuildWithInternalPreviewAsync(service, request, cancellationToken).ConfigureAwait(false);
        await WriteReportFileIfRequestedAsync(result, options, bindings, outputTarget, action == "build", output, cancellationToken)
            .ConfigureAwait(false);
        await PrintRunResultAsync(result, output, error).ConfigureAwait(false);
        return result.Status == CompositionExecutionStatus.Succeeded ? Success : CompositionFailed;
    }

    private static async ValueTask<CompositionRunResult> BuildWithInternalPreviewAsync(
        CompositionRunService service,
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        return preview.Status == CompositionExecutionStatus.Succeeded
            ? await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
                .ConfigureAwait(false)
            : preview;
    }

    private static bool TryCreateBindings(
        CompositionPlan plan,
        ParsedOptions options,
        TextWriter error,
        out IReadOnlyList<InputArtifactBinding> bindings)
    {
        List<InputArtifactBinding> items = [];
        HashSet<string> requiredAddressSpaces = [.. plan.RequiredInputAddressSpaceIds, .. plan.RequiredSeededMutableAddressSpaceIds];
        foreach (string addressSpaceId in requiredAddressSpaces.Order(StringComparer.Ordinal))
        {
            if (!InputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: profile requires unsupported address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            if (!options.Values.TryGetValue(optionName, out string? path))
            {
                error.WriteLine($"error: {optionName} is required for address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            items.Add(new InputArtifactBinding(addressSpaceId, addressSpaceId, fullPath));
        }

        foreach ((string addressSpaceId, string optionName) in InputOptionsByAddressSpace)
        {
            if (options.Values.ContainsKey(optionName) && !requiredAddressSpaces.Contains(addressSpaceId))
            {
                error.WriteLine($"error: {optionName} is not used by this profile");
                bindings = [];
                return false;
            }
        }

        bindings = items;
        return true;
    }

    private static OutputTarget ResolveOutputTarget(string? requestedOutput, string defaultFileName)
    {
        string outputPath = string.IsNullOrWhiteSpace(requestedOutput)
            ? Path.GetFullPath(defaultFileName)
            : Path.GetFullPath(requestedOutput);
        string? directory = Path.GetDirectoryName(outputPath);
        string fileName = Path.GetFileName(outputPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("Output must resolve to a file path.")
            : new OutputTarget(directory, fileName);
    }

    private static void EnsureOutputDoesNotAliasInputs(
        OutputTarget outputTarget,
        IReadOnlyList<InputArtifactBinding> bindings)
    {
        ProtectedPathGuard.EnsureOutputDoesNotAliasInputs(
            outputTarget.FullPath,
            bindings,
            nameof(outputTarget));
    }

    private static void EnsureReportDoesNotAliasProtectedPaths(
        ParsedOptions options,
        IReadOnlyList<InputArtifactBinding> bindings,
        OutputTarget outputTarget,
        bool protectOutput)
    {
        if (!options.Values.TryGetValue("--report", out string? reportPath))
        {
            return;
        }

        ProtectedPathGuard.EnsureReportDoesNotAliasProtectedPaths(
            reportPath,
            bindings,
            protectOutput ? outputTarget.FullPath : null,
            "--report");
    }

    private static bool TryFindStandardMergeProfile(
        string selector,
        [NotNullWhen(true)]
        out CompositionProfileDefinition? profile)
    {
        string normalized = selector.Trim();
        profile = BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }

    private static string GetIcNumber(string icId)
    {
        return icId.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? icId[2..]
            : icId;
    }

    private static CompositionRunProfile ToRunProfile(CompositionProfileDefinition profile)
    {
        return new CompositionRunProfile(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind,
            profile.IcNumberInputMode);
    }

    private static async Task PrintRunResultAsync(
        CompositionRunResult result,
        TextWriter output,
        TextWriter error)
    {
        CompositionRunReport report = result.Report;
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {report.ProfileId} ({report.IcId})").ConfigureAwait(false);
        await output.WriteLineAsync($"Output: {report.Output.FileName}").ConfigureAwait(false);
        await output.WriteLineAsync($"Size: {report.Output.Size.ToString(CultureInfo.InvariantCulture)} bytes").ConfigureAwait(false);
        await output.WriteLineAsync($"SHA256: {report.Output.Sha256}").ConfigureAwait(false);
        if (result.PreviewToken is not null)
        {
            await output.WriteLineAsync($"PreviewToken: {result.PreviewToken}").ConfigureAwait(false);
        }

        if (result.CommittedOutputId is not null)
        {
            await output.WriteLineAsync($"Committed: {result.CommittedOutputId}").ConfigureAwait(false);
        }

        if (report.Mutations.Count > 0)
        {
            await output.WriteLineAsync("Mutations:").ConfigureAwait(false);
            foreach (MutationRunSummary mutation in report.Mutations)
            {
                await output.WriteLineAsync(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"  {mutation.OperationId}: {mutation.TargetSpaceId} {FormatRange(mutation.TargetRange)} changed={mutation.ChangedByteCount}"))
                    .ConfigureAwait(false);
            }
        }

        if (report.Issues.Count > 0)
        {
            await PrintIssuesAsync(error, report.Issues).ConfigureAwait(false);
        }
    }

    private static async Task WriteReportFileIfRequestedAsync(
        CompositionRunResult result,
        ParsedOptions options,
        IReadOnlyList<InputArtifactBinding> bindings,
        OutputTarget outputTarget,
        bool protectOutput,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (!options.Values.TryGetValue("--report", out string? reportPath))
        {
            return;
        }

        string fullPath = await CliRunReportWriter
            .WriteAsync(
                result.Report,
                reportPath,
                ProtectedPathGuard.CreateProtectedPaths(
                    bindings,
                    protectOutput ? outputTarget.FullPath : null),
                cancellationToken)
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Report: {fullPath}").ConfigureAwait(false);
    }

    private static async Task PrintIssuesAsync(
        TextWriter error,
        IReadOnlyList<CompositionIssue> issues)
    {
        await error.WriteLineAsync("Issues:").ConfigureAwait(false);
        foreach (CompositionIssue issue in issues)
        {
            string operation = issue.OperationId is null ? string.Empty : $" [{issue.OperationId}]";
            await error.WriteLineAsync($"  {issue.Code}{operation}: {issue.Message}").ConfigureAwait(false);
        }
    }

    private static string FormatRange(ByteRange range)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})");
    }

    private static bool TryParseOptions(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> flagOptions,
        TextWriter error,
        out ParsedOptions parsed)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        HashSet<string> flags = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string name = args[index];
            if (flagOptions.Contains(name))
            {
                if (!flags.Add(name))
                {
                    error.WriteLine($"error: duplicate option '{name}'");
                    parsed = ParsedOptions.Empty;
                    return false;
                }

                continue;
            }

            if (!valueOptions.Contains(name))
            {
                error.WriteLine($"error: unknown option '{name}'");
                parsed = ParsedOptions.Empty;
                return false;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine($"error: option '{name}' requires a value");
                parsed = ParsedOptions.Empty;
                return false;
            }

            string value = args[++index];
            if (!values.TryAdd(name, value))
            {
                error.WriteLine($"error: duplicate option '{name}'");
                parsed = ParsedOptions.Empty;
                return false;
            }
        }

        parsed = new ParsedOptions(values, flags);
        return true;
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter error)
    {
        await error.WriteLineAsync($"error: unknown command '{command}'").ConfigureAwait(false);
        return UsageError;
    }

    private static async Task WriteUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner [--version|version|doctor]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner profiles list").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge preview --profile <id|ic> --dp <path> --tp <path> [--ld <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge build --profile <id|ic> --dp <path> --tp <path> [--ld <path>] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner dp-replace preview --profile <id|ic> --ic-num <value> --base <path> --dp <path> [--ld <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile <id|ic> --ic-family <value> --ic-num <value> --base <path> --ctrlram <path> [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <id|ic> --ic-num <value> --base <path> --input <path> --source-start <n> --target-start <n> --length <n> [--output <path>] [--report <path>]").ConfigureAwait(false);
    }

    private static async Task WriteProfilesUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage: nvt_fw_combiner profiles list").ConfigureAwait(false);
    }

    private static async Task WriteStandardMergeUsageAsync(TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge preview --profile <id|ic> --dp <path> --tp <path> [--ld <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
        await output.WriteLineAsync("  nvt_fw_combiner standard-merge build --profile <id|ic> --dp <path> --tp <path> [--ld <path>] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
    }

    private static string CreateRunId(string action)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"cli-{action}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
    }

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Flags)
    {
        internal static ParsedOptions Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
    }

    private readonly record struct OutputTarget(string OutputDirectory, string FileName)
    {
        internal string FullPath => ProtectedPathGuard.CombineFullPath(OutputDirectory, FileName);
    }
}
