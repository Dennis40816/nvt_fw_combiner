using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Automatic reference inspection uses captured bytes and current declarations without dummy inputs.</summary>
public sealed class AbCtrlRamAutoDetectionTests
{
    /// <summary>Base-only inspection identifies both banks before selecting a replacement source.</summary>
    [Fact]
    public void BaseOnlyDiscoversBothBanksWithCompleteFacts()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        FirmwareInspectionStatusBatch batch = Inspect(bytes, sources: false);
        CtrlRamBaseInspection facts = Assert.IsType<CtrlRamBaseInspection>(batch.CtrlRamBaseInspection);
        Assert.Equal(CtrlRamBaseKind.AbFlash, facts.Kind);
        Assert.Equal(AbCtrlRamBankSelection.Both, Assert.IsType<AbCtrlRamDraftState>(facts.EffectiveDraft).Banks);
        Assert.Empty(facts.Issues);
        Assert.Empty(batch.Issues);
        Assert.Null(batch.Catalog);
        Assert.Equal(CtrlRamBaseDiscoveryReadiness.Inspected, batch.CtrlRamBaseDiscovery!.Readiness);
        Assert.Collection(facts.Banks, bank => AssertBank(bank, "a-bank"), bank => AssertBank(bank, "b-bank"));
        Assert.False(facts.Banks[0].Range.Overlaps(facts.Banks[1].Range));
        Assert.Equal(bytes[CompositionSlotIds.ReplaceBase].Length, facts.Banks[1].Range.EndExclusive);
        foreach (CtrlRamBaseBankInspection bank in facts.Banks)
        {
            // The fixture's owner-declared u8EventBufferFormatVersion is 0x0C bytes into each Backup.
            int byteAddress = checked((int)(bank.Range.Start + bank.FirmwareConfig!.FirmwareConfigBackupStart + 0x0C));
            Assert.Equal(bytes[CompositionSlotIds.ReplaceBase][byteAddress], bank.EventBufferFormatVersion);
        }
    }

    /// <summary>Automatic selection and explicit per-bank choices survive atomic inspection adoption.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    public void CompleteBatchAdoptsEffectiveDraft(AbCtrlRamBankSelection? selection)
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        AbCtrlRamDraftState? draft = selection is { } value ? new(value, new(0x21, 0x32), new(0x29, 0x41)) : null;
        FirmwareInspectionStatusBatch batch = Inspect(bytes, sources: true, draft);
        CtrlRamBaseInspection facts = Assert.IsType<CtrlRamBaseInspection>(batch.CtrlRamBaseInspection);
        Assert.Empty(batch.Issues);
        Assert.Equal(2, facts.Banks.Count);
        Assert.All(facts.Banks, static bank =>
        {
            _ = Assert.NotNull(bank.EventBufferFormatVersion);
        });
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        AuthoringSessionTransitionResult adopted = BootstrapTestHost.Canonical.CtrlRamAuthoring.AdoptInspectedBatch(
            state, batch.Catalog!, [.. batch.Statuses.Values], facts);
        Assert.True(adopted.Succeeded, adopted.Issue?.Message);
        ActiveSessionSnapshot accepted = adopted.Snapshot!;
        Assert.Equal(facts.EffectiveDraft, accepted.DraftState);
        Assert.Equal(facts.EffectiveDraft, accepted.ExactCapability!.CtrlRamExecutionPlan!.Draft);
        Assert.Equal(draft ?? new AbCtrlRamDraftState(), accepted.DraftState);
        Assert.Equal(selection is null ? 2 : 1,
            accepted.InputSlotStatuses.Single(static status => status.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase)
                .Observation.ReferenceBanks.Count);
    }

    /// <summary>Detected kind is independent of the selected Number's execution eligibility.</summary>
    [Fact]
    public void UnsupportedNumberRetainsAbIdentityAndRefusesCompilation()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        FirmwareInspectionStatusBatch batch = Inspect(bytes, sources: true, number: "2");
        Assert.Equal(CtrlRamBaseKind.AbFlash, batch.CtrlRamBaseInspection!.Kind);
        Assert.Equal(2, batch.CtrlRamBaseInspection.Banks.Count);
        Assert.Null(batch.Catalog);
        Assert.Contains(batch.Issues, static issue => issue.Code == CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported);
    }

    /// <summary>A real Standard reference clears a previous AB draft instead of compiling against the wrong mode.</summary>
    [Fact]
    public void StandardReferenceClearsAbDraft()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("standard-merge", "NT51929", "expected-output"));
        FirmwareInspectionStatusBatch batch = Inspect(bytes, sources: true, new AbCtrlRamDraftState(AbCtrlRamBankSelection.B));
        Assert.Equal(CtrlRamBaseKind.StandardFlash, batch.CtrlRamBaseInspection!.Kind);
        Assert.Null(batch.CtrlRamBaseInspection.EffectiveDraft);
        Assert.Empty(batch.CtrlRamBaseInspection.Banks);
        Assert.Empty(batch.Issues);
        Assert.NotNull(batch.Catalog);
        Assert.IsNotType<RuntimeReferenceBankReplaceV2CompilationContext>(batch.Catalog.Routes[0].ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
    }

    /// <summary>Each bank's observed byte comes from its own captured Backup, including zero and unknown values.</summary>
    [Theory]
    [InlineData((byte)0xA3, (byte)0x00)]
    [InlineData((byte)0x7F, (byte)0xA3)]
    public void BankEventBufferBytesStayIndependent(byte aValue, byte bValue)
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        byte[] reference = bytes[CompositionSlotIds.ReplaceBase];
        CtrlRamBaseInspection baseline = Inspect(bytes, sources: false).CtrlRamBaseInspection!;
        byte[] values = [aValue, bValue];
        for (int index = 0; index < baseline.Banks.Count; index++)
        {
            CtrlRamBaseBankInspection bank = baseline.Banks[index];
            int fieldAddress = checked((int)(bank.Range.Start +
                bank.FirmwareConfig!.FirmwareConfigBackupStart + 0x0C));
            reference[fieldAddress] = values[index];
        }

        CtrlRamBaseInspection observed = Inspect(bytes, sources: false).CtrlRamBaseInspection!;
        Assert.Equal(CtrlRamBaseKind.AbFlash, observed.Kind);
        Assert.Equal(aValue, observed.Banks[0].EventBufferFormatVersion);
        Assert.Equal(bValue, observed.Banks[1].EventBufferFormatVersion);
    }

    /// <summary>A legacy-readable Backup outside the declared TP region cannot supply a canonical display fact.</summary>
    [Fact]
    public void BankEventBufferOutsideCanonicalLocatorIsMissingOnlyForThatBank()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        byte[] reference = bytes[CompositionSlotIds.ReplaceBase];
        CtrlRamBaseInspection baseline = Inspect(bytes, sources: false).CtrlRamBaseInspection!;
        CtrlRamBaseBankInspection b = baseline.Banks[1];
        int original = checked((int)(b.Range.Start + b.FirmwareConfig!.FirmwareConfigBackupStart));
        int relocated = checked((int)(b.Range.Start + 0x1000));
        Array.Copy(reference, original, reference, relocated, 0x1000);
        reference[original + 0xFFC] ^= 1;

        CtrlRamBaseInspection observed = Inspect(bytes, sources: false).CtrlRamBaseInspection!;
        Assert.Equal(CtrlRamBaseKind.AbFlash, observed.Kind);
        _ = Assert.NotNull(observed.Banks[0].EventBufferFormatVersion);
        Assert.Empty(observed.Banks[1].Issues);
        Assert.Equal(0x1000, observed.Banks[1].FirmwareConfig!.FirmwareConfigBackupStart);
        Assert.Null(observed.Banks[1].EventBufferFormatVersion);
    }

    /// <summary>The existing Standard TP shape is recognized independently of CtrlRAM route capacity support.</summary>
    [Fact]
    public void StandardTpClearsAbDraftWithoutInventingBanks()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("standard-merge", "NT51929", "tp-input"));
        FirmwareInspectionStatusBatch batch = Inspect(bytes, false, new AbCtrlRamDraftState());
        Assert.Equal(CtrlRamBaseKind.StandardTp, batch.CtrlRamBaseInspection!.Kind);
        Assert.Null(batch.CtrlRamBaseInspection.EffectiveDraft);
        Assert.Empty(batch.CtrlRamBaseInspection.Banks);
    }

    /// <summary>Damaged metadata, ambiguous markers and mismatched bank addresses never fall back to Standard.</summary>
    [Theory]
    [InlineData("version-bar")]
    [InlineData("duplicate-marker")]
    [InlineData("zero-count")]
    [InlineData("different-count")]
    [InlineData("missing-marker")]
    [InlineData("relocation")]
    public void DamagedRecognizedAbIsTerminal(string damage)
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        CtrlRamBaseBankInspection bank = Inspect(bytes, sources: false).CtrlRamBaseInspection!.Banks[1];
        byte[] reference = bytes[CompositionSlotIds.ReplaceBase];
        int config = checked((int)(bank.Range.Start + bank.FirmwareConfig!.FirmwareConfigBackupStart));
        switch (damage)
        {
            case "version-bar": reference[config + 1] ^= 1; break;
            case "duplicate-marker": new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(reference, checked((int)bank.Range.Start + 0x1000)); break;
            case "zero-count": reference[config + 0x17] = 0; break;
            case "different-count": reference[config + 0x17] = 2; break;
            case "missing-marker": reference[config + 0xFFC] ^= 1; break;
            case "relocation": reference[0x47164] ^= 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(damage));
        }
        FirmwareInspectionStatusBatch batch = Inspect(bytes, sources: true);
        Assert.Equal(CtrlRamBaseKind.AbFlash, batch.CtrlRamBaseInspection!.Kind);
        Assert.NotEmpty(batch.Issues);
        string code = damage switch
        {
            "version-bar" => "input.bank-reference.version-bar",
            "duplicate-marker" => "input.bank-reference.metadata-ambiguous",
            "zero-count" => "input.bank-reference.count-zero",
            "different-count" => "input.bank-reference.count",
            "missing-marker" => "input.bank-reference.metadata-unreadable",
            "relocation" => "input.bank-reference.invalid",
            _ => throw new ArgumentOutOfRangeException(nameof(damage)),
        };
        Assert.Contains(batch.Issues, issue => issue.Code == code);
        Assert.Null(batch.Catalog);
        Assert.Empty(batch.Statuses);
    }

    /// <summary>A matching capacity with no firmware structure is not AB evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapacityAloneCannotIdentifyAb(bool previousAb)
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = new byte[bytes[CompositionSlotIds.ReplaceBase].Length];
        FirmwareInspectionStatusBatch batch = Inspect(bytes, sources: false, previousAb ? new AbCtrlRamDraftState() : null);
        Assert.Equal(CtrlRamBaseKind.Unknown, batch.CtrlRamBaseInspection!.Kind);
        Assert.Null(batch.Catalog);
        Assert.NotEmpty(batch.Issues);
    }

    /// <summary>The facade reads each selected path once and exposes discovery slots from bank-local facts.</summary>
    [Fact]
    public void FacadeRetainsBothBankFactsWithOneReadPerPath()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<FirmwareInspectionSnapshotResult> results = BuiltInFirmwareInspection.InspectFirmwareBatch(
            BootstrapTestHost.Canonical, "NT51929",
            [new("base", CompositionSlotIds.ReplaceBase + ".bin", CtrlRamRequest: new("single"),
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
            path =>
            {
                reads[path] = reads.GetValueOrDefault(path) + 1;
                return bytes[Path.GetFileNameWithoutExtension(path)];
            });
        FirmwareInspectionSnapshot inspection = Assert.Single(results).Inspection;
        Assert.Equal(1, Assert.Single(reads).Value);
        Assert.Equal(CtrlRamBaseKind.AbFlash, inspection.CtrlRamBaseInspection!.Kind);
        Assert.Null(inspection.FirmwareConfig);
        Assert.Null(inspection.DpVersion);
        Assert.Contains(inspection.CtrlRamDisplay!.InputSlots, static slot => slot.SlotId == "replace-ctrlram-nf");
        byte version = inspection.CtrlRamBaseInspection.Banks[0].FirmwareConfig!.FirmwareVersion;
        Array.Clear(bytes[CompositionSlotIds.ReplaceBase]);
        Assert.Equal(version, inspection.CtrlRamBaseInspection.Banks[0].FirmwareConfig!.FirmwareVersion);
    }

    /// <summary>A completed old publication cannot restore a current accepted session after reload.</summary>
    [Fact]
    public void ReloadRefusesOldDetectionAndAdoption()
    {
        var host = new IsolatedBootstrapTestHost();
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        var owner = (CtrlRamAuthoringExperience)host.Canonical.CtrlRamAuthoring;
        FirmwareInspectionSnapshotInput[] inputs =
        [
            new("base", CompositionSlotIds.ReplaceBase + ".bin", CtrlRamRequest: new("single"),
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
            new("nf", "replace-ctrlram-nf.bin", CtrlRamReplaceAddressSpaceId: "replace-ctrlram-nf"),
        ];
        FirmwareInspectionStatusBatch batch = owner.InspectInputSlots("NT51929", inputs, path => bytes[Path.GetFileNameWithoutExtension(path)]);
        Assert.Empty(batch.Issues);
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        AuthoringSessionTransitionResult refused = owner.AdoptInspectedBatch(state, batch.Catalog!,
            [.. batch.Statuses.Values], batch.CtrlRamBaseInspection);
        Assert.False(refused.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, refused.Issue!.Code);
        Assert.Null(state.CurrentSnapshot);
        FirmwareInspectionStatusBatch stale = owner.InspectInputSlots("NT51929",
            [.. inputs.Select(input => input with { ExactCapability = batch.Catalog!.Routes[0].ExactCapability })], path => bytes[Path.GetFileNameWithoutExtension(path)]);
        Assert.Null(stale.Catalog);
        Assert.Contains(stale.Issues, static issue => issue.Code == AuthoringSessionIssueCodes.StaleInspection);
    }

    /// <summary>Same-publication observations from another capture or selection cannot overwrite an accepted session.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdoptionRejectsMixedReferenceOrDraftWithoutChangingSession(bool mixDraft)
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        var draft = new AbCtrlRamDraftState(AbCtrlRamBankSelection.A);
        FirmwareInspectionStatusBatch first = Inspect(bytes, true, draft);
        var state = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        ICtrlRamAuthoring owner = BootstrapTestHost.Canonical.CtrlRamAuthoring;
        AuthoringSessionTransitionResult initial = owner.AdoptInspectedBatch(state, first.Catalog!,
            [.. first.Statuses.Values], first.CtrlRamBaseInspection);
        Assert.True(initial.Succeeded, initial.Issue?.Message);
        ActiveSessionSnapshot accepted = initial.Snapshot!;
        FirmwareInspectionStatusBatch current = Inspect(bytes, true, draft, revision: accepted.AuthoringRevision.Value);
        if (!mixDraft)
        {
            bytes[CompositionSlotIds.ReplaceBase][0x100] ^= 1;
        }
        FirmwareInspectionStatusBatch other = Inspect(bytes, true,
            mixDraft ? new AbCtrlRamDraftState(AbCtrlRamBankSelection.B) : draft, revision: accepted.AuthoringRevision.Value);
        Assert.Empty(other.Issues);
        AuthoringSessionTransitionResult refused = owner.AdoptInspectedBatch(state, current.Catalog!,
            [.. current.Statuses.Values], other.CtrlRamBaseInspection);
        Assert.False(refused.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.InvalidPublication, refused.Issue!.Code);
        Assert.Same(accepted, state.CurrentSnapshot);
    }

    /// <summary>Even when both DP contents lose plausibility, verified AB structure must not become Standard TP.</summary>
    [Fact]
    public void BothBanksWithUniformDpRetainAbIdentityAndBlock()
    {
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        CtrlRamBaseInspection valid = Inspect(bytes, false).CtrlRamBaseInspection!;
        Assert.Empty(valid.Issues);
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge("NT51929", null,
            out CompiledComposition? standard, out _));
        ByteRange dpRange = standard!.V2Details.Provenance.ValidationRequirements.OfType<CompiledUniformInputRangeValidation>()
            .Single(static validation => validation.AddressSpaceId == CompositionAddressSpaceIds.DpInput).Ranges[0];
        foreach (CtrlRamBaseBankInspection bank in valid.Banks)
        {
            bytes[CompositionSlotIds.ReplaceBase].AsSpan(checked((int)(bank.Range.Start + dpRange.Start)), checked((int)dpRange.Length)).Clear();
            Assert.Equal(CompiledFirmwareArtifactKind.TpFirmware, CompiledFirmwareArtifactClassifier.Classify(standard,
                bytes[CompositionSlotIds.ReplaceBase].AsSpan(checked((int)bank.Range.Start), checked((int)bank.Range.Length))).Kind);
        }
        FirmwareInspectionStatusBatch batch = Inspect(bytes, true);
        Assert.Equal(CtrlRamBaseKind.AbFlash, batch.CtrlRamBaseInspection!.Kind);
        Assert.Contains(batch.Issues, static issue => issue.Code == "input.bank-reference.content");
        Assert.Null(batch.Catalog);
        Assert.Empty(batch.Statuses);
    }

    private static FirmwareInspectionStatusBatch Inspect(Dictionary<string, byte[]> bytes, bool sources,
        CtrlRamAuthoringDraftState? draft = null, string number = "single", long revision = 1)
    {
        var inputs = new List<FirmwareInspectionSnapshotInput>
        {
            new("base", CompositionSlotIds.ReplaceBase + ".bin", CtrlRamRequest: new(number, draft),
                AuthoringRevision: revision, CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
        };
        if (sources)
        {
            inputs.Add(new("nf", "replace-ctrlram-nf.bin", AuthoringRevision: revision, CtrlRamReplaceAddressSpaceId: "replace-ctrlram-nf"));
        }
        return ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
            "NT51929", inputs, path => bytes[Path.GetFileNameWithoutExtension(path)]);
    }

    private static void AssertBank(CtrlRamBaseBankInspection bank, string id)
    {
        Assert.Equal(id, bank.BankId);
        Assert.Empty(bank.Issues);
        Assert.True(bank.FirmwareConfig!.IsFirmwareVersionBarValid);
        Assert.Equal(1, bank.FirmwareConfig.ChipNumber);
        Assert.NotEmpty(bank.FirmwareConfig.CommonFwVersion);
        Assert.True(bank.TpVersion!.IsKnown);
        Assert.True(bank.DpVersion!.IsKnown);
        _ = Assert.NotNull(bank.EventBufferFormatVersion);
    }
}
