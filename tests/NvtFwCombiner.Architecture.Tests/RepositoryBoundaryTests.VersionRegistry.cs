namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Registry transports share one parser while selection policy stays in Application.</summary>
    [Fact]
    public void VersionRegistryKeepsTransportPolicyAndFilesystemInTheirOwningLayers()
    {
        string contract = ReadText(
            "src/NvtFwCombiner.Contracts/VersionManagement/UpdateSourceRegistryDocument.cs");
        string application = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/UpdateSourceRegistry.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.FreshInstallation.cs");
        string infrastructure = string.Concat(
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateSourceRegistry.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/HttpUpdateSourceRegistry.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/UpdateSourceRegistryDocumentParser.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/UpdateSourceRegistryAdapterFactory.cs"));
        string bootstrap = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");

        Assert.Contains("UpdateSourceRegistryDocument", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", contract, StringComparison.Ordinal);
        Assert.Contains("IUpdateSourceRegistry", application, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryEntryStatus.Latest", application, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryEntryStatus.Available", application, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSourceRegistryEntryStatus.Deprecated =>", application, StringComparison.Ordinal);
        Assert.Contains("FileSystemUpdateSourceRegistry", infrastructure, StringComparison.Ordinal);
        Assert.Contains("HttpUpdateSourceRegistry", infrastructure, StringComparison.Ordinal);
        Assert.Equal(
            2,
            CountOccurrences(infrastructure, "UpdateSourceRegistryDocumentParser.Parse(bytes)"));
        Assert.DoesNotContain("IManagedVersionRepository", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("IUpdateCatalogSource", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionManagerState", infrastructure, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceRegistryAdapterFactory.Create", bootstrap, StringComparison.Ordinal);
    }

    /// <summary>Filesystem Registry and Catalog adapters consume one path-admission owner.</summary>
    [Fact]
    public void VersionRegistryAndCatalogReuseManagedPathSafety()
    {
        string owner = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedPathSafety.cs");
        string[] consumers =
        [
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateSourceRegistry.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/UpdateSourceRegistryDocumentParser.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateCatalogSource.cs"),
        ];

        Assert.Contains("TryNormalizeExactAbsolutePath", owner, StringComparison.Ordinal);
        Assert.Contains("HasReparseComponent", owner, StringComparison.Ordinal);
        Assert.Contains("PathComparer", owner, StringComparison.Ordinal);
        foreach (string consumer in consumers)
        {
            Assert.Contains("ManagedPathSafety.", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain("IsDeviceExtendedOrAlternateStream", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain("private static bool HasReparseComponent", consumer, StringComparison.Ordinal);
            Assert.DoesNotContain("private static StringComparer PathComparer", consumer, StringComparison.Ordinal);
        }
    }

    /// <summary>Fresh setup promotion and recovery never regress to path-based tree mutation.</summary>
    [Fact]
    public void ManagedSetupUsesHandleCustodyInsteadOfPathMoveOrRecursiveDelete()
    {
        string implementation = string.Concat(
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.Helpers.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.Transaction.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.Native.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsManagedSetupPathCustody.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsManagedSetupPathCustody.Native.cs"));

        Assert.DoesNotContain("Directory.Move(", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain("recursive: true", implementation, StringComparison.Ordinal);
        Assert.Contains("NtCreateFile", implementation, StringComparison.Ordinal);
        Assert.Contains("NtSetInformationFile", implementation, StringComparison.Ordinal);
        Assert.Contains("RevalidateClosedTree", implementation, StringComparison.Ordinal);

        string setupCustody = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsManagedSetupPathCustody.cs");
        Assert.Contains(
            "TryCaptureImmutableTreeFromHeldDirectory",
            setupCustody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryAcquireImmutableTree(",
            setupCustody,
            StringComparison.Ordinal);
    }

    /// <summary>Setup and ordinary install retain one package semantic owner.</summary>
    [Fact]
    public void ManagedSetupReusesTheSingleManagedPackageVerifier()
    {
        string sourceRoot = Path.Combine(Root.FullName, "src");
        string[] declarations =
        [
            .. Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "internal static class ManagedPackageVerifier",
                    StringComparison.Ordinal)),
        ];
        string repository = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.Installation.cs");
        string materializer = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.cs");

        _ = Assert.Single(declarations);
        Assert.Contains("ManagedPackageVerifier.CreatePlanAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ManagedPackageVerifier.ExtractAsync", repository, StringComparison.Ordinal);
        Assert.Contains("ManagedPackageVerifier.VerifyInstalledAsync", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagedPackageVerifier", materializer, StringComparison.Ordinal);
    }

    /// <summary>Recovery diagnosis has one codec, one typed decision owner, and no second writer.</summary>
    [Fact]
    public void ManagedSetupRecoveryDiagnosisKeepsObservationAndDecisionInOwningLayers()
    {
        string application = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedSetupRecoveryDiagnosis.cs");
        string stateStore = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/JsonVersionManagerStateStore.cs");
        string codec = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedSetupTransactionCodec.cs");
        string probe = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedSetupRecoveryProbe.cs");
        string lifetimeProbe = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedProcessLifetimeProbe.cs");
        string lifetimeLease = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedProcessLifetimeLease.cs");
        int statusObserverStart = lifetimeLease.IndexOf(
            "internal static ManagedProcessLifetimeStatus GetStatus(string statePath, string suffix)",
            StringComparison.Ordinal);
        int statusObserverEnd = lifetimeLease.IndexOf(
            "internal bool TerminateTreeAndConfirmEmpty",
            statusObserverStart,
            StringComparison.Ordinal);
        int leaseObserverStart = lifetimeLease.IndexOf(
            "private static ManagedProcessLifetimeStatus GetLeaseStatus",
            StringComparison.Ordinal);
        int leaseObserverEnd = lifetimeLease.IndexOf(
            "private static ManagedProcessLifetimeStatus GetTreeStatus",
            leaseObserverStart,
            StringComparison.Ordinal);
        int treeObserverStart = leaseObserverEnd;
        int treeObserverEnd = lifetimeLease.IndexOf(
            "private static SafeFileHandle? OpenOrCreateJob",
            treeObserverStart,
            StringComparison.Ordinal);
        int openJobStart = lifetimeLease.IndexOf(
            "private static SafeFileHandle? OpenJob(string jobName, uint access)",
            StringComparison.Ordinal);
        int openJobEnd = lifetimeLease.IndexOf(
            "private static string GetJobName",
            openJobStart,
            StringComparison.Ordinal);
        Assert.True(statusObserverStart >= 0 && statusObserverEnd > statusObserverStart);
        Assert.True(leaseObserverStart >= 0 && leaseObserverEnd > leaseObserverStart);
        Assert.True(treeObserverStart >= 0 && treeObserverEnd > treeObserverStart);
        Assert.True(openJobStart >= 0 && openJobEnd > openJobStart);
        string observerChain = string.Concat(
            lifetimeLease[statusObserverStart..statusObserverEnd],
            lifetimeLease[leaseObserverStart..leaseObserverEnd],
            lifetimeLease[treeObserverStart..treeObserverEnd],
            lifetimeLease[openJobStart..openJobEnd]);
        string materializer = string.Concat(
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.cs"),
            ReadText("src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.Helpers.cs"));
        string infrastructureRoot = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.VersionManagement.Infrastructure");
        string[] parserOwners =
        [
            .. Directory.EnumerateFiles(infrastructureRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "JsonContext.ManagedSetupTransactionDocument",
                    StringComparison.Ordinal)),
        ];

        _ = Assert.Single(parserOwners);
        Assert.EndsWith("ManagedSetupTransactionCodec.cs", parserOwners[0], StringComparison.Ordinal);
        Assert.Contains("ManagedInstallationRecoveryExperience", application, StringComparison.Ordinal);
        Assert.Contains("IManagedSetupRecoveryStateReader", application, StringComparison.Ordinal);
        Assert.Contains("IManagedSetupRecoveryStateReader", stateStore, StringComparison.Ordinal);
        Assert.Contains("StatePathIdentity => _path", stateStore, StringComparison.Ordinal);
        Assert.Contains(
            "DiagnoseAsync(\n        string managedRoot,\n        CancellationToken cancellationToken)",
            application,
            StringComparison.Ordinal);
        Assert.Contains("IManagedProcessLifetimeProbe", application, StringComparison.Ordinal);
        Assert.Contains("ManagedProcessLifetimeKind.Bootstrap", application, StringComparison.Ordinal);
        Assert.Contains("ManagedProcessLifetimeKind.Application", application, StringComparison.Ordinal);
        Assert.Contains("ManagedProcessLifetimeKind.Launcher", application, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", application, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", application, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", application, StringComparison.Ordinal);
        Assert.DoesNotContain("IImmutableBootstrapHandoff", application, StringComparison.Ordinal);
        Assert.DoesNotContain("StartAsync(", application, StringComparison.Ordinal);
        Assert.DoesNotContain("StartUntilReadyAsync", application, StringComparison.Ordinal);
        Assert.Contains("WindowsStablePathCustody.TryAcquireFile", probe, StringComparison.Ordinal);
        Assert.Contains("custody.OpenReadOnlyFile", probe, StringComparison.Ordinal);
        Assert.Contains("custody.RevalidateClosedTree", probe, StringComparison.Ordinal);
        Assert.Contains("acquired.IsExactChildMissing", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("File.GetAttributes", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Create", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Move", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Write", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Create", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Move", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", probe, StringComparison.Ordinal);
        Assert.Contains("ManagedProcessLifetimeLease.GetStatus", lifetimeProbe, StringComparison.Ordinal);
        Assert.DoesNotContain("Start", lifetimeProbe, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAcquire", lifetimeProbe, StringComparison.Ordinal);
        Assert.Contains("GetLeaseStatus(statePath, suffix)", observerChain, StringComparison.Ordinal);
        Assert.Contains("GetTreeStatus(GetJobName(statePath, suffix))", observerChain, StringComparison.Ordinal);
        Assert.Contains("WindowsStablePathCustody.TryAcquireFile", observerChain, StringComparison.Ordinal);
        Assert.Contains("OpenJob(jobName, JobObjectQuery)", observerChain, StringComparison.Ordinal);
        Assert.Contains("OpenJobObject(access, inheritHandle: false, jobName)", observerChain,
            StringComparison.Ordinal);
        Assert.Contains("QueryInformationJobObject(", observerChain, StringComparison.Ordinal);
        Assert.Contains("JobObjectBasicAccountingInformation", observerChain,
            StringComparison.Ordinal);
        Assert.DoesNotContain("OpenJob(jobName, JobObjectAssignProcess", observerChain,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JobObjectAssignProcess", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignProcessToJobObject", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("TerminateJobObject", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("SetInformationJobObject", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAcquire(", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenOrCreateJob", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateJobObject", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("FileMode.OpenOrCreate", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Create", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Create", observerChain, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", observerChain, StringComparison.Ordinal);
        Assert.Contains("ManagedSetupTransactionCodec.Serialize", materializer, StringComparison.Ordinal);
        Assert.Contains("ManagedSetupTransactionCodec.ReadAsync", materializer, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseMarker", materializer, StringComparison.Ordinal);
        Assert.Contains("MaximumDocumentBytes = 64 * 1024", codec, StringComparison.Ordinal);
    }
}
