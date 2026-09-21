using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Real adapter and official catalog closure for captured AB authoring.</summary>
public sealed class AbCtrlRamAuthoringTests
{
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
            "nt51919-nt51929-nt51932-ab-merge", "5acf2fd4d0757d7b757bf7491ff2f268d07cf70a76588f528d36b616e1e5eed0");
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
