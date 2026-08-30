using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    /// <summary>Directory enumeration failures discard every previously observed row.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InventoryEnumerationFailureReturnsUnavailableWithoutPartialFacts(
        bool permissionDenied)
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed");
        _ = Directory.CreateDirectory(Path.Combine(managedRoot, "versions"));
        var admission = new ManagedVersionAdmission(
            ManagedAppVersion.Parse("0.10.6"),
            "admission-0.10.6",
            new string('a', 64));
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            _ => permissionDenied
                ? throw new UnauthorizedAccessException("Injected inventory denial.")
                : throw new IOException("Injected inventory read failure."));

        ManagedVersionInventoryReadResult result = await repository.InventoryAsync(
            managedRoot,
            [admission],
            admission.Version,
            admission.Version,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, result.Issue);
        Assert.Null(result.Inventory);
    }

    /// <summary>A directory that disappears after enumeration invalidates the whole observation.</summary>
    [Fact]
    public async Task EnumeratedDirectoryDisappearingBeforeObservationReturnsUnavailable()
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed");
        string versionsRoot = Directory.CreateDirectory(Path.Combine(managedRoot, "versions")).FullName;
        string disappearing = Directory.CreateDirectory(Path.Combine(versionsRoot, "0.10.6")).FullName;
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            _ => EnumerateAfterDeleting(disappearing));

        ManagedVersionInventoryReadResult result = await repository.InventoryAsync(
            managedRoot,
            [],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, result.Issue);
        Assert.Null(result.Inventory);
    }

    /// <summary>A self-admitted directory disappearing after verification is never published as damaged.</summary>
    [Fact]
    public async Task SelfAdmittedDirectoryDisappearingAfterVerificationReturnsUnavailable()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        var installer = new FileSystemManagedVersionRepository();
        ManagedVersionInstallResult installed = await installer.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);
        Assert.True(installed.IsSuccess);

        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            _ => false);
        ManagedVersionInventoryReadResult result = await repository.InventoryAsync(
            managedRoot,
            [],
            activeVersion: null,
            lastKnownGoodVersion: null,
            failedActivationVersion: null,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, result.Issue);
        Assert.Null(result.Inventory);
    }

    /// <summary>Factories expose only coherent success and unavailable inventory states.</summary>
    [Fact]
    public void InventoryReadResultFactoriesProduceCoherentStates()
    {
        ManagedVersionInventoryReadResult success = ManagedVersionInventoryReadResult.Success(
            ManagedVersionInventory.Create([]));
        ManagedVersionInventoryReadResult unavailable = ManagedVersionInventoryReadResult.Unavailable();

        Assert.True(success.IsSuccess);
        Assert.NotNull(success.Inventory);
        Assert.Equal(ManagedVersionInventoryReadIssue.None, success.Issue);
        Assert.False(unavailable.IsSuccess);
        Assert.Null(unavailable.Inventory);
        Assert.Equal(ManagedVersionInventoryReadIssue.Unavailable, unavailable.Issue);
        _ = Assert.Throws<ArgumentNullException>(() => ManagedVersionInventoryReadResult.Success(null!));
    }

    private static IEnumerable<string> EnumerateAfterDeleting(string directory)
    {
        Directory.Delete(directory);
        yield return directory;
    }

    private static ManagedVersionInventory RequireInventory(
        ManagedVersionInventoryReadResult result)
    {
        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.Equal(ManagedVersionInventoryReadIssue.None, result.Issue);
        return Assert.IsType<ManagedVersionInventory>(result.Inventory);
    }
}
