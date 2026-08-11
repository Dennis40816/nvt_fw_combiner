using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Creates path-protected atomic filesystem destinations for composition output.</summary>
internal sealed class ProtectedCompositionDestinationProvider :
    ICompositionExecutionDestinationProvider
{
    public CompositionExecutionDestination Prepare(
        CompositionExecutionDestinationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Build)
        {
            return new CompositionExecutionDestination(null, null);
        }

        List<ProtectedPathGuard.ProtectedPath> protectedPaths =
            ProtectedPathGuard.CreateProtectedPaths(request.Bindings, outputPath: null);
        protectedPaths.AddRange(request.AdditionalProtectedPaths.Select(static path =>
            new ProtectedPathGuard.ProtectedPath(path.Path, path.Description)));
        if (!request.OutputPathUsesAutomaticName)
        {
            ProtectedPathGuard.EnsureDoesNotAlias(
                ProtectedPathGuard.CombineFullPath(
                    request.OutputDirectory,
                    request.OutputFileName),
                "Output path",
                protectedPaths,
                nameof(request.OutputFileName));
        }

        ICompositionOutputWriter outputWriter = new ProtectedCompositionOutputWriter(
            new AtomicFileCompositionOutputWriter(
                request.OutputDirectory,
                overwrite: true),
            request.OutputDirectory,
            protectedPaths);
        ICompositionDeliveryWriter? deliveryWriter = request.AdditionalDelivery is null
            ? null
            : new ProtectedCompositionDeliveryWriter(
                request.OutputDirectory,
                request.AdditionalDelivery,
                protectedPaths);
        return new CompositionExecutionDestination(outputWriter, deliveryWriter);
    }

    private sealed class ProtectedCompositionOutputWriter :
        ICompositionOutputWriter,
        ICompositionOutputCommitPreflight
    {
        private readonly ICompositionOutputWriter _inner;
        private readonly string _outputDirectory;
        private readonly IReadOnlyList<ProtectedPathGuard.ProtectedPath> _protectedPaths;

        internal ProtectedCompositionOutputWriter(
            ICompositionOutputWriter inner,
            string outputDirectory,
            IEnumerable<ProtectedPathGuard.ProtectedPath> protectedPaths)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
            ArgumentNullException.ThrowIfNull(protectedPaths);
            _inner = inner;
            _outputDirectory = Path.GetFullPath(outputDirectory);
            _protectedPaths = [.. protectedPaths];
        }

        public void EnsureCanCommit(
            string fileName,
            OutputNamingSummary? outputNaming)
        {
            _ = EnsurePrimaryOutputPath(fileName);
        }

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            _ = EnsurePrimaryOutputPath(fileName);
            return _inner.CommitAsync(fileName, outputBytes, cancellationToken);
        }

        private string EnsurePrimaryOutputPath(string fileName)
        {
            string outputPath = ProtectedPathGuard.CombineFullPath(
                _outputDirectory,
                fileName);
            ProtectedPathGuard.EnsureDoesNotAlias(
                outputPath,
                "Output path",
                _protectedPaths,
                nameof(fileName));
            return outputPath;
        }
    }

    private sealed class ProtectedCompositionDeliveryWriter :
        ICompositionDeliveryWriter
    {
        private readonly string _primaryOutputDirectory;
        private readonly IReadOnlyList<ProtectedPathGuard.ProtectedPath> _protectedPaths;
        private readonly CompositionExecutionDeliveryTarget _selection;
        private string? _resolvedOutputPath;

        internal ProtectedCompositionDeliveryWriter(
            string primaryOutputDirectory,
            CompositionExecutionDeliveryTarget selection,
            IEnumerable<ProtectedPathGuard.ProtectedPath> protectedPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(primaryOutputDirectory);
            ArgumentNullException.ThrowIfNull(selection);
            ArgumentNullException.ThrowIfNull(protectedPaths);
            ArgumentException.ThrowIfNullOrWhiteSpace(selection.DeliveryKind);
            ArgumentException.ThrowIfNullOrWhiteSpace(selection.OutputPath);
            _primaryOutputDirectory = Path.GetFullPath(primaryOutputDirectory);
            _selection = selection;
            _protectedPaths =
            [
                .. selection.ProtectedPaths.Select(static path =>
                    new ProtectedPathGuard.ProtectedPath(
                        path.Path,
                        path.Description)),
                .. protectedPaths,
            ];
        }

        public string DeliveryKind => _selection.DeliveryKind;

        public string EnsureCanCommit(
            string primaryOutputFileName,
            string suggestedDeliveryFileName)
        {
            string primaryOutputPath = ProtectedPathGuard.CombineFullPath(
                _primaryOutputDirectory,
                primaryOutputFileName);
            string selectedOutputPath = Path.GetFullPath(_selection.OutputPath);
            string deliveryOutputPath = !_selection.UsesAutomaticFileName
                ? selectedOutputPath
                : Path.Combine(
                    Path.GetDirectoryName(selectedOutputPath) ?? throw new ArgumentException(
                        "An automatic additional delivery requires one concrete destination directory."),
                    suggestedDeliveryFileName);
            string outputFileName = Path.GetFileName(deliveryOutputPath);
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(deliveryOutputPath)) ||
                string.IsNullOrWhiteSpace(outputFileName))
            {
                throw new ArgumentException(
                    "An additional delivery requires one concrete output file path.");
            }

            ProtectedPathGuard.EnsureDoesNotAlias(
                deliveryOutputPath,
                StringComparer.Ordinal.Equals(
                    _selection.DeliveryKind,
                    CompiledAdditionalDelivery.AbAFlashCodeKind)
                        ? "A FlashCode output path"
                        : "Additional delivery output path",
                [
                    .. _protectedPaths,
                    new ProtectedPathGuard.ProtectedPath(
                        primaryOutputPath,
                        "Primary composition output"),
                ],
                nameof(suggestedDeliveryFileName));
            _resolvedOutputPath = deliveryOutputPath;
            return outputFileName;
        }

        public ValueTask<string> CommitAsync(
            string deliveryFileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            string outputPath = _resolvedOutputPath ?? throw new InvalidOperationException(
                "Additional delivery must pass output-target preflight before commit.");
            if (!StringComparer.Ordinal.Equals(
                    Path.GetFileName(outputPath),
                    deliveryFileName))
            {
                throw new InvalidOperationException(
                    "Additional delivery filename changed after output-target preflight.");
            }

            var writer = new AtomicFileCompositionOutputWriter(
                Path.GetDirectoryName(outputPath)!,
                overwrite: true);
            return writer.CommitAsync(
                deliveryFileName,
                outputBytes,
                cancellationToken);
        }
    }
}
