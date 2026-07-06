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
        CancellationToken cancellationToken)
    {
        string[] inputRoots =
        [
            .. bindings
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

        FileArtifactReader reader = new(inputRoots);
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

        CompositionRunResult result = await PreviewOrBuildAsync(
                service,
                request,
                build,
                cancellationToken)
            .ConfigureAwait(false);
        return ToWorkbenchRunResult(result);
    }

    private static string CreateWorkbenchRunId(string prefix, bool build)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        return $"{prefix}-{(build ? "build" : "preview")}-{timestamp.ToString(CultureInfo.InvariantCulture)}-{suffix}";
    }

    private static async ValueTask<CompositionRunResult> PreviewOrBuildAsync(
        CompositionRunService service,
        CompositionRunRequest request,
        bool build,
        CancellationToken cancellationToken)
    {
        if (!build)
        {
            return await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        }

        CompositionRunResult preview = await service.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
        return preview.Status == CompositionExecutionStatus.Succeeded
            ? await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), cancellationToken)
                .ConfigureAwait(false)
            : preview;
    }
}
