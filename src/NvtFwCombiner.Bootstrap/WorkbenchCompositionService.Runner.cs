using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;

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
        CompiledComposition compiledComposition,
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
            compiledComposition.DefaultOutputFileName);
        if (build)
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                ProtectedPathGuard.CombineFullPath(outputDirectory, outputFileName),
                "Output path",
                ProtectedPathGuard.CreateProtectedPaths(bindings, outputPath: null),
                nameof(outputPath));
        }

        IArtifactReader? fileReader = inputRoots.Length == 0 ? null : new FileArtifactReader(inputRoots);
        IArtifactReader reader = virtualArtifacts is { Count: > 0 }
            ? new OverlayArtifactReader(fileReader, virtualArtifacts)
            : fileReader ?? throw new InvalidOperationException("A composition requires at least one physical or virtual input artifact.");
        AtomicFileCompositionOutputWriter? writer = build
            ? new AtomicFileCompositionOutputWriter(outputDirectory, overwrite)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer, externalProcessor);
        CompositionRunRequest request = new(
            CreateWorkbenchRunId(runIdPrefix, build),
            compiledComposition,
            bindings,
            outputFileName,
            icNumberSelection: icNumberSelection);

        CompositionRunResult result = await service.PreviewOrBuildAsync(
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
