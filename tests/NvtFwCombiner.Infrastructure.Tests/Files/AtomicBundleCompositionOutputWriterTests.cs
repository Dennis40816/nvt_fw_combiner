using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests sibling-staged atomic bundle promotion.</summary>
public sealed class AtomicBundleCompositionOutputWriterTests
{
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
        AtomicBundleCompositionOutputWriter writer = new(
            workspace.Root,
            new string('a', 260),
            []);

        _ = Assert.Throws<PathTooLongException>(() => writer.EnsureCanCommit("output.bin", null));

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
