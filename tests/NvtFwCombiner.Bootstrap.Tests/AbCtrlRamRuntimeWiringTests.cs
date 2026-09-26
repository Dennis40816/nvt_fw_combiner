using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Candidate catalog admission and the real Application Preview/Build boundary.</summary>
public sealed class AbCtrlRamRuntimeWiringTests
{
    /// <summary>A damaged OSD A or B Backup remains recognized as AB and reports its bank issue.</summary>
    [Theory]
    [InlineData(0, "version-bar", "input.bank-reference.version-bar")]
    [InlineData(0, "zero-count", "input.bank-reference.count-zero")]
    [InlineData(0, "different-count", "input.bank-reference.count")]
    [InlineData(0, "moved-marker", "input.bank-reference.metadata-unreadable")]
    [InlineData(0, "missing-marker", "input.bank-reference.metadata-unreadable")]
    [InlineData(0x40000, "version-bar", "input.bank-reference.version-bar")]
    [InlineData(0x40000, "zero-count", "input.bank-reference.count-zero")]
    [InlineData(0x40000, "different-count", "input.bank-reference.count")]
    [InlineData(0x40000, "moved-marker", "input.bank-reference.metadata-unreadable")]
    [InlineData(0x40000, "missing-marker", "input.bank-reference.metadata-unreadable")]
    public void Nt51950OsdDamagedBankRemainsTerminalAb(int bankStart, string damage, string issueCode)
    {
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51950-ab-osd-d03t02-20260924");
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        int backup = bankStart + 0x36000;
        switch (damage)
        {
            case "version-bar": reference[backup + 1] ^= 1; break;
            case "zero-count": reference[backup + 0x17] = 0; break;
            case "different-count": reference[backup + 0x17] = 2; break;
            // NVT-END-FLAG-1113-01: a second marker away from the end flag is ignored, so only a moved marker damages the bank.
            case "moved-marker":
                reference[backup + 0xFFC] ^= 1;
                new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(reference, bankStart + 0x1000);
                break;
            case "missing-marker": reference[backup + 0xFFC] ^= 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(damage));
        }
        if (bankStart == 0x40000)
        {
            Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge("NT51950", 0x40000,
                out CompiledComposition? standard, out _, out _));
            Assert.Equal(CompiledFirmwareArtifactKind.FlashCode,
                CompiledFirmwareArtifactClassifier.Classify(standard, reference.AsSpan(0, 0x40000)).Kind);
        }
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = reference;
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-950-osd-normal.bin");
        bytes["replace-ctrlram-normal"] = reference.AsSpan(0x25610, 0x5C00).ToArray();
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950",
                [
                    new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                        CtrlRamRequest: new("single", new AbCtrlRamDraftState()),
                        CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new FirmwareInspectionSnapshotInput("normal", "replace-ctrlram-normal.bin",
                        CtrlRamReplaceAddressSpaceId: "replace-ctrlram-normal"),
                ], path => bytes[Path.GetFileNameWithoutExtension(path)]);
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        Assert.Contains(inspected.Issues, issue => issue.Code == issueCode);
        Assert.Null(inspected.Catalog);
        Assert.Empty(inspected.Statuses);
    }

    /// <summary>A wrong execution Number cannot redefine the captured OSD Base as Standard.</summary>
    [Fact]
    public void Nt51950OsdWrongNumberRetainsAbIdentity()
    {
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51950-ab-osd-d03t02-20260924");
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950", [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new("cascade", new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => reference);
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        Assert.NotEmpty(inspected.Issues);
        Assert.Null(inspected.Catalog);
    }

    /// <summary>A 1 MiB length without canonical bank evidence never grants AB identity.</summary>
    [Fact]
    public void Nt51950OsdCapacityAloneCannotIdentifyAb()
    {
        byte[] malformed = new byte[0x100000];
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950", [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new("single", new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => malformed);
        Assert.NotEqual(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        Assert.Null(inspected.Catalog);
    }

    /// <summary>A synthetic 1 MiB non-AB image with a real Standard prefix is not a Golden or AB Reference.</summary>
    [Fact]
    public void Nt51950StandardPrefixWithSyntheticTailIsNotAb()
    {
        byte[] standard = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "standard-merge", "NT51950", "expected-output"));
        Assert.Equal(0x40000, standard.Length);
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge("NT51950", 0x40000,
            out CompiledComposition? standardPlan, out _, out _));
        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode,
            CompiledFirmwareArtifactClassifier.Classify(standardPlan, standard).Kind);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(standard, out FirmwareConfigMetadata config, out _));
        Assert.True(config.IsFirmwareVersionBarValid);
        byte[] synthetic = [.. Enumerable.Repeat((byte)0xFF, 0x100000)];
        standard.CopyTo(synthetic, 0);
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950", [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new("single", new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => synthetic);
        Assert.NotEqual(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        Assert.Empty(inspected.CtrlRamBaseInspection!.Banks);
    }

    /// <summary>Even two damaged Backups cannot turn a structurally recognized AB Base into executable Standard.</summary>
    [Fact]
    public void Nt51950OsdBothDamagedBanksRemainTerminalAb()
    {
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51950-ab-osd-d03t02-20260924");
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        reference[0x36001] ^= 1;
        reference[0x76001] ^= 1;
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950", [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new("single", new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => reference);
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        Assert.Equal(2, inspected.Issues.Count(static issue => issue.Code == "input.bank-reference.version-bar"));
        Assert.Null(inspected.Catalog);
        Assert.Empty(inspected.Statuses);
    }

    /// <summary>Broken B header relocation with both valid Backups remains terminal AB evidence.</summary>
    [Fact]
    public void Nt51950OsdRelocationDamageRemainsTerminalAb()
    {
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51950-ab-osd-d03t02-20260924");
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        reference[0x4A100] ^= 1;
        foreach (int bankStart in new[] { 0, 0x40000 })
        {
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference.AsSpan(bankStart, 0x40000),
                out FirmwareConfigMetadata config, out _));
            Assert.True(config.IsFirmwareVersionBarValid);
        }
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950", [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new("single", new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => reference);
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        Assert.Contains(inspected.Issues, static issue => issue.Code == "input.bank-reference.invalid");
        Assert.Null(inspected.Catalog);
        Assert.Empty(inspected.Statuses);
    }

    /// <summary>The OSD Base keeps its extra DP tail while each bank selection uses the fixed 512 KiB AB template.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public async Task Nt51950OsdBaseRetainsTailAndReportsActualEnvelope(AbCtrlRamBankSelection selection)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51950-ab-osd-d03t02-20260924");
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = reference;
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "captured-950-osd-normal.bin");
        bytes["replace-ctrlram-normal"] = reference.AsSpan(0x25610, 0x5C00).ToArray();
        bytes["replace-ctrlram-normal"][0] ^= 0x5A;
        TopologySelection chosen = BootstrapTestHost.Canonical.Compiler.GetAbMergeTopologyChoices("NT51950")
            .Single(static choice => choice.Selection.ChipCount == 1).Selection;
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileAbMergeCapability("NT51950", chosen,
            ["dp-ab-input"], out _, out ResolvedCapability? published, out IReadOnlyList<CompositionIssue> compileIssues),
            string.Join("; ", compileIssues.Select(static issue => issue.Message)));
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompilePublishedDynamicCapability(
            published!.Identity, reference.LongLength, [new FirmwareArtifactPayload("dp-ab-input", reference)],
            ["dp-ab-input"], out CompiledComposition? capturedLayout, out _, out compileIssues, chosen),
            string.Join("; ", compileIssues.Select(static issue => issue.Message)));
        Assert.NotNull(capturedLayout);
        FirmwareInspectionStatusBatch inspected =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                "NT51950", [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new("single", new AbCtrlRamDraftState(selection)),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                path => bytes[Path.GetFileNameWithoutExtension(path)]);
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.CtrlRamBaseInspection?.Kind);
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51950", "single", paths, bytes,
            new AbCtrlRamDraftState(selection));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot session = prepared.AcceptedSession!;
        CompiledComposition composition = session.ExactCapability!.CompiledComposition;
        RuntimeReferenceBankReplaceV2CompilationContext context =
            Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(composition.V2Details.Provenance.Context);
        Assert.Equal(0x100000, context.SourceEnvelope?.ActualOutputLength);
        Assert.Equal(0x100000, composition.Plan.OutputInitialization.Capacity);
        if (selection != AbCtrlRamBankSelection.A)
        {
            Assert.Equal(new ByteRange(0, 0x80000), composition.Plan.OrderedOperations.Single(
                static operation => operation.OperationId == "ab-replace/b-finalize").TargetRange);
        }
        Assert.All(composition.Plan.OrderedOperations,
            static operation => Assert.True(operation.TargetRange.EndExclusive <= 0x80000));
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(session.ExactCapability, session, composition);
        Assert.Equal(0x100000, layout.Capacity);
        Assert.Same(context.SourceEnvelope, layout.SourceEnvelope);
        MemoryLayoutSegment tail = Assert.Single(layout.BeforeSegments,
            static segment => segment.Range == new ByteRange(0x80000, 0x80000));
        Assert.Null(tail.CanonicalRegion);
        Assert.Equal(MemoryContentRole.Dp, tail.ContentRole);
        Assert.Equal("reference-base", tail.SourceSlotId);
        MemoryLayoutSectionLocator tailSection = Assert.Single(layout.SectionLocators,
            static section => section.Range == new ByteRange(0x80000, 0x80000));
        Assert.Null(tailSection.Bank);
        Assert.Null(tailSection.CanonicalRegion);
        Assert.Equal(new ByteRange(0, 0x40000), layout.Banks[0].Range);
        Assert.Equal(new ByteRange(0x40000, 0x40000), layout.Banks[1].Range);
        using TempWorkspace workspace = TempWorkspace.Create("ab-950-osd-envelope");
        CompositionRunResult built = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            BootstrapTestHost.Canonical, session, paths, true, workspace.PathFor("result.bin"),
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent().Processor, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, built.Status);
        Assert.Equal(reference.AsSpan(0x80000).ToArray(), built.OutputBytes[0x80000..].ToArray());
        Assert.Equal(selection == AbCtrlRamBankSelection.B ? reference[0x25610] : bytes["replace-ctrlram-normal"][0],
            built.OutputBytes.Span[0x25610]);
        if (selection != AbCtrlRamBankSelection.A)
        {
            Assert.Equal(bytes["replace-ctrlram-normal"][0], built.OutputBytes.Span[0x65610]);
        }
        else
        {
            Assert.Equal(reference[0x65610], built.OutputBytes.Span[0x65610]);
        }
        Assert.Equal(0x100000, built.Report.SourceEnvelope?.ActualOutputLength);
        Assert.Equal(0x80000, built.Report.SourceEnvelope?.LayoutTemplateCapacity);
    }
    /// <summary>The shared runner executes selected banks and publishes once after an approved preview.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CandidateRouteRunsPreviewAndBuildWithRealCombiner(bool a, bool b)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        byte[] original = Reference();
        original[0x61000] ^= 0x34;
        original[0x5F200] = 0x29;
        original[0x5F201] = 0xD6;
        CompiledComposition compiled = Compile(original, a, b, editVersions: true);
        ResolvedCapability capability = Resolve(compiled);
        var writer = new Writer();
        using TempWorkspace workspace = TempWorkspace.Create("ab-runtime-wiring");
        var processor = new CountingProcessor(RealProcessor(workspace));
        CompositionRunService service = Service(original, processor, writer);
        CompositionRunRequest request = Request(capability);
        CompositionRunResult preview = await service.PreviewAsync(request, TestContext.Current.CancellationToken);
        AssertSucceeded(preview);
        Assert.Equal(0, writer.Count);
        int banks = (a ? 1 : 0) + (b ? 1 : 0);
        Assert.Equal(banks, processor.Calls);
        CompositionRunResult build = await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), TestContext.Current.CancellationToken);
        AssertSucceeded(build);
        Assert.Equal(1, writer.Count);
        Assert.Equal(banks * 2, processor.Calls);
        Assert.Equal(preview.OutputBytes.ToArray(), build.OutputBytes.ToArray());
        Assert.Equal(build.OutputBytes.ToArray(), writer.Bytes);
        Assert.Equal(a ? 0xA5 : original[0x1FC00], build.OutputBytes.Span[0x1FC00]);
        Assert.Equal(b ? 0xA5 : original[0x5FC00], build.OutputBytes.Span[0x5FC00]);
        if (!a)
        {
            Assert.Equal(original[..0x40000], build.OutputBytes[..0x40000].ToArray());
        }

        if (!b)
        {
            Assert.Equal(original[0x40000..], build.OutputBytes[0x40000..].ToArray());
        }

        // The existing core parity test compares entire outputs to independent local tool controls.
        // This boundary additionally proves that the real runner uses the captured plan and one publisher.
        Assert.Equal(original.AsSpan(0x7164, 12).ToArray(), build.OutputBytes.Slice(0x7164, 12).ToArray());
        Assert.Equal(original.AsSpan(0x47164, 12).ToArray(), build.OutputBytes.Slice(0x47164, 12).ToArray());
        foreach (int start in new[] { a ? 0 : -1, b ? 0x40000 : -1 }.Where(static start => start >= 0))
        {
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(build.OutputBytes.Span.Slice(start, 0x40000), out FirmwareConfigMetadata metadata));
            Assert.Equal(start == 0 ? 0x21 : 0x29, metadata.FirmwareVersion);
            Assert.Equal(start == 0 ? 0x32 : 0x41, metadata.FirmwareSubVersion);
        }
        Assert.Equal(CapabilityPublicationStatus.Candidate, capability.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, capability.Evidence.Value);
    }

    /// <summary>A Reference changed after compilation fails before the native tool and publication.</summary>
    [Fact]
    public async Task ChangedReferenceIsRejectedBeforeTool()
    {
        byte[] original = Reference();
        ResolvedCapability capability = Resolve(Compile(original, true, true));
        original[0x61000] ^= 1;
        var processor = new CountingProcessor(null);
        var writer = new Writer();
        CompositionRunResult result = await Service(original, processor, writer).PreviewAsync(Request(capability), TestContext.Current.CancellationToken);
        Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Report.Issues, static issue => issue.Code == "input.bank-reference.identity-mismatch");
        Assert.Equal(0, processor.Calls);
        Assert.Equal(0, writer.Count);
    }

    /// <summary>A failed local tool cannot publish a partly updated AB image.</summary>
    [Fact]
    public async Task ToolFailureNeverPublishes()
    {
        byte[] original = Reference();
        var processor = new CountingProcessor(null);
        var writer = new Writer();
        CompositionRunResult result = await Service(original, processor, writer).PreviewAsync(Request(Resolve(Compile(original, true, true))), TestContext.Current.CancellationToken);
        Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Report.Issues, static issue => issue.Code == "test.tool.failure");
        Assert.Equal(1, processor.Calls);
        Assert.Equal(0, writer.Count);
    }

    /// <summary>Even after A succeeds, a B tool or version postcondition failure discards the whole Build.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondBankFailurePreventsCommitAfterSuccessfulPreview(bool corruptBackup)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        byte[] original = Reference();
        using TempWorkspace workspace = TempWorkspace.Create("ab-runtime-failure");
        var processor = new CountingProcessor(RealProcessor(workspace)) { FailOnCall = 4, CorruptBackup = corruptBackup };
        var writer = new Writer();
        CompositionRunService service = Service(original, processor, writer);
        CompositionRunRequest request = Request(Resolve(Compile(original, true, true, editVersions: true)));
        CompositionRunResult preview = await service.PreviewAsync(request, TestContext.Current.CancellationToken);
        AssertSucceeded(preview);
        CompositionRunResult build = await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), TestContext.Current.CancellationToken);
        Assert.NotEqual(CompositionExecutionStatus.Succeeded, build.Status);
        Assert.Contains(build.Report.Issues, issue => issue.Code == (corruptBackup ? "test.version-invalid" : "test.tool.failure"));
        Assert.Equal(4, processor.Calls);
        Assert.Equal(0, writer.Count);
        Assert.Empty(writer.Bytes);
    }

    /// <summary>Selections change run identity but never redefine the admitted composite capability.</summary>
    [Fact]
    public void SelectionAndReferenceAreBoundSeparatelyFromDefinition()
    {
        byte[] reference = Reference();
        CompiledComposition a = Compile(reference, true, false);
        CompiledComposition b = Compile(reference, false, true);
        CompiledComposition both = Compile(reference, true, true);
        reference[0x61000] ^= 1;
        CompiledComposition changed = Compile(reference, true, true);
        Assert.Equal(4, new[] { a, b, both, changed }.Select(static item => item.CompilationFingerprint).Distinct().Count());
        _ = Assert.Single(new[] { a, b, both, changed }.Select(static item => Resolve(item).CapabilityFingerprint).Distinct());
        RuntimeReferenceCompilationProof proof = Proof(a);
        _ = Assert.Throws<ArgumentException>(() => proof.ValidateAndGetSemanticBindings(b));
        _ = Assert.Throws<ArgumentException>(() => proof.BindCapabilityCompilation(a, b));
        _ = Assert.Throws<ArgumentException>(() => RuntimeReferenceCompilationProof.CreateBankReplace(both,
            new Dictionary<string, LegacyCombinerPostbuildCommandPlan> { ["a-bank"] = ToolPlan() }));
        _ = Assert.Throws<ArgumentException>(() => RequestWithoutCapability(a));
    }

    /// <summary>Proof must retain all parent obligations and the exact checked outer plan.</summary>
    [Fact]
    public void MissingValidationOrSubstitutedPlanCannotReceiveProof()
    {
        CompiledComposition original = Compile(Reference(), true, true, editVersions: true);
        CompiledComposition stripped = WithValidations(original, []);
        _ = Assert.Throws<ArgumentException>(() => Proof(stripped));
        var changedPlan = new CompositionPlan(original.Plan.Initializations, original.Plan.OutputSpaceId,
            original.Plan.AddressSpaces, original.Plan.OrderedOperations.Where(static operation => operation.OperationId != "b-bank/publish"));
        _ = Assert.Throws<ArgumentException>(() => Proof(CompiledComposition.CreateV2(changedPlan, original.V2Details)));
        _ = Assert.Throws<ArgumentException>(() => Resolve(original, CapabilityPublicationStatus.Supported));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest("injected-bank", Resolve(original).CompiledComposition,
            [.. Bindings(), new InputArtifactBinding("ab-replace/b-bank/reference", "forged", "forged", "forged.bin", CompiledInputArtifactClass.ReferenceImage)],
            "ab.bin", icNumberSelection: Selection(), outputFileNameIsOverride: true, resolvedCapability: Resolve(original)));
    }

    /// <summary>Existing input/final validators receive their bank's Reference and restored local view.</summary>
    [Theory]
    [InlineData("uniform")]
    [InlineData("authority")]
    [InlineData("expected-address")]
    public async Task ParentValidationIsForwardedAtOriginalStage(string rule)
    {
        byte[] reference = Reference();
        reference[0x3000] = 1;
        reference[0x3001] = 2;
        reference[0x43000] = reference[0x43001] = 0;
        // Keep A/B references distinct while exercising the local immutable Reference validator.
        reference[0x6F100] ^= 0x51;
        CompiledValidationRequirement validation = rule switch
        {
            "uniform" => CompiledValidationRequirements.RejectUniformInputRanges("uniform", CompiledValidationSeverity.Error,
                "test.uniform", "reference-base", [new ByteRange(0x3000, 2)]),
            "authority" => CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority("authority", "test.authority",
                "test.inactive", "reference-base", new ByteRange(0x2E000, 0x1000), 0x1000),
            _ => CompiledValidationRequirements.FirmwareConfigBackupExpectedAddress("expected", "test.expected", 0x2D000),
        };
        // Synthetic validation obligations test forwarding; they do not add a production/profile admission.
        CompiledComposition compiled = Compile(reference, false, true, extraTestValidation: validation);
        var processor = new CountingProcessor(new PassthroughProcessor());
        var writer = new Writer();
        CompositionRunResult result = await Service(reference, processor, writer).PreviewAsync(Request(Resolve(compiled)), TestContext.Current.CancellationToken);
        if (rule == "uniform")
        {
            Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
            Assert.Contains(result.Report.Issues, static issue => issue.Code == "test.uniform");
            Assert.Equal(0, processor.Calls);
        }
        else
        {
            AssertSucceeded(result);
            Assert.Equal(1, processor.Calls);
            if (rule == "expected-address")
            {
                Assert.Contains(result.Report.Issues, static issue => issue.Code == "test.expected" && issue.Severity == "warning");
            }
        }

        Assert.Equal(0, writer.Count);
    }

    internal static CompiledComposition Compile(byte[] reference, bool a, bool b, bool editVersions = false,
        CompiledValidationRequirement? extraTestValidation = null)
    {
        TrustedProfileBundleCatalog ab = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51919-nt51929-nt51932-ab-merge", "ece8e9ee7a81b3f00ce04bd7c1aa053acde26835d75c3042bf1a86302d8de793");
        CompiledComposition layout = ab.Compile("nt51929-ab-merge", "0.4.0", "NT51929", ExperienceIds.AbMerge,
            0x80000, null, [], selectedInputSlotIds: ["dp-ab-input"]).CompiledComposition!;
        TrustedProfileBundleCatalog local = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51929-ctrlram-replace-candidate", "309f29e33a8fb672e92ed441d6633fab829bee3bd4c94a93fd842a7f3bb157d0");
        BankReferenceReplaceDefinition definition = ab.CreateBankReplaceDefinition(local, "NT51929",
            "nt51929-ab-merge", "0.4.0", "nt51929-ab-merge-512k",
            "nt51929-ctrlram-replace-fw200-single", "0.3.0", "nt51929-ctrlram-fw200-single-full-flash");
        LegacyCombinerPostbuildCommandPlan plan = ToolPlan();
        ByteRange[] staged = [.. LegacyCombinerPostbuildPlanCompiler.GetStagedFileBlocks(plan).Select(static block => block.FirmwareRange)];
        var request = new V2RuntimeReferenceReplaceCompileRequest(
            [new("reference-base", "reference-base", 0x40000), new("source", "ctrlram-source", 1)],
            [new ExplicitMapping("replace-nf", 100, ExplicitMappingOperationKind.ReplaceRange, "source", new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage, new ByteRange(0x1FC00, 1), OverlapPolicy.Reject, alignment: 1, reason: "Replace NF byte.")],
            postbuildWriteRangeSections: LegacyCombinerPostbuildPlanCompiler.GetAllowedWriteRangeSectionsForStagedSources(plan, 0x40000, staged, staged),
            processorProtocolPlan: plan.ProtocolPlan);
        var banks = new List<V2RuntimeReferenceBankReplaceRequest>();
        if (a)
        {
            banks.Add(new("a-bank", WithVersion(0x21, 0x32)));
        }

        if (b)
        {
            banks.Add(new("b-bank", WithVersion(0x29, 0x41)));
        }

        V2RuntimeReferenceBankReplacePlan prepared = V2CompositionPlanCompiler.PrepareAbRuntimeReferenceReplace(
            layout, new FirmwareArtifactPayload("reference-base", reference), definition, 1, local, banks);
        if (extraTestValidation is not null)
        {
            prepared = new V2RuntimeReferenceBankReplacePlan(prepared.AbLayout, prepared.Reference,
                prepared.Definition, prepared.Plan,
                prepared.Banks.Select(bank => bank with { LocalComposition = WithValidations(bank.LocalComposition, [extraTestValidation]) }));
        }

        return V2CompositionPlanCompiler.CompileAbRuntimeReferenceReplace(prepared);

        V2RuntimeReferenceReplaceCompileRequest WithVersion(byte version, byte subVersion)
        {
            return new V2RuntimeReferenceReplaceCompileRequest(request.Bindings, request.Mappings,
                editVersions ? new V2RuntimeReferenceReplaceFirmwareVersionEdit(new ByteRange(0x1F200, 2), new ByteRange(0x1F200 + FirmwareConfigLayout.FirmwareSubVersionOffset, 1),
                    version, subVersion, "test.version-invalid", "test.version-mismatch") : null,
                postbuildWriteRangeSections: request.PostbuildWriteRangeSections, processorProtocolPlan: request.ProcessorProtocolPlan);
        }
    }

    private static CompiledComposition WithValidations(CompiledComposition original, IEnumerable<CompiledValidationRequirement> validations)
    {
        V2CompiledCompositionDetails d = original.V2Details;
        V2CompilationProvenance p = d.Provenance;
        var provenance = new V2CompilationProvenance(p.Bundle, p.ProfileEntry, p.Context, p.Promotion, p.ProfileEvidenceRefs,
            validations, p.RequiredCapabilities);
        return CompiledComposition.CreateV2(original.Plan, new V2CompiledCompositionDetails(d.ProfileId, d.ProfileVersion,
            d.ExperienceId, d.CompositionKind, provenance, d.InputContract, d.RegionAccessContract, d.OutputNamingRequirement, d.IcNumberInputMode));
    }

    internal static RuntimeReferenceCompilationProof Proof(CompiledComposition composition)
    {
        var context = (RuntimeReferenceBankReplaceV2CompilationContext)composition.V2Details.Provenance.Context;
        return RuntimeReferenceCompilationProof.CreateBankReplace(composition, context.Banks.ToDictionary(static bank => bank.BankId, static _ => ToolPlan()));
    }

    private static ResolvedCapability Resolve(CompiledComposition composition, CapabilityPublicationStatus publication = CapabilityPublicationStatus.Candidate)
    {
        var context = (RuntimeReferenceBankReplaceV2CompilationContext)composition.V2Details.Provenance.Context;
        RuntimeReferenceCompilationProof proof = Proof(composition);
        var identity = new CapabilityRouteIdentity("NT51929", ExperienceIds.CtrlRamReplace, "single", context.ResolvedMap.ImageMap.MapId);
        var contract = new CanonicalCapabilityCompilationContract(context.Definition.DefinitionId, context.Definition.Version,
            context.Definition.ContentHash, [identity.MapVariant], BankReferenceReplaceDefinition.CompilerSemanticId, proof.ValidateAndGetSemanticBindings(composition));
        string fingerprint = CapabilityDefinitionFingerprint.Compute(identity, contract.ProfileId, contract.ProfileVersion,
            contract.TrustedDefinitionSha256, contract.AllowedMapVariantIds, contract.CompilerSemanticId, contract.SemanticBindingIds);
        var definition = new CanonicalDynamicCapabilityDefinition(identity, fingerprint, contract,
            Decision(CapabilityAuthoringAvailability.Available), Decision(publication), Decision(CapabilityEvidenceStatus.ContractOnly));
        var catalog = new CanonicalCapabilityCatalog(new Source(definition));
        CapabilityCatalogReloadResult load = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, string.Join("; ", load.Issues.Select(static issue => issue.Message)));
        return catalog.ResolveDynamicRoute(identity.RouteId).Route!.BindCompilation(composition, runtimeReferenceProof: proof);

        PinnedCapabilityDecision<T> Decision<T>(T value) where T : struct, Enum
        {
            return new PinnedCapabilityDecision<T>("test-" + typeof(T).Name, identity.RouteId, fingerprint, value, "runtime-wiring-test");
        }
    }

    private static CompositionRunRequest Request(ResolvedCapability capability)
    {
        return new CompositionRunRequest("ab-runtime", capability.CompiledComposition, Bindings(), "ab-replaced.bin",
            icNumberSelection: Selection(), outputFileNameIsOverride: true, resolvedCapability: capability);
    }

    private static CompositionRunRequest RequestWithoutCapability(CompiledComposition composition)
    {
        return new CompositionRunRequest("ab-runtime", composition, Bindings(), "ab-replaced.bin", icNumberSelection: Selection(), outputFileNameIsOverride: true);
    }

    private static InputArtifactBinding[] Bindings()
    {
        return [
            new("reference-base", "reference-base", "reference", "reference.bin", CompiledInputArtifactClass.ReferenceImage),
            new("source", "source", "source", "source.bin", CompiledInputArtifactClass.CtrlRamReplacement),
        ];
    }

    private static CompositionRunService Service(byte[] reference, IExternalProcessor processor, Writer writer)
    {
        return new CompositionRunService(new FakeArtifactReader(new Dictionary<string, byte[]> { ["reference"] = reference, ["source"] = [0xA5] }),
            new FakeClock(Enumerable.Repeat(new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero), 20)), writer, processor);
    }

    private static LegacyCombinerPostbuildCommandPlan ToolPlan()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile("NT51929", out LegacyCombinerPostbuildProfile? profile));
        return profile!.ResolvePlan(Selection());
    }

    private static IcNumberSelection Selection()
    {
        return new(IcNumberInputMode.SingleSelector, ["single"]);
    }
    private static byte[] Reference()
    {
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51929", "expected-output", "t05-d06"));
    }

    private static LegacyCombinerPostbuildProcessor RealProcessor(TempWorkspace workspace)
    {
        string root = Path.Combine(RepositoryPaths.FindRepositoryRoot(), "external-tools");
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "legacy-combiner", "1.13.0", "manifest.json")));
        JsonElement m = json.RootElement;
        string S(string name)
        {
            return m.GetProperty(name).GetString()!;
        }
        var manifest = new ExternalCombinerToolManifest(S("schemaVersion"), S("toolBindingId"), S("toolId"), S("toolVersion"),
            S("displayName"), S("platform"), S("executableName"), S("sha256"), S("adapterId"), S("inputMode"),
            [.. m.GetProperty("argumentTemplate").EnumerateArray().Select(static item => item.GetString()!)],
            S("workingDirectoryPolicy"), m.GetProperty("timeoutSeconds").GetInt32(),
            [.. m.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(static item => item.GetString()!)]);
        return new LegacyCombinerPostbuildProcessor(new ExternalCombinerToolRegistry([manifest]), root, workspace.PathFor("staging"), new SystemExternalProcessRunner());
    }

    private static void AssertSucceeded(CompositionRunResult result)
    {
        Assert.True(result.Status == CompositionExecutionStatus.Succeeded, string.Join("; ", result.Report.Issues.Select(static issue => issue.Code + ": " + issue.Message)));
    }

    private sealed class Source(CanonicalDynamicCapabilityDefinition definition) : ICanonicalCapabilityCatalogSource
    {
        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            return CapabilityCatalogLoadResult.Success(new CanonicalCapabilityCatalogCandidate("test", "1", new string('b', 64), [], [definition]));
        }
    }

    private sealed class CountingProcessor(IExternalProcessor? inner) : IExternalProcessor
    {
        public int Calls { get; private set; }
        public int? FailOnCall { get; init; }
        public bool CorruptBackup { get; init; }
        public async ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (inner is null || (Calls == FailOnCall && !CorruptBackup))
            {
                return ExternalProcessorResult.Failed([new CompositionIssue("test.tool.failure", "Deliberate tool failure.")]);
            }

            ExternalProcessorResult result = await inner.TransformAsync(request, cancellationToken);
            if (Calls == FailOnCall && CorruptBackup && result.Succeeded)
            {
                byte[] output = result.OutputBytes.ToArray();
                Assert.True(FirmwareConfigMetadataReader.TryReadBackup(output, out FirmwareConfigMetadata metadata));
                output[checked((int)metadata.StructureStart) + FirmwareConfigLayout.FirmwareVersionBarOffset] ^= 1;
                return ExternalProcessorResult.Success(output, result.ChangedRanges, result.ExecutedCommands);
            }

            return result;
        }
    }

    private sealed class PassthroughProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, [], []));
        }
    }

    private sealed class Writer : ICompositionOutputWriter
    {
        public int Count { get; private set; }
        public byte[] Bytes { get; private set; } = [];
        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(string fileName, ReadOnlyMemory<byte> outputBytes, CancellationToken cancellationToken)
        {
            Count++;
            Bytes = outputBytes.ToArray();
            return ValueTask.FromResult(CompositionOutputCommitReceipt.CreateLoose("committed:" + fileName, fileName, outputBytes.Span));
        }
    }
}
