using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Real AB profile observations, separate from pending format/run admission.</summary>
public sealed class AbMergePrimaryFirmwareConfigTests
{
    private const int Primary = 0x22200;
    private const int Backup = 0x36000;

    /// <summary>Native A/B observations share the canonical definition, not their located binding.</summary>
    [Theory]
    [InlineData("NT51950", 1)]
    [InlineData("NT51950", 2)]
    [InlineData("NT51951", 0)]
    public void CompiledChoicesRetainSharedDefinitionAndSeparateNativeInputs(string icId, int count)
    {
        MetadataPlanDefinition plan = CreatePlan(icId, count);
        MetadataPlanEntry[] primaries = PrimaryEntries(plan);
        Assert.Equal(["tp-a-input", "tp-b-input"], primaries.Select(entry => entry.SlotId));
        // TP-SVN-MODEL-1113-01: the only other entries are the two display-only TP SVN stamps.
        Assert.Equal(["tp-a-svn", "tp-b-svn"], plan.Entries.Except(primaries)
            .Select(static entry => entry.StructureDefinition.StructureId).Order(StringComparer.Ordinal));
        Assert.All(plan.Entries.Except(primaries), static entry =>
            Assert.Equal([MetadataReferencePurpose.Display], entry.Purposes));
        FirmwareMetadataStructure a = primaries[0].StructureDefinition;
        FirmwareMetadataStructure b = primaries[1].StructureDefinition;
        Assert.NotSame(a, b);
        Assert.Same(a.Definition, b.Definition);
        BuiltInV2Registration provider = BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51927"];
        provider.TryCompile(null, out CompiledComposition? compiled, out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        FirmwareMetadataStructure canonical = Assert.Single(provider.CreateMetadataPlan(compiled!).Entries,
            entry => entry.StructureDefinition.Definition.DefinitionId == "firmware-config-general-parameters").StructureDefinition;
        Assert.Same(canonical.Definition, a.Definition);
        Assert.Equal(36, a.Fields.Count);
        _ = Assert.Single(a.Relations);
        Assert.All(primaries, entry =>
        {
            Assert.Equal([MetadataReferencePurpose.Inspection], entry.Purposes);
            Assert.Equal(entry.SlotId, entry.StructureDefinition.ArtifactBindingId);
        });
    }

    /// <summary>Different valid Backup values cannot substitute either primary observation.</summary>
    [Theory]
    [InlineData("NT51950", 1)]
    [InlineData("NT51950", 2)]
    [InlineData("NT51951", 0)]
    public void PrimaryBytesRemainSlotSpecificRegardlessOfBackup(string icId, int count)
    {
        MetadataInspectionResult[] results = Primaries(Inspect(CreatePlan(icId, count), CreateTp(0x97), CreateTp(0xA6)));
        Assert.Equal(2, results.Length);
        AssertPrimary(results[0], "tp-a-input", 0x97, valid: true);
        AssertPrimary(results[1], "tp-b-input", 0xA6, valid: true);
    }

    /// <summary>A decoded byte is not valid-format evidence when its canonical relation fails.</summary>
    [Theory]
    [InlineData("NT51950", 1)]
    [InlineData("NT51950", 2)]
    [InlineData("NT51951", 0)]
    public void InvalidPrimaryComplementRetainsFalseRelationWithoutBackupFallback(string icId, int count)
    {
        byte[] tp = CreateTp(0x97);
        tp[Primary + 1] = 0;
        MetadataInspectionResult[] results = Primaries(Inspect(CreatePlan(icId, count), tp, CreateTp(0xA6)));
        AssertPrimary(results[0], "tp-a-input", 0x97, valid: false);
        AssertPrimary(results[1], "tp-b-input", 0xA6, valid: true);
    }

    /// <summary>The complete canonical structure, not only the format byte, must be in bounds.</summary>
    [Theory]
    [InlineData(0x2220D)]
    [InlineData(0x22228)]
    public void TruncatedPrimaryDoesNotProduceAValue(int length)
    {
        MetadataInspectionResult[] results = Primaries(Inspect(CreatePlan("NT51951", 0), CreateTp(0x97)[..length], CreateTp(0xA6)));
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, results[0].State);
        Assert.Null(results[0].Resolution?.Resolved);
        AssertPrimary(results[1], "tp-b-input", 0xA6, valid: true);
    }

    /// <summary>Exactly 41 bytes at primary are sufficient; no Backup is required by this binding.</summary>
    [Fact]
    public void CompletePrimaryWithoutBackupCanBeObserved()
    {
        MetadataInspectionResult[] results = Primaries(Inspect(CreatePlan("NT51951", 0), CreateTp(0x97)[..0x22229], CreateTp(0xA6)));
        AssertPrimary(results[0], "tp-a-input", 0x97, valid: true);
    }

    /// <summary>A missing TPA remains pending and cannot borrow TPB facts.</summary>
    [Fact]
    public void MissingInputKeepsTypedWaitingState()
    {
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            CreatePlan("NT51951", 0).Resolve(new ResolutionToken("ab-primary-test")),
            [new FirmwareArtifactPayload("tp-b-input", CreateTp(0xA6))]);
        MetadataInspectionResult[] results = Primaries(snapshot);
        Assert.Equal(MetadataInspectionState.WaitingForArtifact, results[0].State);
        Assert.Equal(FirmwareMetadataStructureResolutionFailure.MissingArtifact, results[0].Resolution?.Failure);
        AssertPrimary(results[1], "tp-b-input", 0xA6, valid: true);
    }

    /// <summary>The actual shared authoring path delivers metadata without replacing Backup versions.</summary>
    [Fact]
    public async Task AuthoringBatchCarriesPrimaryObservationsWithoutChangingVersionFacts()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-primary-authoring");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, localStateDirectory: IsolatedLocalState.CreateDirectory(), configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        CompiledAuthoringSessionPreparation first = await PrepareAsync(host, 0x97);
        CompiledAuthoringSessionPreparation second = await PrepareAsync(host, 0x84, primaryVersion: 0x55);
        Assert.True(first.Succeeded, string.Join(",", first.Issues.Select(issue => issue.Code)));
        Assert.True(second.Succeeded, string.Join(",", second.Issues.Select(issue => issue.Code)));
        MetadataInspectionSnapshot metadata = Assert.IsType<MetadataInspectionSnapshot>(first.Inspection!.MetadataInspection);
        AssertPrimary(Primaries(metadata)[0], "tp-a-input", 0x97, valid: true);
        AssertPrimary(Primaries(second.Inspection!.MetadataInspection!)[0], "tp-a-input", 0x84, valid: true);
        Assert.Equal(first.Inspection.Statuses["tp-a-input"].Observation.Versions,
            second.Inspection.Statuses["tp-a-input"].Observation.Versions);
    }

    private static ValueTask<CompiledAuthoringSessionPreparation> PrepareAsync(CompositionHostServices host, byte format, byte primaryVersion = 0x31)
    {
        return host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51951", null,
            [new("tp-a-input", "tp-a.bin", CreateTp(format, primaryVersion)), new("tp-b-input", "tp-b.bin", CreateTp(format))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
    }

    private static MetadataPlanDefinition CreatePlan(string icId, int count)
    {
        string mapVariant = icId == "NT51950"
            ? count == 1 ? "nt51950-ab-merge-maps" : "nt51950-ab-cascade-maps"
            : "nt51951-ab-merge-1024k";
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.FindAbMergeRegistration(icId, mapVariant)!;
        registration.TryCompile(null,
            count == 0 ? null : new TopologySelection(count, "test", TopologySelectionSource.Requested, "test"),
            out CompiledComposition? compiled, out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return registration.CreateMetadataPlan(Assert.IsType<CompiledComposition>(compiled));
    }

    private static bool IsPrimary(MetadataPlanEntry entry)
    {
        return entry.StructureDefinition.StructureId is "tp-a-primary-firmware-config" or "tp-b-primary-firmware-config";
    }

    private static MetadataPlanEntry[] PrimaryEntries(MetadataPlanDefinition plan)
    {
        return [.. plan.Entries.Where(IsPrimary)];
    }

    private static MetadataInspectionResult[] Primaries(MetadataInspectionSnapshot snapshot)
    {
        return [.. snapshot.Results.Where(static result => IsPrimary(result.PlanEntry.Definition))];
    }

    private static MetadataInspectionSnapshot Inspect(MetadataPlanDefinition plan, byte[] a, byte[] b)
    {
        return FirmwareMetadataInspector.Inspect(plan.Resolve(new ResolutionToken("ab-primary-test")),
            [new FirmwareArtifactPayload("tp-a-input", a), new FirmwareArtifactPayload("tp-b-input", b)]);
    }

    private static void AssertPrimary(MetadataInspectionResult result, string slotId, byte format, bool valid)
    {
        Assert.Equal(slotId, result.PlanEntry.Definition.SlotId);
        Assert.Equal(MetadataInspectionState.Value, result.State);
        FirmwareResolvedMetadataStructure resolved = Assert.IsType<FirmwareResolvedMetadataStructure>(result.Resolution?.Resolved);
        Assert.Equal(new ByteRange(Primary, 41), resolved.LocatorOutcome.ResolvedRange.Range);
        FirmwareDecodedMetadataFact fact = Assert.Single(resolved.DecodedStructure.Facts, fact => fact.FieldId == "event-buffer-format-version");
        Assert.Equal(slotId, fact.ArtifactBindingId);
        Assert.Equal(format, fact.Value.UnsignedIntegerValue);
        Assert.Equal(valid, Assert.Single(resolved.DecodedStructure.Relations).IsSatisfied);
    }

    private static byte[] CreateTp(byte format, byte primaryVersion = 0x31)
    {
        byte[] tp = new byte[0x37000];
        tp[Primary] = primaryVersion;
        tp[Primary + 1] = (byte)~primaryVersion;
        tp[Primary + 12] = format;
        tp[Backup] = 0x42;
        tp[Backup + 1] = 0xBD;
        tp[Backup + 12] = format == 0x97 ? (byte)0x84 : (byte)0x85;
        tp[Backup + 23] = 2;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        return tp;
    }
}
