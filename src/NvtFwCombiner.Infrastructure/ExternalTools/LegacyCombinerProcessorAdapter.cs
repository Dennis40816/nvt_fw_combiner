// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Compatibility selection for the two existing public processor entry points.</summary>
internal enum LegacyCombinerInvocationMode
{
    Automatic,
    Manifest,
    CompiledPostbuild,
}

/// <summary>Legacy argv, BIN projection and result interpretation; never admits transformed bytes.</summary>
internal sealed partial class LegacyCombinerProcessorAdapter : IStagedExternalProcessorAdapter
{
    internal const string ProcessorKind = "legacy-combiner";
    private const string WorkFileName = "work.bin";
    private const string OutputFileName = "output.bin";
    private const string BinDirectoryName = "BIN";
    private const string OutputDirectoryName = "output";
    private const string MapFileName = "map.txt";
    private const int LegacyMaximumArgumentPathLength = 259;

    private readonly LegacyCombinerInvocationMode _mode;
    private readonly Dictionary<string, ExternalCombinerInvocationProfile> _invocationsByProcessorId;

    internal LegacyCombinerProcessorAdapter(
        IEnumerable<ExternalCombinerInvocationProfile> invocationProfiles,
        LegacyCombinerInvocationMode mode = LegacyCombinerInvocationMode.Automatic)
    {
        ArgumentNullException.ThrowIfNull(invocationProfiles);
        _mode = mode;
        _invocationsByProcessorId = CreateInvocationIndex(invocationProfiles);
    }

    public string Kind => ProcessorKind;

    public CompositionIssue? ValidateRequest(ExternalProcessorRequest request)
    {
        ExternalProcessorProtocolPlan? plan = request.ProtocolPlan;
        return _mode == LegacyCombinerInvocationMode.Manifest ||
            (_mode == LegacyCombinerInvocationMode.Automatic && plan is null) ||
            StringComparer.Ordinal.Equals(plan?.ProtocolId, LegacyCombinerPostbuildPlanCompiler.ProtocolId)
            ? null
            : _mode == LegacyCombinerInvocationMode.Automatic
            ? new CompositionIssue(
                "external-processor.protocol.unsupported",
                $"No external-processor adapter is registered for compiled protocol '{plan!.ProtocolId}'.")
            : new CompositionIssue(
                "legacy-combiner.compiled-plan.missing",
                "Legacy combiner execution requires the exact compiled protocol plan.");
    }

    public CompositionIssue? ValidatePaths(ExternalProcessorRequest request, string runDirectory)
    {
        if (!UsesPostbuild(request))
        {
            return null;
        }

        ExternalProcessorProtocolPlan commandPlan = request.ProtocolPlan!;
        string firmwarePath = Path.Combine(runDirectory, OutputDirectoryName, commandPlan.TargetFileName);
        string binDirectory = Path.Combine(runDirectory, BinDirectoryName);
        foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)
        {
            foreach (string argument in ResolveProtocolArguments(command, firmwarePath, binDirectory))
            {
                if (Path.IsPathFullyQualified(argument) && argument.Length > LegacyMaximumArgumentPathLength)
                {
                    return new CompositionIssue(
                        "external-tool.argument-path.too-long",
                        $"Legacy combiner command '{command.CommandId}' has an argument path of {argument.Length} characters; the maximum is {LegacyMaximumArgumentPathLength}.");
                }
            }
        }

        return null;
    }

    public bool TryPrepare(
        ExternalProcessorRequest request,
        ExternalCombinerToolManifest manifest,
        out StagedExternalProcessorInvocation? invocation,
        out CompositionIssue? issue)
    {
        if (UsesPostbuild(request))
        {
            string firmwarePath = Path.Combine(OutputDirectoryName, request.ProtocolPlan!.TargetFileName);
            invocation = new StagedExternalProcessorInvocation(firmwarePath, firmwarePath, [OutputDirectoryName, BinDirectoryName]);
            issue = null;
            return true;
        }

        if (!TryResolveInvocation(request, manifest, out ExternalCombinerInvocationProfile resolved, out issue))
        {
            invocation = null;
            return false;
        }

        invocation = new StagedExternalProcessorInvocation(
            WorkFileName,
            string.Equals(resolved.InputMode, "input-output-file", StringComparison.Ordinal) ? OutputFileName : WorkFileName,
            [],
            resolved);
        return true;
    }

    public ValueTask<CompositionIssue?> ExecuteAsync(
        StagedExternalProcessorHost.Session session,
        CancellationToken cancellationToken)
    {
        return UsesPostbuild(session.Request)
            ? ExecutePostbuildAsync(session, cancellationToken)
            : ExecuteManifestAsync(session, cancellationToken);
    }

    public string DescribeMissingOutput(ExternalProcessorRequest request)
    {
        return UsesPostbuild(request)
            ? "External processor did not leave the staged firmware file."
            : "External processor did not produce the expected output file.";
    }

    public string DescribeWriteViolation(ExternalProcessorRequest request, ChangedRangeVerdict verdict)
    {
        return UsesPostbuild(request)
            ? $"External processor changed bytes outside declared write ranges: {FormatRanges(verdict.ViolatingRanges)}."
            : "External processor changed bytes outside declared write ranges.";
    }

    public string DescribeProcessFailure(string? commandId, ExternalProcessResult result)
    {
        return commandId is null
            ? $"External processor exited with code {result.ExitCode}."
            : $"External processor command '{commandId}' exited with code {result.ExitCode}. {FormatProcessOutput(result)}";
    }

    public bool RetainCommandsOnException(ExternalProcessorRequest request)
    {
        return UsesPostbuild(request);
    }

    private bool UsesPostbuild(ExternalProcessorRequest request)
    {
        return _mode == LegacyCombinerInvocationMode.CompiledPostbuild ||
            (_mode == LegacyCombinerInvocationMode.Automatic && request.ProtocolPlan is not null);
    }
}
