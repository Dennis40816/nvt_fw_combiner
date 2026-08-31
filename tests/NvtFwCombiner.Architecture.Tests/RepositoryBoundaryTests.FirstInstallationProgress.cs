namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>First-install progress remains one actual-work projection rather than an estimate.</summary>
    [Fact]
    public void FirstInstallationProgressUsesTheExistingMeasuredPathAndOneUiProjection()
    {
        string contract = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedFirstInstallationContracts.cs");
        string setup = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedInstallationSetup.cs");
        string installation = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.Installation.cs");
        string materializer = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.cs");
        string repository = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.cs");
        string archive = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/BoundedArchiveReader.cs");
        string launcher = ReadText(
            "src/NvtFwCombiner.DistributionLauncher/LauncherWindow.axaml.cs");
        string launcherXaml = ReadText(
            "src/NvtFwCombiner.DistributionLauncher/LauncherWindow.axaml");

        AssertContainsAll(
            contract,
            "public enum ManagedFirstInstallationProgressStage",
            "public readonly record struct ManagedFirstInstallationProgress",
            "public int? Percent =>",
            "IProgress<ManagedFirstInstallationProgress>? progress");
        AssertContainsAll(
            setup,
            "new NonThrowingProgress(progress)",
            "ManagedFirstInstallationProgressStage.RevalidatingSource",
            "ManagedFirstInstallationProgressStage.FinalizingInstallation",
            "ManagedFirstInstallationProgressStage.StartingApplication");
        AssertContainsAll(
            installation,
            "new MeasuredFirstInstallationStageProgress(progress, stage)",
            "ManagedFirstInstallationProgressStage.ReadingPackage",
            "ManagedFirstInstallationProgressStage.VerifyingPackage",
            "ManagedFirstInstallationProgressStage.InstallingPackage",
            "ManagedFirstInstallationProgressStage.VerifyingInstallation",
            "percent <= _lastPercent");
        AssertContainsAll(repository, "progress?.Invoke(0, stream.Length)", "progress?.Invoke(completed, stream.Length)");
        Assert.Contains("bytesTransferred?.Invoke(read)", archive, StringComparison.Ordinal);
        AssertContainsAll(
            materializer,
            "ManagedFirstInstallationProgressStage.FinalizingInstallation",
            "var seedStore = new JsonVersionManagerStateStore(");
        AssertContainsAll(
            launcher,
            "new Progress<ManagedFirstInstallationProgress>(PresentOperationProgressSafely)",
            "private void PresentOperationProgressSafely",
            "OperationProgressBar.Value = progress.Percent ?? 0",
            "Downloading update package");
        AssertDoesNotContainAny(launcher, "DispatcherTimer", "CompletedWork *", "TotalWork.Value");
        AssertContainsAll(
            launcherXaml,
            "x:Name=\"OperationProgressPanel\"",
            "x:Name=\"OperationProgressText\"",
            "AutomationProperties.LiveSetting=\"Polite\"",
            "x:Name=\"OperationProgressBar\"");
        Assert.Equal(1, CountOccurrences(installation, "actualPackageHash = await HashAsync("));
    }
}
