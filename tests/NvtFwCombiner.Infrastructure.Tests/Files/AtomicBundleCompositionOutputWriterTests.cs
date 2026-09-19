using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests sibling-staged atomic bundle promotion.</summary>
public sealed class AtomicBundleCompositionOutputWriterTests
{
    /// <summary>Source collision suffixes cannot create an illegal filename component.</summary>
    [Fact]
    public void PreflightRejectsOverlongSourceCollisionNameWithoutMutation()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string name = new string('a', 251) + ".bin";
        AtomicBundleCompositionOutputWriter writer = new(workspace.Root, "bundle",
            [new AtomicBundleArtifact(name, [1]), new AtomicBundleArtifact(name, [2])]);

        ArgumentException error = Assert.Throws<ArgumentException>(() => writer.EnsureCanCommit("output.bin", null));

        Assert.Contains("Bundle collision filename", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Preflight uses the actual collision suffix, including the transition to two digits.</summary>
    [Fact]
    public void PreflightRejectsActualCollisionPathBeforeWriting()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folderName = new('a', 259 - workspace.Root.Length - 16);
        for (int suffix = 1; suffix <= 9; suffix++)
        {
            _ = Directory.CreateDirectory(Path.Combine(workspace.Root,
                suffix == 1 ? folderName : $"{folderName} ({suffix})"));
        }
        Assert.Equal(260, Path.Combine(workspace.Root, folderName + " (10)", "output.bin").Length);
        AtomicBundleCompositionOutputWriter writer = new(workspace.Root, folderName, []);

        _ = Assert.Throws<PathTooLongException>(() => writer.EnsureCanCommit("output.bin", null));

        Assert.Equal(9, Directory.EnumerateFileSystemEntries(workspace.Root).Count());
    }

    /// <summary>An unoccupied legal destination is not rejected for a suffix it does not need.</summary>
    [Fact]
    public async Task CommitAcceptsBoundaryDestinationWithoutHypotheticalCollision()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folderName = new('a', 259 - workspace.Root.Length - 12);
        string outputPath = Path.Combine(workspace.Root, folderName, "output.bin");
        Assert.Equal(259, outputPath.Length);
        AtomicBundleCompositionOutputWriter writer = new(workspace.Root, folderName, []);

        writer.EnsureCanCommit("output.bin", null);
        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin", new byte[] { 1, 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(outputPath, receipt.OutputId);
        Assert.Equal([1, 2], await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken));
        _ = Assert.Single(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>A legal long destination survives staging and existing-folder suffix allocation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitLongFolderNameStagesCompleteBundleWithoutNameInflation(bool collision)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folderName = new('a', 240 - workspace.Root.Length - 12);
        string originalFolder = Path.Combine(workspace.Root, folderName);
        if (collision)
        {
            _ = Directory.CreateDirectory(originalFolder);
            await File.WriteAllBytesAsync(Path.Combine(originalFolder, "keep.bin"), [7],
                TestContext.Current.CancellationToken);
        }
        AtomicBundleCompositionOutputWriter writer = new(workspace.Root, folderName,
            [new AtomicBundleArtifact("output.bin", [3, 4])]);
        writer.EnsureCanCommit("output.bin", null);

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin", new byte[] { 1, 2 }, TestContext.Current.CancellationToken);

        string destination = collision ? originalFolder + " (2)" : originalFolder;
        Assert.Equal(Path.Combine(destination, "output.bin"), receipt.OutputId);
        Assert.Equal([1, 2], await File.ReadAllBytesAsync(receipt.OutputId, TestContext.Current.CancellationToken));
        Assert.Equal([3, 4], await File.ReadAllBytesAsync(Path.Combine(destination, "output (2).bin"),
            TestContext.Current.CancellationToken));
        Assert.Equal(collision ? 2 : 1, Directory.EnumerateFileSystemEntries(workspace.Root).Count());
        Assert.Equal(2, Directory.EnumerateFileSystemEntries(destination).Count());
        if (collision)
        {
            Assert.Equal([7], await File.ReadAllBytesAsync(Path.Combine(originalFolder, "keep.bin"),
                TestContext.Current.CancellationToken));
        }
    }

    /// <summary>Compiled additional output is staged before sources with deterministic names and typed evidence.</summary>
    [Fact]
    public async Task CommitStagesAdditionalDeliveryAndSourcesInOneManifest()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            "bundle",
            [new AtomicBundleArtifact("source", "output.bin", "source-id", [1])],
            additionalArtifacts:
            [
                new AtomicBundlePlannedArtifact(
                    "additional-delivery",
                    "ab-a-flashcode",
                    "output.bin"),
            ]);

        CompositionOutputCommitReceipt receipt = await writer.CommitBundleAsync(
            "output.bin",
            new ReadOnlyMemory<byte>([9]),
            [
                new CompositionOutputBundleCommitArtifact(
                    "additional-delivery",
                    "ab-a-flashcode",
                    "output.bin",
                    new ReadOnlyMemory<byte>([8, 7])),
            ],
            TestContext.Current.CancellationToken);

        CompositionOutputBundleCommitReceipt bundle = Assert.IsType<CompositionOutputBundleCommitReceipt>(
            receipt.Bundle);
        Assert.Equal(
            ["output.bin", "output (2).bin", "output (3).bin"],
            bundle.Artifacts.Select(static artifact => artifact.DeliveredFileName));
        Assert.Equal(
            ["output", "additional-delivery", "source"],
            bundle.Artifacts.Select(static artifact => artifact.Role));
        Assert.Equal(
            [null, "ab-a-flashcode", "source"],
            bundle.Artifacts.Select(static artifact => artifact.BindingId));
        Assert.Equal([9], await File.ReadAllBytesAsync(
            Path.Combine(bundle.ResolvedDirectory, "output.bin"),
            TestContext.Current.CancellationToken));
        Assert.Equal([8, 7], await File.ReadAllBytesAsync(
            Path.Combine(bundle.ResolvedDirectory, "output (2).bin"),
            TestContext.Current.CancellationToken));
    }

    /// <summary>Output reserves its name and same-basename sources receive deterministic suffixes.</summary>
    [Fact]
    public async Task CommitWritesOutputAndSourcesWithDeterministicBasenameSuffixes()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            "bundle",
            [
                new AtomicBundleArtifact("output.bin", [1]),
                new AtomicBundleArtifact("output.bin", [2]),
            ]);

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin",
            new ReadOnlyMemory<byte>([9]),
            TestContext.Current.CancellationToken);

        string outputPath = receipt.OutputId;
        string folder = Path.Combine(workspace.Root, "bundle");
        Assert.Equal(Path.Combine(folder, "output.bin"), outputPath);
        Assert.Equal([9], await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken));
        Assert.Equal([1], await File.ReadAllBytesAsync(
            Path.Combine(folder, "output (2).bin"), TestContext.Current.CancellationToken));
        Assert.Equal([2], await File.ReadAllBytesAsync(
            Path.Combine(folder, "output (3).bin"), TestContext.Current.CancellationToken));
        CompositionOutputBundleCommitReceipt bundle = receipt.Bundle ??
            throw new Xunit.Sdk.XunitException("Bundle commit did not return promotion evidence.");
        Assert.Equal(folder, bundle.ResolvedDirectory);
        Assert.Equal(
            ["output.bin", "output (2).bin", "output (3).bin"],
            bundle.Artifacts.Select(static artifact => artifact.DeliveredFileName));
        Assert.Equal(["output", "source", "source"],
            bundle.Artifacts.Select(static artifact => artifact.Role));
        Assert.All(bundle.Artifacts, static artifact => Assert.Equal(64, artifact.Sha256.Length));
    }

    /// <summary>Existing files or folders allocate the next bundle suffix without mutation.</summary>
    [Fact]
    public async Task CommitSuffixesFolderCollision()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        _ = Directory.CreateDirectory(Path.Combine(workspace.Root, "bundle"));
        await File.WriteAllBytesAsync(
            Path.Combine(workspace.Root, "bundle (2)"),
            [7],
            TestContext.Current.CancellationToken);
        AtomicBundleCompositionOutputWriter writer = new(workspace.Root, "bundle", []);

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin",
            new ReadOnlyMemory<byte>([9]),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            Path.Combine(workspace.Root, "bundle (3)", "output.bin"),
            receipt.OutputId);
    }

    /// <summary>A destination created after staging is complete is treated as a race and receives a suffix.</summary>
    [Fact]
    public async Task CommitSuffixesDestinationRaceAtPromotion()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            "bundle",
            [],
            new RacingBundleFileWriter(Path.Combine(workspace.Root, "bundle")),
            [new AtomicBundlePlannedArtifact(
                "additional-delivery",
                "ab-a-flashcode",
                "a.bin")]);

        CompositionOutputCommitReceipt receipt = await writer.CommitBundleAsync(
            "output.bin",
            new ReadOnlyMemory<byte>([9]),
            [new CompositionOutputBundleCommitArtifact(
                "additional-delivery",
                "ab-a-flashcode",
                "a.bin",
                new ReadOnlyMemory<byte>([8]))],
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(workspace.Root, "bundle (2)", "output.bin"), receipt.OutputId);
        Assert.True(Directory.Exists(Path.Combine(workspace.Root, "bundle")));
    }

    /// <summary>Windows device names and path syntax fail before staging begins.</summary>
    [Theory]
    [InlineData("CON")]
    [InlineData("bundle.")]
    [InlineData("../bundle")]
    public void ConstructorRejectsInvalidWindowsFolderNames(string folderName)
    {
        using TempWorkspace workspace = TempWorkspace.Create();

        _ = Assert.Throws<ArgumentException>(() =>
            new AtomicBundleCompositionOutputWriter(workspace.Root, folderName, []));

        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Overlong destination paths fail preflight before staging begins.</summary>
    [Fact]
    public void PreflightRejectsOverlongDestinationPath()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folderName = new('a', 260 - workspace.Root.Length - 1);
        Assert.InRange(folderName.Length, 1, 255);
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            folderName,
            []);

        _ = Assert.Throws<PathTooLongException>(() => writer.EnsureCanCommit("output.bin", null));

        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Oversized components are rejected before any staging or destination access.</summary>
    [Fact]
    public void ConstructorRejectsOverlongComponentBeforeMutation()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        _ = Assert.Throws<ArgumentException>(() =>
            new AtomicBundleCompositionOutputWriter(workspace.Root, new string('a', 256), []));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>An injected staging write failure leaves neither a final nor staging directory.</summary>
    [Fact]
    public async Task StagingFailureLeavesNoVisiblePartialBundle()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            "bundle",
            [],
            new ThrowingBundleFileWriter(writeNumber: 2),
            [new AtomicBundlePlannedArtifact(
                "additional-delivery",
                "ab-a-flashcode",
                "a.bin")]);

        _ = await Assert.ThrowsAsync<IOException>(async () =>
            await writer.CommitBundleAsync(
                "output.bin",
                new ReadOnlyMemory<byte>([9]),
                [new CompositionOutputBundleCommitArtifact(
                    "additional-delivery",
                    "ab-a-flashcode",
                    "a.bin",
                    new ReadOnlyMemory<byte>([8]))],
                TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Cancellation during staging removes staging and publishes no final folder.</summary>
    [Fact]
    public async Task CancellationLeavesNoVisiblePartialBundle()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        using CancellationTokenSource cancellation = new();
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            "bundle",
            [],
            new CancellingBundleFileWriter(cancellation, writeNumber: 2),
            [new AtomicBundlePlannedArtifact(
                "additional-delivery",
                "ab-a-flashcode",
                "a.bin")]);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await writer.CommitBundleAsync(
                "output.bin",
                new ReadOnlyMemory<byte>([9]),
                [new CompositionOutputBundleCommitArtifact(
                    "additional-delivery",
                    "ab-a-flashcode",
                    "a.bin",
                    new ReadOnlyMemory<byte>([8]))],
                cancellation.Token));

        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Bundle delivery uses supplied bytes while a protected source stays locked and unchanged.</summary>
    [Fact]
    public async Task CommitNeverReopensOrOverwritesProtectedInput()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string protectedInput = workspace.Write("source.bin", [5, 6, 7]);
        string deliveryParent = Directory.CreateDirectory(
            Path.Combine(workspace.Root, "delivery")).FullName;
        await using FileStream inputLock = new(
            protectedInput,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        AtomicBundleCompositionOutputWriter writer = new(
            deliveryParent,
            "bundle",
            [new AtomicBundleArtifact("source.bin", [5, 6, 7])]);

        CompositionOutputCommitReceipt receipt = await writer.CommitAsync(
            "output.bin",
            new ReadOnlyMemory<byte>([9]),
            TestContext.Current.CancellationToken);

        Assert.True(File.Exists(receipt.OutputId));
        inputLock.Position = 0;
        byte[] unchanged = new byte[3];
        await inputLock.ReadExactlyAsync(unchanged, TestContext.Current.CancellationToken);
        Assert.Equal([5, 6, 7], unchanged);
    }

    private sealed class ThrowingBundleFileWriter(int writeNumber) : IAtomicBundleFileWriter
    {
        private int _writeCount;

        public ValueTask WriteAsync(
            string path,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            return ++_writeCount == writeNumber
                ? ValueTask.FromException(new IOException("Injected staging failure."))
                : ValueTask.CompletedTask;
        }
    }

    private sealed class CancellingBundleFileWriter(
        CancellationTokenSource cancellation,
        int writeNumber) :
        IAtomicBundleFileWriter
    {
        private int _writeCount;

        public ValueTask WriteAsync(
            string path,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            if (++_writeCount == writeNumber)
            {
                cancellation.Cancel();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RacingBundleFileWriter(string racedDestination) : IAtomicBundleFileWriter
    {
        public async ValueTask WriteAsync(
            string path,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            await File.WriteAllBytesAsync(path, bytes.ToArray(), cancellationToken);
            _ = Directory.CreateDirectory(racedDestination);
        }
    }
}
