using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Preview and commit validate the same actually allocated bundle names.</summary>
public sealed class CompositionOutputBundleDestinationValidationTests
{
    /// <summary>The resolved folder component includes its actual filesystem collision suffix.</summary>
    [Fact]
    public void PreviewRejectsOverlongActualFolderComponent()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folder = new('f', 252);
        _ = Directory.CreateDirectory(Path.Combine(workspace.Root, folder));
        CompositionOutputBundleIntent intent = CreateIntent(workspace.Root, folder, "output.bin", []);

        CompositionOutputBundleDestinationValidation result = new FileSystemCompositionOutputBundleDestinationValidator()
            .Validate(intent);

        Assert.Contains(result.Issues, issue => issue.Code == CompositionOutputBundleValidationIssueCodes.PathTooLong &&
            issue.Message.Contains("255 UTF-16", StringComparison.Ordinal));
        _ = Assert.Throws<PathTooLongException>(() => CreateWriter(intent).EnsureCanCommit(intent.OutputFileName, null));
        _ = Assert.Single(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Collision suffixes on either optional delivery or accepted source count toward the full path.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewRejectsActualChildCollisionPath(bool additional)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folder = new('f', 50);
        string name = new string('a', 259 - workspace.Root.Length - folder.Length - 6) + ".bin";
        CompositionOutputBundleIntent intent = CreateIntent(workspace.Root, folder, name,
            additional ? [] : [name], additional ? name : null);
        Assert.Equal(259, Path.Combine(workspace.Root, folder, name).Length);

        CompositionOutputBundleDestinationValidation result = new FileSystemCompositionOutputBundleDestinationValidator()
            .Validate(intent);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == CompositionOutputBundleValidationIssueCodes.PathTooLong);
        _ = Assert.Throws<PathTooLongException>(() => CreateWriter(intent).EnsureCanCommit(name, null));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Additional delivery reserves suffix 2 before sources advance the final suffix from 9 to 10.</summary>
    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void PreviewCountsTwoDigitSourceSuffixAfterAdditionalDelivery(int originalPathLength, bool valid)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folder = new('f', 50);
        string name = new string('a', originalPathLength - workspace.Root.Length - folder.Length - 6) + ".bin";
        CompositionOutputBundleIntent intent = CreateIntent(workspace.Root, folder, name,
            [.. Enumerable.Repeat(name, 8)], name);

        CompositionOutputBundleDestinationValidation result = new FileSystemCompositionOutputBundleDestinationValidator()
            .Validate(intent);

        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            CreateWriter(intent).EnsureCanCommit(name, null);
        }
        else
        {
            Assert.Contains(result.Issues, issue => issue.Code == CompositionOutputBundleValidationIssueCodes.PathTooLong);
            _ = Assert.Throws<PathTooLongException>(() => CreateWriter(intent).EnsureCanCommit(name, null));
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Preview admits exactly the boundary without reserving hypothetical suffixes.</summary>
    [Fact]
    public void PreviewAcceptsBoundaryWithoutCollision()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string folder = new('f', 50);
        string name = new string('a', 259 - workspace.Root.Length - folder.Length - 6) + ".bin";
        CompositionOutputBundleIntent intent = CreateIntent(workspace.Root, folder, name, []);

        CompositionOutputBundleDestinationValidation result = new FileSystemCompositionOutputBundleDestinationValidator()
            .Validate(intent);

        Assert.True(result.IsValid);
        CreateWriter(intent).EnsureCanCommit(name, null);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Every allocated child is checked against every accepted identity, including output and delivery.</summary>
    [Theory]
    [InlineData("output.bin")]
    [InlineData("delivery.bin")]
    public void PreviewRejectsChildAliasingAcceptedSource(string protectedName)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        CompositionOutputBundleIntent intent = CreateIntent(workspace.Root, "bundle", "output.bin",
            ["source.bin"], "delivery.bin", Path.Combine(workspace.Root, "bundle", protectedName));

        CompositionOutputBundleDestinationValidation result = new FileSystemCompositionOutputBundleDestinationValidator()
            .Validate(intent);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == CompositionOutputBundleValidationIssueCodes.ProtectedAlias);
        _ = Assert.Throws<ArgumentException>(() => CreateWriter(intent).EnsureCanCommit(intent.OutputFileName, null));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Primary, additional, and source collisions preserve allocation order and complete accepted bytes.</summary>
    [Fact]
    public async Task PreviewAndCommitShareAdditionalThenSourceCollisionOrder()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        CompositionOutputBundleIntent intent = CreateIntent(workspace.Root, "bundle", "output.bin",
            ["OUTPUT.bin", "output (2).bin"], "output.bin");
        CompositionOutputBundleDestinationValidation result = new FileSystemCompositionOutputBundleDestinationValidator()
            .Validate(intent);
        Assert.True(result.IsValid);
        AtomicBundleCompositionOutputWriter writer = CreateWriter(intent);
        writer.EnsureCanCommit(intent.OutputFileName, null);

        CompositionOutputCommitReceipt receipt = await writer.CommitBundleAsync(intent.OutputFileName,
            new byte[] { 0xA9 }, [new CompositionOutputBundleCommitArtifact("additional-delivery", "test-delivery",
                "output.bin", new byte[] { 0xA8 })], TestContext.Current.CancellationToken);

        Assert.Equal(result.ResolvedDirectoryPreview, receipt.Bundle!.ResolvedDirectory);
        Assert.Equal(["output.bin", "output (2).bin", "OUTPUT (3).bin", "output (2) (2).bin"],
            receipt.Bundle.Artifacts.Select(static artifact => artifact.DeliveredFileName));
        byte[][] expected = [[0xA9], [0xA8], [1], [2]];
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], await File.ReadAllBytesAsync(
                Path.Combine(receipt.Bundle.ResolvedDirectory, receipt.Bundle.Artifacts[index].DeliveredFileName),
                TestContext.Current.CancellationToken));
        }
    }

    private static AtomicBundleCompositionOutputWriter CreateWriter(CompositionOutputBundleIntent intent)
    {
        var delivery = new CompositionExecutionBundleDelivery(intent);
        return Assert.IsType<AtomicBundleCompositionOutputWriter>(new ProtectedCompositionDestinationProvider().Prepare(
            new CompositionExecutionDestinationRequest(true, intent.ParentDirectory, intent.OutputFileName, false,
                [], [], null, delivery)).OutputWriter);
    }

    private static CompositionOutputBundleIntent CreateIntent(string parent, string folder, string output,
        string[] sources, string? additional = null, string? acceptedIdentity = null)
    {
        var host = new IsolatedBootstrapTestHost();
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(host.Canonical.Catalog.ResolveUniqueRoute(
            "NT51929", ExperienceIds.StandardMerge, "selector-free").Capability);
        var session = new ActiveSessionSnapshot(ExperienceIds.StandardMerge, capability.ResolutionToken,
            new AuthoringRevision(1), "test-route", capability.CapabilityFingerprint, true,
            "NT51929", "selector-free", "test-map", [], [], [], null, null, [],
            capability.CompiledComposition.CompilationFingerprint, capability);
        CompositionAdditionalDeliveryPlan[] additionalPlans = additional is null ? [] :
            [new CompositionAdditionalDeliveryPlan("test-profile", "test-delivery", new ByteRange(0, 1), additional)];
        var preparation = new CompositionOutputPreparation(new CompositionOutputNamePreview(output, null, []),
            additionalPlans);
        var admission = new CompositionOutputBundleAdmission(session, preparation,
            [.. sources.Select((name, index) => new CompositionExecutionBundleSource($"source-{index}", $"slot-{index}",
                acceptedIdentity ?? Path.Combine(parent, $"accepted-{index}.bin"), FileStamp.FromBytes([(byte)(index + 1)]), name,
                [(byte)(index + 1)]))]);
        return new CompositionOutputBundleIntent(admission, parent, folder,
            additional is null ? null : "test-delivery");
    }
}
