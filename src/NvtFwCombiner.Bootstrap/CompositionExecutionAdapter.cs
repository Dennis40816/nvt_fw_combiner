using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Temporary host adapter that binds immutable compiled compositions to the
/// shared Application Preview/Build service and host-owned file ports.
/// </summary>
public static partial class CompositionExecutionAdapter
{
    internal const string StandardMergeRunIdPrefix = "ui";
    internal const string GeneralMergeRunIdPrefix = "ui-merge-general";
    internal const string DpReplaceRunIdPrefix = "ui-replace-dp";
    internal const string CtrlRamReplaceRunIdPrefix = "ui-replace-ctrlram";
    internal const string GeneralReplaceRunIdPrefix = "ui-replace-general";

    internal static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(
            Environment.NewLine,
            issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }

    internal static async ValueTask<WorkbenchRunResult> RunCompiledCompositionAsync(
        string runIdPrefix,
        CompiledComposition compiledComposition,
        IReadOnlyList<InputArtifactBinding> bindings,
        string firstInputPath,
        bool build,
        string? outputPath,
        IExternalProcessor? externalProcessor,
        IcNumberSelection? icNumberSelection,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, byte[]>? virtualArtifacts = null,
        CompositionRunProgressFeed? progress = null,
        string? previewOutputFileName = null,
        TopologySelection? abMergeTopologySelection = null,
        string? automaticOutputDirectory = null,
        IReadOnlyList<ProtectedPathGuard.ProtectedPath>? additionalOutputProtectedPaths = null,
        bool outputPathUsesAutomaticName = false,
        Action<string, OutputNamingSummary?>? additionalOutputPreflight = null,
        IReadOnlyList<CompositionIssue>? advisoryIssues = null,
        GeneralAuthoringAdmissionResult? generalAdmission = null,
        ResolvedCapability? resolvedCapability = null)
    {
        CompositionRunResult result = await RunCompiledCompositionResultAsync(
            runIdPrefix,
            compiledComposition,
            bindings,
            firstInputPath,
            build,
            outputPath,
            externalProcessor,
            icNumberSelection,
            cancellationToken,
            virtualArtifacts,
            progress,
            previewOutputFileName,
            abMergeTopologySelection,
            automaticOutputDirectory,
            additionalOutputProtectedPaths,
            outputPathUsesAutomaticName,
            additionalOutputPreflight,
            advisoryIssues,
            generalAdmission,
            resolvedCapability).ConfigureAwait(false);
        return ToWorkbenchRunResult(result) with { ResolvedCapability = resolvedCapability };
    }

    /// <summary>Runs one composition and retains the typed Application result for a bounded adapter delivery phase.</summary>
    internal static async ValueTask<CompositionRunResult> RunCompiledCompositionResultAsync(
        string runIdPrefix,
        CompiledComposition compiledComposition,
        IReadOnlyList<InputArtifactBinding> bindings,
        string firstInputPath,
        bool build,
        string? outputPath,
        IExternalProcessor? externalProcessor,
        IcNumberSelection? icNumberSelection,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, byte[]>? virtualArtifacts = null,
        CompositionRunProgressFeed? progress = null,
        string? previewOutputFileName = null,
        TopologySelection? abMergeTopologySelection = null,
        string? automaticOutputDirectory = null,
        IReadOnlyList<ProtectedPathGuard.ProtectedPath>? additionalOutputProtectedPaths = null,
        bool outputPathUsesAutomaticName = false,
        Action<string, OutputNamingSummary?>? additionalOutputPreflight = null,
        IReadOnlyList<CompositionIssue>? advisoryIssues = null,
        GeneralAuthoringAdmissionResult? generalAdmission = null,
        ResolvedCapability? resolvedCapability = null)
    {
        if (outputPathUsesAutomaticName &&
            (string.IsNullOrWhiteSpace(outputPath) || previewOutputFileName is not null))
        {
            throw new ArgumentException(
                "An automatic output directory requires a selected output path and cannot be combined with a Preview output name.",
                nameof(outputPathUsesAutomaticName));
        }

        string[] inputRoots =
        [
            .. bindings
                .Where(binding => !VirtualArtifactLocator.IsVirtual(binding.ArtifactId))
                .Select(binding => Path.GetDirectoryName(binding.ArtifactId)!)
        ];
        (string outputDirectory, string outputFileName) = ResolveOutputTarget(
            firstInputPath,
            build,
            outputPath,
            compiledComposition.V2Details.OutputNamingRequirement.FileNameTemplate,
            automaticOutputDirectory);
        if (outputPathUsesAutomaticName)
        {
            // The native save dialog supplies the destination directory.  Keep the compiled
            // template as the Application request value so execution snapshots render the final
            // automatic name, rather than accidentally treating the dialog's stale suggestion as
            // an operator override.
            outputFileName = compiledComposition.V2Details.OutputNamingRequirement.FileNameTemplate;
        }
        if (previewOutputFileName is not null)
        {
            if (build ||
                string.IsNullOrWhiteSpace(previewOutputFileName) ||
                Path.GetFileName(previewOutputFileName) != previewOutputFileName ||
                previewOutputFileName.IndexOfAny(['/', '\\', ':']) >= 0)
            {
                throw new ArgumentException(
                    "A preview output name must be one plain filename and is not valid for Build.",
                    nameof(previewOutputFileName));
            }

            outputFileName = previewOutputFileName;
        }
        List<ProtectedPathGuard.ProtectedPath> outputProtectedPaths =
            ProtectedPathGuard.CreateProtectedPaths(bindings, outputPath: null);
        if (additionalOutputProtectedPaths is not null)
        {
            outputProtectedPaths.AddRange(additionalOutputProtectedPaths);
        }

        if (build && !outputPathUsesAutomaticName)
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                ProtectedPathGuard.CombineFullPath(outputDirectory, outputFileName),
                "Output path",
                outputProtectedPaths,
                nameof(outputPath));
        }

        IArtifactReader? fileReader = inputRoots.Length == 0 ? null : new FileArtifactReader(inputRoots);
        IArtifactReader reader = virtualArtifacts is { Count: > 0 }
            ? new OverlayArtifactReader(fileReader, virtualArtifacts)
            : fileReader ?? throw new InvalidOperationException("A composition requires at least one physical or virtual input artifact.");
        // Composition outputs replace an unrelated existing target atomically.  The
        // protected-path guard above remains the hard boundary: an input alias is
        // never an eligible output target.
        ICompositionOutputWriter? writer = build
            ? new ProtectedCompositionOutputWriter(
                new AtomicFileCompositionOutputWriter(outputDirectory, overwrite: true),
                outputDirectory,
                outputProtectedPaths,
                additionalOutputPreflight)
            : null;
        CompositionRunService service = new(reader, new SystemClock(), writer, externalProcessor);
        resolvedCapability = CanonicalCapabilityResolution.ResolveCanonicalCapabilityForRun(
            compiledComposition,
            resolvedCapability);
        CompositionRunRequest request = new(
            CreateWorkbenchRunId(runIdPrefix, build),
            compiledComposition,
            bindings,
            outputFileName,
            icNumberSelection: icNumberSelection,
            outputFileNameIsOverride: (outputPath is not null && !outputPathUsesAutomaticName) ||
                previewOutputFileName is not null,
            abMergeTopologySelection: abMergeTopologySelection,
            advisoryIssues: advisoryIssues,
            generalAdmission: generalAdmission?.ToSummary(),
            resolvedCapability: resolvedCapability);

        CompositionRunResult result = progress is null
            ? await service.PreviewOrBuildAsync(request, build, cancellationToken).ConfigureAwait(false)
            : await service.PreviewOrBuildAsync(request, build, progress, cancellationToken).ConfigureAwait(false);
        return result;
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

    private static (string Directory, string FileName) ResolveOutputTarget(
        string firstInputPath,
        bool build,
        string? outputPath,
        string defaultOutputFileName,
        string? automaticOutputDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string automaticDirectory = string.IsNullOrWhiteSpace(automaticOutputDirectory)
                ? Path.GetDirectoryName(firstInputPath)!
                : Path.GetFullPath(automaticOutputDirectory);
            return (automaticDirectory, defaultOutputFileName);
        }

        if (!build)
        {
            throw new ArgumentException(
                "Preview does not accept an output file path.",
                nameof(outputPath));
        }

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException(
                "Output path must include a directory and file name.",
                nameof(outputPath))
            : (directory, fileName);
    }
}
