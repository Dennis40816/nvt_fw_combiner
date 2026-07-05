using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;

    private static readonly Dictionary<string, string> FixedInputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            ["reference-base"] = "--base",
            ["dp-replacement"] = "--dp",
            ["ld-replacement"] = "--ld",
            ["ctrlram-replacement"] = "--ctrlram",
            ["replacement-input"] = "--input",
        };

    internal static async Task<int> RunAsync(
        string command,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(command, output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown {command} command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] valueOptions = [
            "--profile",
            "--ic-family",
            "--ic-num",
            "--base",
            "--dp",
            "--ld",
            "--ctrlram",
            "--input",
            "--source-start",
            "--target-start",
            "--length",
            "--mapping",
            "--output",
            "--report",
        ];
        string[] flagOptions = action == "build" ? ["--overwrite"] : [];
        string[] repeatableValueOptions = command switch
        {
            "ctrlram-replace" => ["--ctrlram"],
            "general-replace" => ["--mapping"],
            _ => [],
        };
        if (!TryParseOptions(args[1..], valueOptions, repeatableValueOptions, flagOptions, error, out ParsedOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (command == "dp-replace" &&
            TryResolveNt51950FamilyDpReplaceIc(profileSelector, out string? dpWorkbenchIcId))
        {
            return await RunWorkbenchDpReplaceAsync(
                    action,
                    dpWorkbenchIcId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!TryFindReplaceProfile(command, profileSelector, out CompositionProfileDefinition? selectedProfile))
        {
            return command switch
            {
                "ctrlram-replace" => await RunWorkbenchCtrlRamReplaceAsync(
                        action,
                        profileSelector,
                        options,
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false),
                "general-replace" => await RunWorkbenchGeneralReplaceAsync(
                        action,
                        profileSelector,
                        options,
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false),
                _ => await UnknownReplaceProfileAsync(command, profileSelector, error).ConfigureAwait(false),
            };
        }

        if (command == "ctrlram-replace" && options.GetValues("--ctrlram").Count > 1)
        {
            await error.WriteLineAsync(
                    "error: built-in CtrlRAM profiles accept one --ctrlram path; use --profile <IC> with repeated --ctrlram <slot-id=path> for multi-region replacement.")
                .ConfigureAwait(false);
            return UsageError;
        }

        if (!TryCreateIcNumberSelection(selectedProfile, options, error, out IcNumberSelection? icNumberSelection))
        {
            return UsageError;
        }

        if (!TryCompileProfile(selectedProfile, options, error, out ProfileCompileResult compile))
        {
            return UsageError;
        }

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

        string[] inputRoots = [.. bindings.Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        var reader = new FileArtifactReader(inputRoots);
        AtomicFileCompositionOutputWriter? writer = action == "build"
            ? new AtomicFileCompositionOutputWriter(outputTarget.OutputDirectory, options.Flags.Contains("--overwrite"))
            : null;
        var service = new CompositionRunService(reader, new SystemClock(), writer, ExternalProcessorFactory.CreateOrNull());
        var request = new CompositionRunRequest(
            CreateRunId(command, action),
            ToRunProfile(selectedProfile),
            plan,
            bindings,
            outputTarget.FileName,
            icNumberSelection: icNumberSelection);

        CompositionRunResult result = action == "preview"
            ? await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false)
            : await BuildWithInternalPreviewAsync(service, request, cancellationToken).ConfigureAwait(false);
        await WriteReportFileIfRequestedAsync(result, options, bindings, outputTarget, action == "build", output, cancellationToken)
            .ConfigureAwait(false);
        await PrintRunResultAsync(result, output, error).ConfigureAwait(false);
        return result.Status == CompositionExecutionStatus.Succeeded ? Success : CompositionFailed;
    }

    private static async Task<int> UnknownReplaceProfileAsync(
        string command,
        string profileSelector,
        TextWriter error)
    {
        await error.WriteLineAsync($"error: unknown {command} profile '{profileSelector}'").ConfigureAwait(false);
        return UsageError;
    }

    private static bool TryCompileProfile(
        CompositionProfileDefinition profile,
        ParsedOptions options,
        TextWriter error,
        out ProfileCompileResult compile)
    {
        if (profile.ExperienceId != "general-replace")
        {
            compile = CompositionProfileCompiler.Compile(profile, []);
            return true;
        }

        if (!TryCreateGeneralMapping(options, error, out ExplicitMapping? mapping, out AddressSpace? requestSpace))
        {
            compile = ProfileCompileResult.Failed([]);
            return false;
        }

        compile = CompositionProfileCompiler.Compile(profile, [mapping], [requestSpace]);
        return true;
    }

    private static bool TryCreateGeneralMapping(
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out ExplicitMapping? mapping,
        [NotNullWhen(true)] out AddressSpace? requestSpace)
    {
        mapping = null;
        requestSpace = null;
        if (!RequireOption(options, "--input", error, out string? inputPath) ||
            !RequireLong(options, "--source-start", error, out long sourceStart) ||
            !RequireLong(options, "--target-start", error, out long targetStart) ||
            !RequireLong(options, "--length", error, out long length))
        {
            return false;
        }

        if (length <= 0)
        {
            error.WriteLine("error: --length must be positive");
            return false;
        }

        string fullPath = Path.GetFullPath(inputPath);
        long declaredLength = File.Exists(fullPath)
            ? new FileInfo(fullPath).Length
            : checked(sourceStart + length);
        requestSpace = new AddressSpace("replacement-input", declaredLength, AddressSpaceMutability.Immutable);
        mapping = new ExplicitMapping(
            "replace-general",
            100,
            ExplicitMappingOperationKind.ReplaceRange,
            "replacement-input",
            new ByteRange(sourceStart, length),
            "output-image",
            new ByteRange(targetStart, length),
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace synthetic explicit range.",
            targetRegionId: null);
        return true;
    }

    private static bool TryCreateIcNumberSelection(
        CompositionProfileDefinition profile,
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out IcNumberSelection? selection)
    {
        selection = null;
        if (profile.IcNumberInputMode is null)
        {
            error.WriteLine($"error: replace profile '{profile.ProfileId}' does not declare an IC num input mode");
            return false;
        }

        if (!RequireOption(options, "--ic-num", error, out string? icNumber))
        {
            return false;
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.SingleSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, [icNumber]);
            return true;
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.NumericSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            if (!int.TryParse(icNumber, out int parsedIcNumber) || parsedIcNumber <= 0)
            {
                error.WriteLine("error: numeric --ic-num must be a positive integer");
                return false;
            }

            selection = new IcNumberSelection(IcNumberInputMode.NumericSelector, [icNumber]);
            return true;
        }

        if (!RequireOption(options, "--ic-family", error, out string? icFamily))
        {
            return false;
        }

        selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, [icFamily, icNumber]);
        return true;
    }

    private static bool TryCreateBindings(
        CompositionPlan plan,
        ParsedOptions options,
        TextWriter error,
        out IReadOnlyList<InputArtifactBinding> bindings)
    {
        List<InputArtifactBinding> items = [];
        HashSet<string> usedInputOptions = new(StringComparer.Ordinal);
        foreach (string addressSpaceId in plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal))
        {
            if (!FixedInputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: profile requires unsupported address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            if (!RequireOption(options, optionName, error, out string? path))
            {
                bindings = [];
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            items.Add(new InputArtifactBinding(addressSpaceId, addressSpaceId, fullPath));
            _ = usedInputOptions.Add(optionName);
        }

        foreach (string optionName in FixedInputOptionsByAddressSpace.Values.Order(StringComparer.Ordinal))
        {
            if (options.Values.ContainsKey(optionName) && !usedInputOptions.Contains(optionName))
            {
                error.WriteLine($"error: option '{optionName}' is not used by the selected replace profile");
                bindings = [];
                return false;
            }
        }

        bindings = items;
        return true;
    }

    private static bool TryFindReplaceProfile(
        string command,
        string selector,
        [NotNullWhen(true)] out CompositionProfileDefinition? profile)
    {
        string normalized = selector.Trim();
        profile = BuiltInReplaceProfiles.All.FirstOrDefault(candidate =>
            string.Equals(candidate.ExperienceId, command, StringComparison.Ordinal) &&
            (string.Equals(candidate.ProfileId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.IcId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetIcNumber(candidate.IcId), normalized, StringComparison.OrdinalIgnoreCase)));
        return profile is not null;
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

    private static async Task PrintRunResultAsync(
        CompositionRunResult result,
        TextWriter output,
        TextWriter error)
    {
        CompositionRunReport report = result.Report;
        await output.WriteLineAsync($"Status: {result.Status}").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {report.ProfileId} ({report.IcId})").ConfigureAwait(false);
        await output.WriteLineAsync($"Experience: {report.ExperienceId}").ConfigureAwait(false);
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

    private static bool TryParseOptions(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> repeatableValueOptions,
        IReadOnlyCollection<string> flagOptions,
        TextWriter error,
        out ParsedOptions parsed)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> multiValues = new(StringComparer.Ordinal);
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
            if (repeatableValueOptions.Contains(name))
            {
                if (!multiValues.TryGetValue(name, out List<string>? items))
                {
                    items = [];
                    multiValues.Add(name, items);
                }

                items.Add(value);
                _ = values.TryAdd(name, value);
                continue;
            }

            if (!values.TryAdd(name, value))
            {
                error.WriteLine($"error: duplicate option '{name}'");
                parsed = ParsedOptions.Empty;
                return false;
            }
        }

        parsed = new ParsedOptions(values, multiValues, flags);
        return true;
    }

    private static bool RequireOption(
        ParsedOptions options,
        string optionName,
        TextWriter error,
        [NotNullWhen(true)] out string? value)
    {
        if (options.Values.TryGetValue(optionName, out value))
        {
            return true;
        }

        error.WriteLine($"error: {optionName} is required");
        return false;
    }

    private static bool RequireLong(
        ParsedOptions options,
        string optionName,
        TextWriter error,
        out long value)
    {
        value = 0;
        if (!RequireOption(options, optionName, error, out string? text))
        {
            return false;
        }

        string trimmed = text.Trim();
        bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(
                trimmed[2..],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out value)
            : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        if (parsed && value >= 0)
        {
            return true;
        }

        error.WriteLine($"error: {optionName} must be a non-negative integer");
        return false;
    }

    private static string GetIcNumber(string icId)
    {
        return icId.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? icId[2..]
            : icId;
    }

    private static string FormatRange(ByteRange range)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})");
    }

    private static string CreateRunId(string command, string action)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"cli-{command}-{action}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
    }

    private static async Task WriteUsageAsync(string command, TextWriter output)
    {
        await output.WriteLineAsync("Usage:").ConfigureAwait(false);
        switch (command)
        {
            case "dp-replace":
                await output.WriteLineAsync("  nvt_fw_combiner dp-replace preview --profile <id|ic> --ic-num <value> --base <path> --dp <path> [--ld <path>] [--output <path>] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner dp-replace build --profile <id|ic> --ic-num <value> --base <path> --dp <path> [--ld <path>] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                break;
            case "ctrlram-replace":
                await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace build --profile <ic> --ic-num <value> --base <path> --ctrlram <slot-id=path> [--ctrlram <slot-id=path> ...] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner ctrlram-replace preview --profile synthetic-ctrlram-replace --ic-family <value> --ic-num <value> --base <path> --ctrlram <path> [--report <path>]").ConfigureAwait(false);
                break;
            case "general-replace":
                await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <id|ic> --ic-num <value> --base <path> --input <path> --source-start <n> --target-start <n> --length <n> [--output <path>] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner general-replace build --profile <id|ic> --ic-num <value> --base <path> --input <path> --source-start <n> --target-start <n> --length <n> [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner general-replace preview --profile <ic> --ic-num <value> --base <path> --mapping <target-start+length=path> [--mapping <target-start+length=path> ...] [--report <path>]").ConfigureAwait(false);
                await output.WriteLineAsync("  nvt_fw_combiner general-replace build --profile <ic> --ic-num <value> --base <path> --mapping <target-start+length=path> [--mapping <target-start+length=path> ...] [--output <path>] [--report <path>] [--overwrite]").ConfigureAwait(false);
                break;
            default:
                await output.WriteLineAsync("  nvt_fw_combiner <dp-replace|ctrlram-replace|general-replace> <preview|build> [options]").ConfigureAwait(false);
                break;
        }
    }

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyDictionary<string, List<string>> MultiValues,
        IReadOnlySet<string> Flags)
    {
        internal static ParsedOptions Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, List<string>>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

        internal List<string> GetValues(string optionName)
        {
            return MultiValues.TryGetValue(optionName, out List<string>? values)
                ? values
                : Values.TryGetValue(optionName, out string? value)
                    ? [value]
                    : [];
        }
    }

    private readonly record struct OutputTarget(string OutputDirectory, string FileName)
    {
        internal string FullPath => ProtectedPathGuard.CombineFullPath(OutputDirectory, FileName);
    }
}
