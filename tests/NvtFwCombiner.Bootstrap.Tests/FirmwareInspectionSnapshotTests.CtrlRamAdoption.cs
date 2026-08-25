using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>A reportful CtrlRAM reference resolves one bounded read-only DPCMI display plan.</summary>
    [Fact]
    public async Task Nt51926ReportfulCtrlRamReferenceUsesOneBoundedDpcmiQuery()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement[] artifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        string basePath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "expected-output"));
        string normalPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "normal-ctrlram-input"));
        var calls = new List<(
            string IcId,
            string WorkflowId,
            string IcCountVariant,
            long? Capacity)>();
        ICanonicalCapabilityQuery queryCatalog = BootstrapTestHost.Canonical.Catalog;
        BuiltInFirmwareInspection firmwareInspection = CreateInspection(
            new InterceptingMetadataPlanQuery(
                queryCatalog,
                (icId, workflowId, icCountVariant, outputCapacity) =>
                {
                    calls.Add((icId, workflowId, icCountVariant, outputCapacity));
                    return queryCatalog.ResolveUniqueMetadataPlan(
                        icId,
                        workflowId,
                        icCountVariant,
                        outputCapacity);
                }),
            new DelegatingContentInspector(static (path, _, _) =>
            {
                byte[] bytes = File.ReadAllBytes(path);
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    Path.GetFileName(path),
                    acceptedBytes: bytes));
            }));

        FirmwareInspectionBatchResult batch = await firmwareInspection.InspectFirmwareBatchAsync(
            "NT51926",
            [
                new FirmwareInspectionSnapshotInput(
                    "base",
                    basePath,
                    CtrlRamRequest: new CtrlRamInspectionRequest(
                        IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId:
                        CompositionAddressSpaceIds.ReferenceBase),
                new FirmwareInspectionSnapshotInput(
                    "normal",
                    normalPath,
                    CtrlRamReplaceAddressSpaceId: "replace-ctrlram-normal"),
            ],
            TestContext.Current.CancellationToken);

        FirmwareInspectionSnapshot baseInspection = batch.InspectionsById["base"];
        AuthoringCapabilityCatalogSnapshot catalog = Assert.IsType<
            AuthoringCapabilityCatalogSnapshot>(baseInspection.InputSlotCatalog);
        ResolvedCapability exact = Assert.IsType<ResolvedCapability>(
            Assert.Single(catalog.Routes).ExactCapability);
        Assert.DoesNotContain(exact.MetadataPlan.Entries, entry =>
            StringComparer.Ordinal.Equals(
                entry.Definition.StructureDefinition.Definition.DefinitionId,
                DpcmiMetadataContract.StructureId));
        Assert.NotEmpty(exact.MetadataPlan.Definition.ReportProjections);
        Assert.Equal(
            [("NT51926", ExperienceIds.DpReplace, "1-ic", 0x40000L)],
            calls);
        Assert.Equal(
            "0200",
            Assert.IsType<DpVersionMetadata>(baseInspection.DpVersion).VersionToken);
        Assert.Equal(
            (ushort)597,
            Assert.IsType<CmiDpCodeMetadata>(baseInspection.CmiDpCode).JiraNumber);
        Assert.Null(baseInspection.DpMetadataPrerequisite);
    }

    /// <summary>An exact reportless CtrlRAM route cannot fall back to DP metadata.</summary>
    [Fact]
    public async Task Nt51950ReportlessCtrlRamInspectionKeepsMetadataAbsent()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement[] artifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        string basePath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "expected-output"));
        string nfPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "postbuild-nf-ctrlram"));
        BuiltInFirmwareInspection firmwareInspection = CreateInspection(
            new InterceptingMetadataPlanQuery(
                BootstrapTestHost.Canonical.Catalog,
                static (_, _, _, _) => throw new InvalidOperationException(
                    "An exact reportless CtrlRAM route must remain terminal.")),
            new DelegatingContentInspector(static (path, _, _) =>
            {
                byte[] bytes = File.ReadAllBytes(path);
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    Path.GetFileName(path),
                    acceptedBytes: bytes));
            }));

        FirmwareInspectionBatchResult batch = await firmwareInspection.InspectFirmwareBatchAsync(
            "NT51950",
            [
                new FirmwareInspectionSnapshotInput(
                    "base",
                    basePath,
                    CtrlRamRequest: new CtrlRamInspectionRequest(
                        IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId:
                        CompositionAddressSpaceIds.ReferenceBase),
                new FirmwareInspectionSnapshotInput(
                    "nf",
                    nfPath,
                    CtrlRamReplaceAddressSpaceId: "replace-ctrlram-nf"),
            ],
            TestContext.Current.CancellationToken);

        FirmwareInspectionSnapshot inspection = batch.InspectionsById["base"];
        AuthoringCapabilityCatalogSnapshot catalog =
            Assert.IsType<AuthoringCapabilityCatalogSnapshot>(inspection.InputSlotCatalog);
        ResolvedCapability exact = Assert.IsType<ResolvedCapability>(
            Assert.Single(catalog.Routes).ExactCapability);
        Assert.Empty(exact.MetadataPlan.Entries);
        Assert.NotNull(inspection.InputSlotStatus);
        Assert.Null(inspection.DpVersion);
        Assert.Null(inspection.CmiDpCode);
        Assert.Null(inspection.DpMetadataPrerequisite);
    }

    /// <summary>NT51923 adopts each bounded inspection once and retains one exact route instance.</summary>
    [Fact]
    public async Task Nt51923CtrlRamAdoptionReusesInspectedBytesAndEquivalentCapability()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51923-fw141-single-auto-prj-662-20260717");
        JsonElement[] artifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        string basePath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "expected-output"));
        string normalPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "postbuild-normal-ctrlram"));
        using var workspace = TempWorkspace.Create("nfc-ctrlram-adoption-path-change");
        string secondBasePath = workspace.Write("base-b.bin", File.ReadAllBytes(basePath));
        string secondNormalPath = workspace.Write("normal-b.bin", File.ReadAllBytes(normalPath));
        var metadataCalls = new List<(
            string IcId,
            string WorkflowId,
            string IcCountVariant,
            long? Capacity)>();
        ICanonicalCapabilityQuery queryCatalog = BootstrapTestHost.Canonical.Catalog;
        BuiltInFirmwareInspection inspection = CreateInspection(
            new InterceptingMetadataPlanQuery(
                queryCatalog,
                (icId, workflowId, icCountVariant, outputCapacity) =>
                {
                    metadataCalls.Add((
                        icId,
                        workflowId,
                        icCountVariant,
                        outputCapacity));
                    return queryCatalog.ResolveUniqueMetadataPlan(
                        icId,
                        workflowId,
                        icCountVariant,
                        outputCapacity);
                }),
            new DelegatingContentInspector(static (path, _, _) =>
            {
                byte[] bytes = File.ReadAllBytes(path);
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes),
                    Path.GetFileName(path),
                    acceptedBytes: bytes));
            }));
        ICtrlRamAuthoring authoring = BootstrapTestHost.Services.CtrlRamAuthoring;
        var session = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);

        async Task<(AuthoringCapabilityCatalogSnapshot Catalog,
            AuthoringInputSlotStatus[] Statuses)> InspectAsync(
                long revision,
                string selectedBasePath,
                string selectedNormalPath)
        {
            FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
                "NT51923",
                [
                    new FirmwareInspectionSnapshotInput(
                        "base",
                        selectedBasePath,
                        CtrlRamRequest: new CtrlRamInspectionRequest(
                            IcNumberSelectionTokens.SingleChip),
                        AuthoringRevision: revision,
                        CtrlRamReplaceAddressSpaceId:
                            CompositionAddressSpaceIds.ReferenceBase),
                    new FirmwareInspectionSnapshotInput(
                        "normal",
                        selectedNormalPath,
                        AuthoringRevision: revision,
                        CtrlRamReplaceAddressSpaceId: "replace-ctrlram-normal"),
                ],
                TestContext.Current.CancellationToken);
            FirmwareInspectionSnapshot[] snapshots = [.. batch.InspectionsById.Values];
            FirmwareInspectionSnapshot baseInspection = batch.InspectionsById["base"];
            AuthoringCapabilityCatalogSnapshot catalog = Assert.IsType<
                AuthoringCapabilityCatalogSnapshot>(baseInspection.InputSlotCatalog);
            ResolvedCapability exact = Assert.IsType<ResolvedCapability>(
                Assert.Single(catalog.Routes).ExactCapability);
            Assert.NotEmpty(exact.MetadataPlan.Entries);
            Assert.DoesNotContain(exact.MetadataPlan.Entries, entry =>
                StringComparer.Ordinal.Equals(
                    entry.Definition.StructureDefinition.Definition.DefinitionId,
                    DpcmiMetadataContract.StructureId));
            _ = Assert.IsType<DpVersionMetadata>(baseInspection.DpVersion);
            _ = Assert.IsType<CmiDpCodeMetadata>(baseInspection.CmiDpCode);
            Assert.Null(baseInspection.DpMetadataPrerequisite);
            return (
                catalog,
                [.. snapshots.Select(snapshot => Assert.IsType<AuthoringInputSlotStatus>(
                    snapshot.InputSlotStatus))]);
        }

        (AuthoringCapabilityCatalogSnapshot firstCatalog,
            AuthoringInputSlotStatus[] firstStatuses) = await InspectAsync(
                1,
                basePath,
                normalPath);
        AuthoringSessionTransitionResult first = authoring.AdoptInspectedBatch(
            session,
            firstCatalog,
            firstStatuses);
        Assert.True(first.Succeeded, first.Issue?.Message);
        ActiveSessionSnapshot firstSnapshot = first.Snapshot!;
        Assert.Equal(new AuthoringRevision(2), firstSnapshot.AuthoringRevision);
        Assert.All(firstStatuses, status => Assert.Same(
            status.AcceptedByteArray,
            firstSnapshot.InputSlotStatuses.Single(adopted =>
                adopted.SlotId == status.SlotId).AcceptedByteArray));

        (AuthoringCapabilityCatalogSnapshot secondCatalog,
            AuthoringInputSlotStatus[] secondStatuses) = await InspectAsync(
                firstSnapshot.AuthoringRevision.Value,
                secondBasePath,
                secondNormalPath);
        AuthoringSessionTransitionResult second = authoring.AdoptInspectedBatch(
            session,
            secondCatalog,
            secondStatuses);
        Assert.True(second.Succeeded, second.Issue?.Message);
        ActiveSessionSnapshot secondSnapshot = second.Snapshot!;
        Assert.Equal(new AuthoringRevision(3), secondSnapshot.AuthoringRevision);
        Assert.Same(firstSnapshot.ExactCapability, secondSnapshot.ExactCapability);
        Assert.Equal(
            [secondBasePath, secondNormalPath],
            secondSnapshot.Slots.Select(static slot => slot.SelectedPath).Order(StringComparer.Ordinal));
        Assert.All(secondStatuses, status => Assert.Same(
            status.AcceptedByteArray,
            secondSnapshot.InputSlotStatuses.Single(adopted =>
                adopted.SlotId == status.SlotId).AcceptedByteArray));

        (AuthoringCapabilityCatalogSnapshot concurrentSecondCatalog,
            AuthoringInputSlotStatus[] concurrentSecondStatuses) = await InspectAsync(
                1,
                secondBasePath,
                secondNormalPath);
        var concurrentSession = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        AuthoringSessionTransitionResult[] concurrent = await Task.WhenAll(
            Task.Run(() => authoring.AdoptInspectedBatch(
                concurrentSession,
                firstCatalog,
                firstStatuses),
                TestContext.Current.CancellationToken),
            Task.Run(() => authoring.AdoptInspectedBatch(
                concurrentSession,
                concurrentSecondCatalog,
                concurrentSecondStatuses),
                TestContext.Current.CancellationToken));
        _ = Assert.Single(concurrent, static result => result.Succeeded);
        AuthoringSessionTransitionResult stale = Assert.Single(
            concurrent,
            static result => !result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, stale.Issue!.Code);
        string[] finalPaths =
        [
            .. concurrentSession.CurrentSnapshot!.Slots
                .Select(static slot => slot.SelectedPath!)
                .Order(StringComparer.Ordinal),
        ];
        Assert.True(
            finalPaths.SequenceEqual([basePath, normalPath]) ||
            finalPaths.SequenceEqual([secondBasePath, secondNormalPath]));
        Assert.Equal(
            Enumerable.Repeat(
                ("NT51923", ExperienceIds.DpReplace, "1-ic", (long?)0x40000),
                3),
            metadataCalls);

        var countingAdapter = new CountingCtrlRamAuthoringAdapter(
            new BuiltInCtrlRamAuthoringAdapter(
                BootstrapTestHost.Canonical.Catalog,
                BootstrapTestHost.Canonical.Projection));
        var countedAuthoring = new CtrlRamAuthoringExperience(
            countingAdapter,
            BootstrapTestHost.Services.ExternalEnvironment);
        FirmwareInspectionStatusBatch countedBatch = countedAuthoring.InspectInputSlots(
            "NT51923",
            [
                new FirmwareInspectionSnapshotInput(
                    "base",
                    basePath,
                    CtrlRamRequest: new CtrlRamInspectionRequest(
                        IcNumberSelectionTokens.SingleChip),
                    AuthoringRevision: 1,
                    CtrlRamReplaceAddressSpaceId:
                        CompositionAddressSpaceIds.ReferenceBase),
                new FirmwareInspectionSnapshotInput(
                    "normal",
                    normalPath,
                    AuthoringRevision: 1,
                    CtrlRamReplaceAddressSpaceId: "replace-ctrlram-normal"),
            ],
            static path => File.ReadAllBytes(path));
        Assert.Equal(1, countingAdapter.ResolveCalls);
        (int Resolve, int IsAccepted) callsAfterInspection = countingAdapter.Counts;
        AuthoringSessionTransitionResult countedAdoption = countedAuthoring.AdoptInspectedBatch(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace),
            Assert.IsType<AuthoringCapabilityCatalogSnapshot>(countedBatch.Catalog),
            [.. countedBatch.Statuses.Values]);
        Assert.True(countedAdoption.Succeeded, countedAdoption.Issue?.Message);
        Assert.Equal(callsAfterInspection, countingAdapter.Counts);
    }

    private sealed class CountingCtrlRamAuthoringAdapter(ICtrlRamAuthoringAdapter inner)
        : ICtrlRamAuthoringAdapter
    {
        internal int ResolveCalls { get; private set; }

        internal (int Resolve, int IsAccepted) Counts =>
            (ResolveCalls, IsAcceptedCapabilityCalls);

        private int IsAcceptedCapabilityCalls { get; set; }

        public CtrlRamInspectionDisplay GetDiscoveryDisplay(
            string icId,
            string number,
            string? basePath)
        {
            return inner.GetDiscoveryDisplay(icId, number, basePath);
        }

        public CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(
            string icId,
            string number,
            ReadOnlyMemory<byte> acceptedBaseBytes)
        {
            return inner.GetDiscoveryDisplayFromAcceptedBase(icId, number, acceptedBaseBytes);
        }

        public CtrlRamAuthoringCompilation Resolve(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
            IReadOnlyDictionary<string, byte[]>? selectedInputBytes = null)
        {
            ResolveCalls++;
            return inner.Resolve(
                icId,
                number,
                slotPaths,
                firmwareVersionEdit,
                selectedInputBytes);
        }

        public bool IsAcceptedCapability(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
            IReadOnlyDictionary<string, byte[]>? selectedInputBytes,
            ResolvedCapability capability,
            out IReadOnlyDictionary<string, string> expectedPaths,
            out IReadOnlyList<CompositionIssue> issues)
        {
            IsAcceptedCapabilityCalls++;
            return inner.IsAcceptedCapability(
                icId,
                number,
                slotPaths,
                firmwareVersionEdit,
                selectedInputBytes,
                capability,
                out expectedPaths,
                out issues);
        }
    }
}
