using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string StandardMergeRunIdPrefix = "ui";
    private const string GeneralMergeRunIdPrefix = "ui-merge-general";
    private const string DpReplaceRunIdPrefix = "ui-replace-dp";
    private const string CtrlRamReplaceRunIdPrefix = "ui-replace-ctrlram";
    private const string GeneralReplaceRunIdPrefix = "ui-replace-general";

    private static async ValueTask<WorkbenchRunResult> RunCompiledCompositionAsync(
        string runIdPrefix,
        CompositionProfileDefinition profile,
        CompositionPlan plan,
        IReadOnlyList<InputArtifactBinding> bindings,
        string firstInputPath,
        bool build,
        string? outputPath,
        IExternalProcessor? externalProcessor,
        IcNumberSelection? icNumberSelection,
        bool overwrite,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, byte[]>? virtualArtifacts = null)
    {
        string[] inputRoots =
        [
            .. bindings
                .Where(binding => !VirtualArtifactLocator.IsVirtual(binding.ArtifactId))
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        (string outputDirectory, string outputFileName) = ResolveOutputTarget(
            firstInputPath,
            build,
            outputPath,
            profile.DefaultOutputFileName);
        if (build)
        {
            ProtectedPathGuard.EnsureOutputDoesNotAliasInputs(
                ProtectedPathGuard.CombineFullPath(outputDirectory, outputFileName),
                bindings,
                nameof(outputPath));
        }

        var fileReader = new FileArtifactReader(inputRoots);
        IArtifactReader reader = virtualArtifacts is { Count: > 0 }
            ? new OverlayArtifactReader(fileReader, virtualArtifacts)
            : fileReader;
        AtomicFileCompositionOutputWriter? writer = build
            ? new AtomicFileCompositionOutputWriter(outputDirectory, overwrite)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer, externalProcessor);
        CompositionRunRequest request = new(
            CreateWorkbenchRunId(runIdPrefix, build),
            ToRunProfile(profile),
            plan,
            bindings,
            outputFileName,
            icNumberSelection: icNumberSelection);

        CompositionRunResult result = await CompositionRunExecutionSupport.PreviewOrBuildAsync(
                service,
                request,
                build,
                cancellationToken)
            .ConfigureAwait(false);
        return ToWorkbenchRunResult(result);
    }

    private static string CreateWorkbenchRunId(string prefix, bool build)
    {
        return CreateWorkbenchRunId(prefix, build, DateTimeOffset.UtcNow);
    }

    private static string CreateWorkbenchRunId(string prefix, bool build, DateTimeOffset timestamp)
    {
        string suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        return $"{prefix}-{FormatWorkbenchRunAction(build)}-{FormatWorkbenchRunTimestamp(timestamp)}-{suffix}";
    }

    private static string CreateWorkbenchReportRunId(string prefix, bool build, DateTimeOffset timestamp)
    {
        return $"{prefix}-{FormatWorkbenchRunAction(build)}-{FormatWorkbenchRunTimestamp(timestamp)}";
    }

    private static string GetReplaceRunIdPrefix(string replaceMode)
    {
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => DpReplaceRunIdPrefix,
            WorkbenchReplaceModes.CtrlRam => CtrlRamReplaceRunIdPrefix,
            WorkbenchReplaceModes.General => GeneralReplaceRunIdPrefix,
            _ => FormattableString.Invariant($"ui-replace-{replaceMode.ToLowerInvariant()}"),
        };
    }

    private static string FormatWorkbenchRunAction(bool build)
    {
        return build ? "build" : "preview";
    }

    private static string FormatWorkbenchRunTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    }
}
