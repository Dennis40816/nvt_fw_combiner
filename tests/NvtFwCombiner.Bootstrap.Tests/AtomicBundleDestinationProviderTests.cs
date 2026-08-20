using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests Bootstrap-visible destination selection without UI or CLI policy.</summary>
public sealed class AtomicBundleDestinationProviderTests
{
    /// <summary>Omitting bundle intent preserves the existing loose-output writer.</summary>
    [Fact]
    public async Task BundleDisabledCommitsOnlyLooseOutput()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        ProtectedCompositionDestinationProvider provider = new();
        CompositionExecutionDestination destination = provider.Prepare(
            new CompositionExecutionDestinationRequest(
                Build: true,
                workspace.Root,
                "output.bin",
                OutputPathUsesAutomaticName: false,
                Bindings: [],
                AdditionalProtectedPaths: [],
                AdditionalDelivery: null,
                BundleDelivery: null));

        ICompositionOutputWriter writer = destination.OutputWriter ??
            throw new Xunit.Sdk.XunitException("Build destination did not provide an output writer.");
        CompositionOutputCommitReceipt committed = await writer.CommitAsync(
                "output.bin",
                new ReadOnlyMemory<byte>([1]),
                TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(workspace.Root, "output.bin"), committed.OutputId);
        string onlyEntry = Assert.Single(Directory.EnumerateFileSystemEntries(workspace.Root));
        Assert.Equal(
            "output.bin",
            Path.GetFileName(onlyEntry) ?? throw new Xunit.Sdk.XunitException(
                "Committed output did not have a filename."));
    }
}
