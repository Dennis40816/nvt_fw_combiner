using NvtFwCombiner.Application.Ports;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>One immutable accepted source artifact staged into an output bundle.</summary>
internal sealed class AtomicBundleArtifact
{
    internal AtomicBundleArtifact(
        string bindingId,
        string originalFileName,
        string acceptedIdentity,
        ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedIdentity);
        BindingId = bindingId;
        OriginalFileName = originalFileName;
        AcceptedIdentity = acceptedIdentity;
        Bytes = bytes.ToArray();
    }

    internal AtomicBundleArtifact(string originalFileName, ReadOnlySpan<byte> bytes)
        : this(originalFileName, originalFileName, originalFileName, bytes)
    {
    }

    internal string BindingId { get; }

    internal string OriginalFileName { get; }

    internal string AcceptedIdentity { get; }

    internal ReadOnlyMemory<byte> Bytes { get; }
}

/// <summary>Path-free metadata for one dynamic artifact admitted before execution.</summary>
internal sealed record AtomicBundlePlannedArtifact(
    string Role,
    string BindingId,
    string SuggestedFileName);

/// <summary>Writes one file into a host-owned bundle staging directory.</summary>
internal interface IAtomicBundleFileWriter
{
    ValueTask WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken);
}

/// <summary>Stages a complete output bundle beside its destination and promotes one directory atomically.</summary>
internal sealed class AtomicBundleCompositionOutputWriter :
    ICompositionOutputBundleWriter,
    ICompositionOutputCommitPreflight
{
    private const int MaximumPromotionAttempts = 1000;
    private readonly IReadOnlyList<AtomicBundleArtifact> _artifacts;
    private readonly IReadOnlyList<AtomicBundlePlannedArtifact> _additionalArtifacts;
    private readonly string _folderName;
    private readonly IAtomicBundleFileWriter _fileWriter;
    private readonly string _parentDirectory;

    internal AtomicBundleCompositionOutputWriter(
        string parentDirectory,
        string folderName,
        IEnumerable<AtomicBundleArtifact> artifacts,
        IAtomicBundleFileWriter? fileWriter = null,
        IEnumerable<AtomicBundlePlannedArtifact>? additionalArtifacts = null)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _parentDirectory = FileSystemPathGuard.ResolveExistingRoot(parentDirectory);
        AtomicBundlePathRules.EnsureWindowsName(
            folderName,
            "Bundle folder name",
            nameof(folderName));
        _folderName = folderName;
        _artifacts = [.. artifacts];
        _additionalArtifacts = [.. additionalArtifacts ?? []];
        _fileWriter = fileWriter ?? new PhysicalAtomicBundleFileWriter();
    }

    public void EnsureCanCommit(string fileName, OutputNamingSummary? outputNaming)
    {
        _ = outputNaming;
        _ = CreateArtifactNames(fileName);
        _ = ResolveCandidatePath(suffix: 1);
    }

    public async ValueTask<CompositionOutputCommitReceipt> CommitAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken)
    {
        return await CommitBundleAsync(fileName, outputBytes, [], cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<CompositionOutputCommitReceipt> CommitBundleAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        IReadOnlyList<CompositionOutputBundleCommitArtifact> additionalArtifacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(additionalArtifacts);
        ValidateAdditionalArtifacts(additionalArtifacts);
        List<string> artifactNames = CreateArtifactNames(fileName);
        int additionalCount = additionalArtifacts.Count;
        int suffix = FindFirstAvailableSuffix();
        string stagingDirectory = Path.Combine(
            _parentDirectory,
            $".{_folderName}.{Guid.NewGuid():N}.staging");
        AtomicBundlePathRules.EnsureSupportedPathLength(
            stagingDirectory,
            "Bundle staging directory");
        ValidateChildPaths(stagingDirectory, fileName, artifactNames);
        _ = Directory.CreateDirectory(stagingDirectory);
        try
        {
            await _fileWriter.WriteAsync(
                    Path.Combine(stagingDirectory, fileName),
                    outputBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            for (int index = 0; index < additionalCount; index++)
            {
                await _fileWriter.WriteAsync(
                        Path.Combine(stagingDirectory, artifactNames[index]),
                        additionalArtifacts[index].Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            for (int index = 0; index < _artifacts.Count; index++)
            {
                await _fileWriter.WriteAsync(
                        Path.Combine(stagingDirectory, artifactNames[additionalCount + index]),
                        _artifacts[index].Bytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            while (suffix <= MaximumPromotionAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destinationDirectory = ResolveCandidatePath(suffix);
                ValidateChildPaths(destinationDirectory, fileName, artifactNames);
                try
                {
                    Directory.Move(stagingDirectory, destinationDirectory);
                    string outputPath = Path.Combine(destinationDirectory, fileName);
                    List<CompositionOutputBundleArtifactReceipt> receipts =
                    [
                        CreateReceipt("output", null, fileName, outputBytes.Span),
                    ];
                    for (int index = 0; index < additionalCount; index++)
                    {
                        receipts.Add(CreateReceipt(
                            additionalArtifacts[index].Role,
                            additionalArtifacts[index].BindingId,
                            artifactNames[index],
                            additionalArtifacts[index].Bytes.Span));
                    }

                    for (int index = 0; index < _artifacts.Count; index++)
                    {
                        receipts.Add(CreateReceipt(
                            "source",
                            _artifacts[index].BindingId,
                            artifactNames[additionalCount + index],
                            _artifacts[index].Bytes.Span));
                    }

                    return new CompositionOutputCommitReceipt(
                        outputPath,
                        fileName,
                        outputBytes.Length,
                        ToSha256(outputBytes.Span),
                        new CompositionOutputBundleCommitReceipt(
                            destinationDirectory,
                            receipts));
                }
                catch (IOException) when (DestinationExists(destinationDirectory))
                {
                    suffix++;
                }
            }

            throw new IOException(
                $"Bundle destination remained occupied after {MaximumPromotionAttempts} attempts.");
        }
        catch (Exception exception)
        {
            try
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
            catch (Exception cleanupException)
            {
                exception.Data["BundleStagingCleanupFailure"] = cleanupException;
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static CompositionOutputBundleArtifactReceipt CreateReceipt(
        string role,
        string? bindingId,
        string deliveredFileName,
        ReadOnlySpan<byte> bytes)
    {
        return new CompositionOutputBundleArtifactReceipt(
            role,
            bindingId,
            deliveredFileName,
            bytes.Length,
            ToSha256(bytes));
    }

    private void ValidateAdditionalArtifacts(
        IReadOnlyList<CompositionOutputBundleCommitArtifact> additionalArtifacts)
    {
        if (additionalArtifacts.Count != _additionalArtifacts.Count)
        {
            throw new InvalidOperationException(
                "Bundle commit artifacts do not match the admitted dynamic artifact count.");
        }

        for (int index = 0; index < additionalArtifacts.Count; index++)
        {
            CompositionOutputBundleCommitArtifact actual = additionalArtifacts[index];
            AtomicBundlePlannedArtifact expected = _additionalArtifacts[index];
            if (!StringComparer.Ordinal.Equals(actual.Role, expected.Role) ||
                !StringComparer.Ordinal.Equals(actual.BindingId, expected.BindingId) ||
                !StringComparer.Ordinal.Equals(
                    actual.SuggestedFileName,
                    expected.SuggestedFileName))
            {
                throw new InvalidOperationException(
                    "Bundle commit artifacts do not match the admitted dynamic artifact plan.");
            }
        }
    }

    private static string ToSha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private void ValidateChildPaths(
        string directory,
        string outputFileName,
        List<string> sourceNames)
    {
        IReadOnlyList<ProtectedPathGuard.ProtectedPath> protectedPaths =
        [
            .. _artifacts.Select(static artifact => new ProtectedPathGuard.ProtectedPath(
                artifact.AcceptedIdentity,
                $"accepted source '{artifact.BindingId}'")),
        ];
        string outputPath = Path.Combine(directory, outputFileName);
        AtomicBundlePathRules.EnsureSupportedPathLength(
            outputPath,
            "Bundle output path");
        ProtectedPathGuard.EnsureDoesNotAlias(
            outputPath,
            "Bundle output path",
            protectedPaths,
            nameof(outputFileName));
        for (int index = 0; index < sourceNames.Count; index++)
        {
            string sourcePath = Path.Combine(directory, sourceNames[index]);
            AtomicBundlePathRules.EnsureSupportedPathLength(
                sourcePath,
                "Bundle source path");
            ProtectedPathGuard.EnsureDoesNotAlias(
                sourcePath,
                "Bundle source path",
                protectedPaths,
                nameof(sourceNames));
        }
    }

    private List<string> CreateArtifactNames(string outputFileName)
    {
        AtomicBundlePathRules.EnsureWindowsName(
            outputFileName,
            "Output filename",
            nameof(outputFileName));
        HashSet<string> allocated = new(StringComparer.OrdinalIgnoreCase)
        {
            outputFileName,
        };
        List<string> names = new(_additionalArtifacts.Count + _artifacts.Count);
        foreach (AtomicBundlePlannedArtifact artifact in _additionalArtifacts)
        {
            AtomicBundlePathRules.EnsureWindowsName(
                artifact.SuggestedFileName,
                "Bundle additional-delivery filename",
                nameof(artifact.SuggestedFileName));
            names.Add(AllocateUniqueFileName(artifact.SuggestedFileName, allocated));
        }

        foreach (AtomicBundleArtifact artifact in _artifacts)
        {
            AtomicBundlePathRules.EnsureWindowsName(
                artifact.OriginalFileName,
                "Bundle source filename",
                nameof(artifact.OriginalFileName));
            names.Add(AllocateUniqueFileName(artifact.OriginalFileName, allocated));
        }

        string longestCandidate = ResolveCandidatePath(suffix: 2);
        AtomicBundlePathRules.EnsureSupportedPathLength(
            Path.Combine(longestCandidate, outputFileName),
            "Bundle output path");
        foreach (string name in names)
        {
            AtomicBundlePathRules.EnsureSupportedPathLength(
                Path.Combine(longestCandidate, name),
                "Bundle source path");
        }

        return names;
    }

    private int FindFirstAvailableSuffix()
    {
        int suffix = 1;
        while (suffix <= MaximumPromotionAttempts && DestinationExists(ResolveCandidatePath(suffix)))
        {
            suffix++;
        }

        return suffix <= MaximumPromotionAttempts
            ? suffix
            : throw new IOException(
                $"Bundle destination remained occupied after {MaximumPromotionAttempts} attempts.");
    }

    private string ResolveCandidatePath(int suffix)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(suffix, 1);
        string folder = suffix == 1 ? _folderName : $"{_folderName} ({suffix})";
        string fullPath = Path.GetFullPath(Path.Combine(_parentDirectory, folder));
        AtomicBundlePathRules.EnsureSupportedPathLength(
            fullPath,
            "Bundle destination directory");
        return fullPath;
    }

    private static string AllocateUniqueFileName(
        string originalFileName,
        HashSet<string> allocated)
    {
        if (allocated.Add(originalFileName))
        {
            return originalFileName;
        }

        string extension = Path.GetExtension(originalFileName);
        string basename = Path.GetFileNameWithoutExtension(originalFileName);
        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{basename} ({suffix}){extension}";
            if (allocated.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool DestinationExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private sealed class PhysicalAtomicBundleFileWriter : IAtomicBundleFileWriter
    {
        public async ValueTask WriteAsync(
            string path,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            await using FileStream stream = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 131_072,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
