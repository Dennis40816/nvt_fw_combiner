using System.IO.Compression;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    /// <summary>Actual counters publish only stage entry and changed integer percentages.</summary>
    [Fact]
    public void MeasuredProgressDoesNotRepublishTheSameIntegerPercent()
    {
        var updates = new List<ManagedFirstInstallationProgress>();
        var projection = new MeasuredFirstInstallationStageProgress(
            new InlineProgress<ManagedFirstInstallationProgress>(updates.Add),
            ManagedFirstInstallationProgressStage.ReadingPackage);

        projection.Report(0, 1_000);
        projection.Report(1, 1_000);
        projection.Report(9, 1_000);
        projection.Report(10, 1_000);
        projection.Report(11, 1_000);
        projection.Report(999, 1_000);
        projection.Report(1_000, 1_000);
        projection.Report(1_000, 1_000);

        Assert.Equal([0, 1, 99, 100], updates.Select(static update => update.Percent));
        Assert.All(updates, update =>
            Assert.Equal(ManagedFirstInstallationProgressStage.ReadingPackage, update.Stage));
    }

    /// <summary>Every measurable first-install stage ends at its actual completed byte total.</summary>
    [Fact]
    public async Task RealPackageMaterializerReportsActualStageLocalByteCompletion()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = RealPackageCandidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        string packagePath = Path.Combine(
            candidate.Identity.SourceRoot,
            candidate.Package.PackagePath.Value.Replace('/', Path.DirectorySeparatorChar));
        long packageBytes = new FileInfo(packagePath).Length;
        long declaredPayloadBytes;
        long expandedArchiveBytes;
        using (ZipArchive archive = ZipFile.OpenRead(packagePath))
        {
            ZipArchiveEntry manifest = Assert.Single(archive.Entries, static entry =>
                entry.FullName.EndsWith("/RELEASE-MANIFEST.json", StringComparison.Ordinal));
            using Stream stream = manifest.Open();
            using JsonDocument document = JsonDocument.Parse(stream);
            declaredPayloadBytes = document.RootElement.GetProperty("files")
                .EnumerateArray()
                .Sum(static file => file.GetProperty("size").GetInt64());
            expandedArchiveBytes = archive.Entries
                .Where(static entry => !entry.FullName.EndsWith('/'))
                .Sum(static entry => entry.Length);
        }
        Dictionary<ManagedFirstInstallationProgressStage, long> expectedTotals =
            new Dictionary<ManagedFirstInstallationProgressStage, long>
            {
                [ManagedFirstInstallationProgressStage.ReadingPackage] = packageBytes,
                [ManagedFirstInstallationProgressStage.VerifyingPackage] = declaredPayloadBytes,
                [ManagedFirstInstallationProgressStage.InstallingPackage] = expandedArchiveBytes,
                [ManagedFirstInstallationProgressStage.VerifyingInstallation] = declaredPayloadBytes,
            };
        var updates = new List<ManagedFirstInstallationProgress>();
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(
            new FileSystemManagedVersionRepository());
        using var payload = new TestPayloadCapture(PayloadIdentity());

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            Path.Combine(temporary.Path, "state", "version-manager.v1.json"),
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            new InlineProgress<ManagedFirstInstallationProgress>(updates.Add),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        result.Installation!.Dispose();
        ManagedFirstInstallationProgressStage[] stages =
        [
            ManagedFirstInstallationProgressStage.ReadingPackage,
            ManagedFirstInstallationProgressStage.VerifyingPackage,
            ManagedFirstInstallationProgressStage.InstallingPackage,
            ManagedFirstInstallationProgressStage.VerifyingInstallation,
        ];
        foreach (ManagedFirstInstallationProgressStage stage in stages)
        {
            ManagedFirstInstallationProgress[] stageUpdates =
                [.. updates.Where(update => update.Stage == stage)];
            Assert.NotEmpty(stageUpdates);
            Assert.Equal(0, stageUpdates[0].CompletedWork);
            Assert.Equal(expectedTotals[stage], stageUpdates[0].TotalWork);
            Assert.Equal(stageUpdates[^1].TotalWork, stageUpdates[^1].CompletedWork);
            Assert.Equal(100, stageUpdates[^1].Percent);
            _ = Assert.Single(stageUpdates.Select(static update => update.TotalWork).Distinct());
            Assert.Equal(
                stageUpdates.Select(static update => update.Percent).Distinct(),
                stageUpdates.Select(static update => update.Percent));
            Assert.Equal(
                stageUpdates.OrderBy(static update => update.CompletedWork),
                stageUpdates);
        }
        int verificationComplete = updates.FindLastIndex(static update =>
            update.Stage == ManagedFirstInstallationProgressStage.VerifyingInstallation &&
            update.Percent == 100);
        int finalizationStarted = updates.FindIndex(static update =>
            update.Stage == ManagedFirstInstallationProgressStage.FinalizingInstallation);
        Assert.True(verificationComplete >= 0);
        Assert.True(finalizationStarted > verificationComplete);
        Assert.Null(updates[finalizationStarted].Percent);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            report(value);
        }
    }
}
