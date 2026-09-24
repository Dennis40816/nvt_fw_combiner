using System.Buffers.Binary;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Real adapter and official catalog closure for captured AB authoring.</summary>
public sealed class AbCtrlRamAuthoringTests
{
    /// <summary>The captured NT51950 AB output reaches the existing Single CtrlRAM authoring route.</summary>
    [Fact]
    public async Task Nt51950AbSingleAuthoringUsesItsExistingProfiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }
        string expectedDirectory = Path.Combine(CanonicalGoldenTestData.Root, "NT51950", "ab-merge",
            "boe-d82t80", "topology-unscoped", "nt51950-ab-boe-d82t80", "expected");
        byte[] reference = File.ReadAllBytes(Directory.GetFiles(expectedDirectory, "*.bin").Single());
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = reference;
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-950-normal.bin");
        bytes["replace-ctrlram-normal"] = reference.AsSpan(0x25610, 0x5C00).ToArray();
        bytes["replace-ctrlram-normal"][0] ^= 0x5A;
        CtrlRamAuthoringSessionPreparation result = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51950", "single", paths, bytes,
            new AbCtrlRamDraftState(AbCtrlRamBankSelection.B));
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
        RuntimeReferenceBankReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(
            result.AcceptedSession!.ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
        Assert.Equal("b-bank", Assert.Single(context.Banks).BankId);
        Assert.Equal("nt51950-ctrlram-replace-fw200-single", context.Banks[0].LocalComposition.V2Details.ProfileId);
        using TempWorkspace workspace = TempWorkspace.Create("ab-950-single-build");
        CompositionRunResult built = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, result.AcceptedSession, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, built.Status);
        Assert.Equal(reference[..0x40000], built.OutputBytes[..0x40000].ToArray());
        Assert.Equal(bytes["replace-ctrlram-normal"][0], built.OutputBytes.Span[0x40000 + 0x25610]);
        Assert.Equal(reference.AsSpan(0x4A100, 4).ToArray(), built.OutputBytes.Slice(0x4A100, 4).ToArray());
        Assert.Equal(reference.AsSpan(0x4A110, 4).ToArray(), built.OutputBytes.Slice(0x4A110, 4).ToArray());
        await AssertMatchesLocalControlsAsync("NT51950", "single", AbCtrlRamBankSelection.B,
            reference, paths, bytes, built.OutputBytes, 0x40000, 0x40000);
    }

    /// <summary>Single-bank selection changes only that bank; Both uses the same shared source.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public async Task Nt51950AbSingleAAndBothRespectSelection(AbCtrlRamBankSelection selection)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }
        string expectedDirectory = Path.Combine(CanonicalGoldenTestData.Root, "NT51950", "ab-merge",
            "boe-d82t80", "topology-unscoped", "nt51950-ab-boe-d82t80", "expected");
        byte[] reference = File.ReadAllBytes(Directory.GetFiles(expectedDirectory, "*.bin").Single());
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = reference;
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-950-normal.bin");
        bytes["replace-ctrlram-normal"] = reference.AsSpan(0x25610, 0x5C00).ToArray();
        bytes["replace-ctrlram-normal"][0] ^= 0x5A;
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51950", "single", paths, bytes,
            new AbCtrlRamDraftState(selection));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        using TempWorkspace workspace = TempWorkspace.Create("ab-950-selection-build");
        CompositionRunResult built = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, prepared.AcceptedSession!, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, built.Status);
        Assert.Equal(bytes["replace-ctrlram-normal"][0], built.OutputBytes.Span[0x25610]);
        if (selection == AbCtrlRamBankSelection.A)
        {
            Assert.Equal(reference[0x40000..], built.OutputBytes[0x40000..].ToArray());
        }
        else
        {
            Assert.Equal(bytes["replace-ctrlram-normal"][0], built.OutputBytes.Span[0x40000 + 0x25610]);
        }
        await AssertMatchesLocalControlsAsync("NT51950", "single", selection,
            reference, paths, bytes, built.OutputBytes, 0x40000, 0x40000);
    }

    /// <summary>The remaining exact Partial-family routes build B via their existing local profile.</summary>
    [Theory]
    [InlineData("NT51950", "cascade", "nt51951-fw200-cascade2-auto-prj-599-20260731", 0x40000, 0x80000)]
    [InlineData("NT51951", "single", "nt51951-fw200-single-auto-prj-695-20260718", 0x80000, 0x80000)]
    [InlineData("NT51951", "cascade", "nt51951-fw200-cascade2-auto-prj-599-20260731", 0x80000, 0x80000)]
    public async Task PartialFamilyBOnlyBuildPreservesAAndInactiveBankTail(
        string member, string number, string sourceCase, int localLength, int bankLength)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", sourceCase);
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] source = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        byte[] bank = new byte[bankLength];
        source.AsSpan(0, localLength).CopyTo(bank);
        if (localLength < bankLength)
        {
            bank.AsSpan(localLength).Fill(0x5A);
        }
        byte[] reference = [.. bank, .. bank];
        foreach (int field in new[] { 0xA100, 0xA110, 0xA120 })
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(reference.AsSpan(bankLength + field, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(bankLength + field, 4),
                checked(address + (uint)bankLength));
        }
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = reference;
        if (number == "cascade")
        {
            _ = paths.Remove("replace-ctrlram-nf");
            _ = bytes.Remove("replace-ctrlram-nf");
        }
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-partial-normal.bin");
        bytes["replace-ctrlram-normal"] = source.AsSpan(0x25610, 0x5C00).ToArray();
        bytes["replace-ctrlram-normal"][0] ^= 0x5A;
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), member, number, paths, bytes,
            new AbCtrlRamDraftState(AbCtrlRamBankSelection.B));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        using TempWorkspace workspace = TempWorkspace.Create("ab-partial-build");
        CompositionRunResult built = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, prepared.AcceptedSession!, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, built.Status);
        Assert.Equal(reference[..bankLength], built.OutputBytes[..bankLength].ToArray());
        Assert.Equal(bytes["replace-ctrlram-normal"][0], built.OutputBytes.Span[bankLength + 0x25610]);
        if (localLength < bankLength)
        {
            Assert.Equal(reference.AsSpan(bankLength + localLength, bankLength - localLength).ToArray(),
                built.OutputBytes.Slice(bankLength + localLength, bankLength - localLength).ToArray());
        }
        await AssertMatchesLocalControlsAsync(member, number, AbCtrlRamBankSelection.B,
            reference, paths, bytes, built.OutputBytes, localLength, bankLength);
    }

    /// <summary>A three-IC AB Reference reaches the accepted bank planner for each Perfect-family member.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void CascadeAuthoringAcceptsThreeIcReference(string member)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = AbCtrlRamReferencePlanTests.CascadeReference();
        _ = paths.Remove("replace-ctrlram-nf");
        _ = bytes.Remove("replace-ctrlram-nf");
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-cascade-normal.bin");
        bytes["replace-ctrlram-normal"] = bytes[CompositionSlotIds.ReplaceBase].AsSpan(0x21B90, 0x4A00).ToArray();
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation result = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            state, member, "cascade_2to8", paths, bytes, new AbCtrlRamDraftState(AbCtrlRamBankSelection.B));
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
        RuntimeReferenceBankReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(
            result.AcceptedSession!.ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
        Assert.Equal("b-bank", Assert.Single(context.Banks).BankId);
    }

    /// <summary>The real Cascade tool updates only B and retains B-bank header addresses.</summary>
    [Fact]
    public async Task CascadeBOnlyBuildKeepsAAndRestoresBHeaders()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        byte[] original = AbCtrlRamReferencePlanTests.CascadeReference();
        original[0x40000 + 0x21B91] ^= 0x33;
        bytes[CompositionSlotIds.ReplaceBase] = original;
        _ = paths.Remove("replace-ctrlram-nf");
        _ = bytes.Remove("replace-ctrlram-nf");
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-cascade-normal.bin");
        bytes["replace-ctrlram-normal"] = original.AsSpan(0x21B90, 0x4A00).ToArray();
        bytes["replace-ctrlram-normal"][0] ^= 0x5A;
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51932", "cascade_2to8", paths, bytes,
            new AbCtrlRamDraftState(AbCtrlRamBankSelection.B));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        using TempWorkspace workspace = TempWorkspace.Create("ab-cascade-build");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, prepared.AcceptedSession!, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(original[..0x40000], result.OutputBytes[..0x40000].ToArray());
        Assert.Equal(bytes["replace-ctrlram-normal"][0], result.OutputBytes.Span[0x40000 + 0x21B90]);
        Assert.Equal(bytes["replace-ctrlram-normal"][1], result.OutputBytes.Span[0x40000 + 0x21B91]);
        Assert.Equal(original.AsSpan(0x40000 + 0x2D100, 0x1400).ToArray(),
            result.OutputBytes.Slice(0x40000 + 0x2D100, 0x1400).ToArray());
        foreach (int offset in new[] { 0x7164, 0x7168, 0x716C })
        {
            Assert.Equal(original.AsSpan(0x40000 + offset, 4).ToArray(),
                result.OutputBytes.Slice(0x40000 + offset, 4).ToArray());
        }
        RuntimeReferenceBankReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(
            prepared.AcceptedSession!.ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
        CompiledReferenceBank bank = Assert.Single(context.Banks);
        ExternalProcessorInvocation invocation = bank.LocalComposition.Plan.OrderedOperations
            .Single(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor).ExternalProcessorInvocation!;
        byte[] control = original.AsSpan(0x40000, 0x40000).ToArray();
        foreach (int offset in new[] { 0x7164, 0x7168, 0x716C })
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(control.AsSpan(offset, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset, 4), address - 0x40000);
        }
        bytes["replace-ctrlram-normal"].AsSpan().CopyTo(control.AsSpan(0x21B90));
        ExternalProcessorResult processed = await ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor!.TransformAsync(
            new ExternalProcessorRequest("cascade-local-control", invocation.ProcessorId, invocation.ToolBindingId,
                control, invocation.AllowedWriteRanges, IcNumberSelection.FromToken("cascade_2to8"),
                resolvedIcCount: 3, protocolPlan: invocation.ProtocolPlan), TestContext.Current.CancellationToken);
        Assert.True(processed.Succeeded, string.Join("; ", processed.Issues.Select(static issue => issue.Message)));
        byte[] expectedB = processed.OutputBytes.ToArray();
        foreach (int offset in new[] { 0x7164, 0x7168, 0x716C })
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(expectedB.AsSpan(offset, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(expectedB.AsSpan(offset, 4), address + 0x40000);
        }
        byte[] expected = [.. original.AsSpan(0, 0x40000).ToArray(), .. expectedB];
        Assert.Equal(expected, result.OutputBytes.ToArray());
    }

    /// <summary>NT51932 Single copies the exact 4 KiB FWConfig page into B Backup and preserves its adjacent bytes.</summary>
    [Fact]
    public async Task Single32BOnlyBuildCopiesBoundedBackupPage()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        byte[] original = bytes[CompositionSlotIds.ReplaceBase];
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51932", "single", paths, bytes,
            new AbCtrlRamDraftState(AbCtrlRamBankSelection.B));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        using TempWorkspace workspace = TempWorkspace.Create("ab-single-32-backup");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, prepared.AcceptedSession!, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(original[..0x40000], result.OutputBytes[..0x40000].ToArray());
        byte[] expectedBackup = original.AsSpan(0x40000 + 0x1F200, 0x1000).ToArray();
        expectedBackup[0xA00] = bytes["replace-ctrlram-nf"][0];
        "\0NVT"u8.CopyTo(expectedBackup.AsSpan(0xFFC));
        Assert.Equal(expectedBackup, result.OutputBytes.Slice(0x40000 + 0x2E000, 0x1000).ToArray());
        Assert.Equal(original[0x40000 + 0x2DFFF], result.OutputBytes.Span[0x40000 + 0x2DFFF]);
        Assert.Equal(original[0x40000 + 0x2F000], result.OutputBytes.Span[0x40000 + 0x2F000]);
    }

    /// <summary>Pre-input disclosure uses only the exact AB route and never inherits Standard evidence.</summary>
    [Theory]
    [InlineData("NT51919", "single", true)]
    [InlineData("NT51919", "cascade_2to8", true)]
    [InlineData("NT51929", "single", true)]
    [InlineData("NT51929", "cascade_2to8", true)]
    [InlineData("NT51932", "single", true)]
    [InlineData("NT51932", "cascade_2to8", true)]
    [InlineData("NT51929", "2", false)]
    [InlineData("NT51950", "single", true)]
    [InlineData("NT51950", "cascade", true)]
    [InlineData("NT51950", "cascade_2to8", false)]
    [InlineData("NT51951", "single", true)]
    [InlineData("NT51951", "cascade", true)]
    [InlineData("", "single", false)]
    public void AbReferenceReadinessUsesExactRoute(string icId, string number, bool available)
    {
        CapabilityWorkflowReadiness readiness = BootstrapTestHost.Canonical.CtrlRamAuthoring.GetAbReferenceReadiness(icId, number);
        Assert.Equal(available, readiness.IsAvailable);
        Assert.Equal(available, readiness.HasExactRoute);
        Assert.False(readiness.HasReviewedEvidence);
        Assert.Equal(available, readiness.IsEvidencePending);
        Assert.Equal(available ? CapabilityEvidenceStatus.ContractOnly : CapabilityEvidenceStatus.Missing, readiness.EvidenceStatus);
    }

    /// <summary>Explicit AB inspection retains the declared shared input slots before bank admission.</summary>
    [Fact]
    public async Task AbInspectionRetainsSharedDiscoverySlots()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-discovery");
        (_, Dictionary<string, byte[]> bytes) = Inputs();
        string reference = workspace.Write("reference.bin", bytes[CompositionSlotIds.ReplaceBase]);
        string nf = workspace.Write("nf.bin", bytes["replace-ctrlram-nf"]);
        FirmwareInspectionBatchResult result = await BootstrapTestHost.Services.FirmwareInspectionExperience.InspectFirmwareBatchAsync(
            "NT51929",
            [
                new FirmwareInspectionSnapshotInput("base", reference,
                    CtrlRamRequest: new CtrlRamInspectionRequest("single", new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                new FirmwareInspectionSnapshotInput("nf", nf, CtrlRamReplaceAddressSpaceId: "replace-ctrlram-nf"),
            ], TestContext.Current.CancellationToken);
        CtrlRamInspectionDisplay display = Assert.IsType<CtrlRamInspectionDisplay>(result.InspectionsById["base"].CtrlRamDisplay);
        CtrlRamInspectionDisplay declared = BootstrapTestHost.Canonical.CtrlRamAuthoring.GetDiscoveryDisplay("NT51929", "single");
        Assert.NotEmpty(declared.InputSlots);
        Assert.Equal(declared.InputSlots.Select(static slot => (slot.SlotId, slot.Title, slot.Description)),
            display.InputSlots.Select(static slot => (slot.SlotId, slot.Title, slot.Description)));
        Assert.Equal(declared.Regions, display.Regions);
    }

    /// <summary>Each explicit selection reaches the official candidate through the normal session owner.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public void PrepareCapturesCompleteDraft(AbCtrlRamBankSelection selection)
    {
        var draft = new AbCtrlRamDraftState(selection, new(0x21, 0x32), new(0x29, 0x41));
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        CtrlRamAuthoringSessionPreparation result = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(state, "NT51929", "single", paths, bytes, draft);
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot accepted = result.AcceptedSession!;
        Assert.Equal(draft, accepted.DraftState);
        ResolvedCapability capability = accepted.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection)!;
        Assert.Equal(draft, capability.CtrlRamExecutionPlan!.Draft);
        Assert.Equal(CapabilityPublicationStatus.Candidate, capability.Publication.Value);
        RuntimeReferenceBankReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(capability.CompiledComposition.V2Details.Provenance.Context);
        Assert.Equal(selection == AbCtrlRamBankSelection.Both ? 2 : 1, context.Banks.Count);
        Assert.Equal(selection == AbCtrlRamBankSelection.B ? "b-bank" : "a-bank", context.Banks[0].BankId);
        IReadOnlyList<CompiledReferenceBankObservation> observations = accepted.InputSlotStatuses.Single(
            static status => status.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase).Observation.ReferenceBanks;
        Assert.Equal(context.Banks.Select(static bank => bank.BankId), observations.Select(static bank => bank.BankId));
        Assert.All(observations, static bank => Assert.True(bank.Version.IsKnown));
    }

    /// <summary>Independent nullable edits survive the accepted-session Preview/Build path and real tool.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A, true, false)]
    [InlineData(AbCtrlRamBankSelection.B, false, true)]
    [InlineData(AbCtrlRamBankSelection.Both, true, false)]
    [InlineData(AbCtrlRamBankSelection.Both, false, true)]
    [InlineData(AbCtrlRamBankSelection.Both, false, false)]
    public async Task AcceptedSessionBuildUsesBankDraft(AbCtrlRamBankSelection banks, bool editA, bool editB)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        byte[] original = bytes[CompositionSlotIds.ReplaceBase];
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        var draft = new AbCtrlRamDraftState(banks, editA ? new(0x21, 0x32) : null, editB ? new(0x29, 0x41) : null);
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            state, "NT51929", "single", paths, bytes, draft);
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        using TempWorkspace workspace = TempWorkspace.Create("ab-authoring-build");
        CompositionRunResult output = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, prepared.AcceptedSession!, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, output.Status);
        Assert.Equal(output.OutputBytes.ToArray(), File.ReadAllBytes(workspace.PathFor("result.bin")));
        if (banks == AbCtrlRamBankSelection.B)
        {
            Assert.Equal(original[..0x40000], output.OutputBytes[..0x40000].ToArray());
        }
        if (banks == AbCtrlRamBankSelection.A)
        {
            Assert.Equal(original[0x40000..], output.OutputBytes[0x40000..].ToArray());
        }
        foreach (int start in new[] { 0, 0x40000 })
        {
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(original.AsSpan(start, 0x40000), out FirmwareConfigMetadata before));
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(output.OutputBytes.Span.Slice(start, 0x40000), out FirmwareConfigMetadata after));
            bool edited = start == 0 ? editA : editB;
            Assert.Equal(edited ? start == 0 ? 0x21 : 0x29 : before.FirmwareVersion, after.FirmwareVersion);
            Assert.Equal(edited ? start == 0 ? 0x32 : 0x41 : before.FirmwareSubVersion, after.FirmwareSubVersion);
        }
    }

    /// <summary>Unsupported selection and malformed Reference return errors rather than Standard fallback.</summary>
    [Theory]
    [InlineData("NT51950", "single", false)]
    [InlineData("NT51929", "2", false)]
    [InlineData("NT51929", "single", true)]
    public void InvalidAbCannotCreateAcceptance(string ic, string number, bool corrupt)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        if (corrupt)
        {
            bytes[CompositionSlotIds.ReplaceBase][0x47164] ^= 1;
        }
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation result = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            state, ic, number, paths, bytes, new AbCtrlRamDraftState());
        Assert.False(result.Succeeded);
        Assert.Null(result.AcceptedSession);
        Assert.NotEmpty(result.Issues);
        Assert.Null(state.CurrentSnapshot);
    }

    /// <summary>New bank drafts invalidate old leases and remain independent between page sessions.</summary>
    [Fact]
    public void TransitionsCompareFullDraftAndKeepSessionsIndependent()
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        var first = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        var second = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        ICtrlRamAuthoring owner = BootstrapTestHost.Canonical.CtrlRamAuthoring;
        ActiveSessionSnapshot before = owner.PrepareSession(first, "NT51929", "single", paths, bytes, new AbCtrlRamDraftState()).AcceptedSession!;
        ActiveSessionSnapshot isolated = owner.PrepareSession(second, "NT51929", "single", paths, bytes, new AbCtrlRamDraftState()).AcceptedSession!;
        var desired = new AbCtrlRamDraftState(AbCtrlRamBankSelection.B, bVersion: new(0x29, 0x41));
        CtrlRamAuthoringTransitionResult changed = owner.TransitionFirmwareVersionCompilation(first, "NT51929", "single", paths, desired);
        Assert.True(changed.Succeeded, string.Join("; ", changed.Issues.Select(static issue => issue.Message)));
        Assert.Equal(desired, changed.Session!.DraftState);
        Assert.NotEqual(before.AuthoringRevision, changed.Session.AuthoringRevision);
        Assert.False(owner.IsFirmwareVersionConfirmationLeaseCurrent(changed.Session, before));
        Assert.Same(isolated, second.CurrentSnapshot);
        ResolvedCapability old = before.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection)!;
        Assert.False(owner.AdoptInspectedBatch(first, AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(old),
            before.InputSlotStatuses).Succeeded);
        Assert.Same(changed.Session, first.CurrentSnapshot);
    }

    /// <summary>Single Reference callers retain their old null and version-draft behavior.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandardDraftRemainsCompatible(bool edit)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = bytes[CompositionSlotIds.ReplaceBase][..0x40000];
        CtrlRamFirmwareVersionDraftState? draft = edit ? new(0x21, 0x32) : null;
        CtrlRamAuthoringSessionPreparation result = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, draft);
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
        Assert.Equal(draft, result.AcceptedSession!.DraftState);
        _ = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(result.AcceptedSession.ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
    }

    /// <summary>Inspection rejects later Reference changes before observing any bank version.</summary>
    [Fact]
    public void ChangedCapturedReferenceBlocksInspection()
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        CtrlRamAuthoringSessionPreparation result = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, new AbCtrlRamDraftState());
        byte[] changed = bytes[CompositionSlotIds.ReplaceBase];
        changed[0x61000] ^= 1;
        CompiledInputArtifactInspectionResult inspection = CompiledInputArtifactInspectionService.Inspect(
            result.AcceptedSession!.ExactCapability!.CompiledComposition, CompositionAddressSpaceIds.ReferenceBase, changed);
        Assert.True(inspection.BlocksBuild);
        Assert.Equal("input.bank-reference.identity-mismatch", inspection.IssueCode);
    }

    /// <summary>Adopting an asynchronously inspected batch retains its complete compiled AB draft.</summary>
    [Fact]
    public void BatchAdoptionRetainsCompiledDraft()
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        var draft = new AbCtrlRamDraftState(AbCtrlRamBankSelection.B, bVersion: new(0x29, 0x41));
        ActiveSessionSnapshot prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, draft).AcceptedSession!;
        ResolvedCapability capability = prepared.ExactCapability!;
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses = AuthoringInputSlotInspectionService.InspectBatch(
            capability, new AuthoringRevision(1), new Dictionary<string, ReadOnlyMemory<byte>?>
            {
                [CompositionAddressSpaceIds.ReferenceBase] = bytes[CompositionSlotIds.ReplaceBase],
                ["replace-ctrlram-nf"] = bytes["replace-ctrlram-nf"],
            }, new Dictionary<string, string>
            {
                [CompositionAddressSpaceIds.ReferenceBase] = paths[CompositionSlotIds.ReplaceBase],
                ["replace-ctrlram-nf"] = paths["replace-ctrlram-nf"],
            });
        var destination = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        AuthoringSessionTransitionResult result = BootstrapTestHost.Canonical.CtrlRamAuthoring.AdoptInspectedBatch(
            destination, AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability), [.. statuses.Values]);
        Assert.True(result.Succeeded);
        Assert.Equal(draft, result.Snapshot!.DraftState);
    }

    /// <summary>Bank-local placeholder findings identify the correct full Reference range.</summary>
    [Fact]
    public void BReferenceDiagnosticUsesOuterCoordinates()
    {
        (_, Dictionary<string, byte[]> bytes) = Inputs();
        byte[] reference = bytes[CompositionSlotIds.ReplaceBase];
        reference[0x3000] = 1;
        reference[0x3001] = 2;
        reference[0x43000] = reference[0x43001] = 0xFF;
        // Synthetic obligation tests transport only, without changing admitted profile semantics.
        CompiledComposition composition = AbCtrlRamRuntimeWiringTests.Compile(reference, true, true,
            extraTestValidation: CompiledValidationRequirements.RejectUniformInputRanges("uniform", CompiledValidationSeverity.Error,
                "test.uniform", "reference-base", [new ByteRange(0x3000, 2)]));
        CompiledInputArtifactInspectionResult result = CompiledInputArtifactInspectionService.Inspect(composition, "reference-base", reference);
        Assert.True(result.BlocksBuild);
        Assert.Equal("test.uniform", result.IssueCode);
        Assert.Equal(new ByteRange(0x43000, 2), result.DiagnosticEvidence!.SourceRange);
        Assert.Equal((byte)0xFF, result.DiagnosticEvidence.RepeatedByte);
        Assert.Contains("b-bank", result.AdmissionIssue!.Message, StringComparison.Ordinal);
    }

    /// <summary>Editing only a disabled bank is explicitly rejected without replacing accepted state.</summary>
    [Fact]
    public void UnselectedBankVersionTransitionIsExplicitlyRejected()
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = Inputs();
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        ICtrlRamAuthoring owner = BootstrapTestHost.Canonical.CtrlRamAuthoring;
        ActiveSessionSnapshot before = owner.PrepareSession(state, "NT51929", "single", paths, bytes,
            new AbCtrlRamDraftState(AbCtrlRamBankSelection.A)).AcceptedSession!;
        CtrlRamAuthoringTransitionResult result = owner.TransitionFirmwareVersionCompilation(state, "NT51929", "single", paths,
            new AbCtrlRamDraftState(AbCtrlRamBankSelection.A, bVersion: new(0x29, 0x41)));
        Assert.False(result.Succeeded);
        Assert.Equal("authoring.ctrlram.unselected-bank-version", Assert.Single(result.Issues).Code);
        Assert.Same(before, state.CurrentSnapshot);
    }

    /// <summary>A uniform warning must not populate the blocking TP admission channel.</summary>
    [Fact]
    public void PositiveCountTpUniformWarningRemainsNonBlocking()
    {
        (_, Dictionary<string, byte[]> inputs) = Inputs();
        byte[] bytes = inputs[CompositionSlotIds.ReplaceBase][..0x40000];
        bytes[0x3000] = bytes[0x3001] = 0xFF;
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(bytes, out FirmwareConfigMetadata metadata));
        Assert.True(metadata.ChipNumber > 0);
        TrustedProfileBundleCatalog catalog = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51919-nt51929-nt51932-ab-merge", "892af5d0f1ff0094bb96a0e30ffad3b6c2cf18451a6705623c2ca97206422c6b");
        CompiledComposition original = catalog.Compile("nt51929-ab-merge", "0.4.0", "NT51929", ExperienceIds.AbMerge,
            0x80000, null, [], selectedInputSlotIds: ["tp-a-input"]).CompiledComposition!;
        V2CompiledCompositionDetails details = original.V2Details;
        V2CompilationProvenance p = details.Provenance;
        CompiledInputSpaceBinding binding = details.InputContract.SpaceBindings.Single(static binding => binding.SlotId == "tp-a-input");
        var provenance = new V2CompilationProvenance(p.Bundle, p.ProfileEntry, p.Context, p.Promotion, p.ProfileEvidenceRefs,
            [CompiledValidationRequirements.RejectUniformInputRanges("warning", CompiledValidationSeverity.Warning,
                "test.uniform-warning", binding.AddressSpaceId, [new ByteRange(0x3000, 2)])], p.RequiredCapabilities);
        CompiledComposition compiled = CompiledComposition.CreateV2(original.Plan,
            new V2CompiledCompositionDetails(details.ProfileId, details.ProfileVersion, details.ExperienceId, details.CompositionKind,
                provenance, details.InputContract, details.RegionAccessContract, details.OutputNamingRequirement, details.IcNumberInputMode));
        CompiledInputArtifactInspectionResult inspection = CompiledInputArtifactInspectionService.Inspect(compiled, binding.AddressSpaceId, bytes);
        Assert.Equal("test.uniform-warning", inspection.IssueCode);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Warning, inspection.Severity);
        Assert.False(inspection.BlocksBuild);
        Assert.Null(inspection.AdmissionIssue);
    }

    private static async Task AssertMatchesLocalControlsAsync(string member, string number,
        AbCtrlRamBankSelection selection, byte[] reference, Dictionary<string, string> paths,
        Dictionary<string, byte[]> bytes, ReadOnlyMemory<byte> actual, int localLength, int bankLength)
    {
        byte[] expected = [.. reference];
        foreach (int bankBase in new[] { 0, bankLength })
        {
            if ((bankBase == 0 && selection == AbCtrlRamBankSelection.B) ||
                (bankBase != 0 && selection == AbCtrlRamBankSelection.A))
            {
                continue;
            }
            byte[] localReference = reference.AsSpan(bankBase, localLength).ToArray();
            if (bankBase != 0)
            {
                foreach (int field in new[] { 0xA100, 0xA110, 0xA120 })
                {
                    uint address = BinaryPrimitives.ReadUInt32LittleEndian(localReference.AsSpan(field, sizeof(uint)));
                    Assert.True(address >= bankLength);
                    BinaryPrimitives.WriteUInt32LittleEndian(localReference.AsSpan(field, sizeof(uint)),
                        address - (uint)bankLength);
                }
            }
            Dictionary<string, byte[]> localBytes = bytes.ToDictionary(static entry => entry.Key,
                static entry => entry.Value, StringComparer.Ordinal);
            localBytes[CompositionSlotIds.ReplaceBase] = localReference;
            CtrlRamAuthoringSessionPreparation localPrepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.CtrlRamReplace), member, number, paths, localBytes);
            Assert.True(localPrepared.Succeeded,
                string.Join("; ", localPrepared.Issues.Select(static issue => issue.Message)));
            Assert.IsNotType<RuntimeReferenceBankReplaceV2CompilationContext>(
                localPrepared.AcceptedSession!.ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
            using TempWorkspace workspace = TempWorkspace.Create("ab-partial-local-control");
            CompositionRunResult local = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
                BootstrapTestHost.Canonical, localPrepared.AcceptedSession, paths, true,
                workspace.PathFor("local.bin"), ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor,
                TestContext.Current.CancellationToken);
            Assert.Equal(CompositionExecutionStatus.Succeeded, local.Status);
            Assert.Equal(localLength, local.OutputBytes.Length);
            local.OutputBytes.Span.CopyTo(expected.AsSpan(bankBase, localLength));
            if (bankBase == 0)
            {
                continue;
            }
            foreach (int field in new[] { 0xA100, 0xA110, 0xA120 })
            {
                uint address = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(bankBase + field, sizeof(uint)));
                BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankBase + field, sizeof(uint)),
                    checked(address + (uint)bankLength));
            }
            uint crc = AbMergeGoldenRegressionTests.CalculateCrc32Mpeg2(expected.AsSpan(bankBase + 0xA100, 0x30));
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankBase + 0xA130, sizeof(uint)), crc);
            Assert.Equal(crc, BinaryPrimitives.ReadUInt32LittleEndian(actual.Span.Slice(bankBase + 0xA130, sizeof(uint))));
        }
        Assert.Equal(expected, actual.ToArray());
    }

    internal static (Dictionary<string, string> Paths, Dictionary<string, byte[]> Bytes) Inputs()
    {
        // Intentionally nonexistent paths: every byte must come from the one captured dictionary.
        return (new(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = Path.Combine(Path.GetTempPath(), "captured-ab-reference.bin"),
            ["replace-ctrlram-nf"] = Path.Combine(Path.GetTempPath(), "captured-shared-nf.bin"),
        }, new(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51929", "expected-output", "t05-d06")),
            ["replace-ctrlram-nf"] = [0xA5],
        });
    }
}
