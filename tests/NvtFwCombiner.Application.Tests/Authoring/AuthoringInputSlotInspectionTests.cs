using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Tests the headless per-slot readiness and inspection publication contract.</summary>
public sealed class AuthoringInputSlotInspectionTests
{
    private const string CapabilityFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SourceSpace = "source-input";
    private const string SourceSlot = "source-input-slot";

    /// <summary>Prerequisite readiness remains independent from artifact inspection health.</summary>
    [Fact]
    public void PendingPrerequisiteProjectsDisabledSelectionWithoutInspectionHealth()
    {
        ResolvedCapabilityRoute route = CreateRoute(ExperienceIds.DpReplace);
        var readiness = new InputSelectionMemberReadiness(
            SourceSlot,
            IsSelected: false,
            ResolvedChildReadiness.PendingInput,
            CanSelect: false,
            "Load reference-base first.",
            new InputSelectionNextAction(
                InputSelectionNextActionKind.LoadArtifactFirst,
                "reference-base"));

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.ProjectReadiness(
            route,
            new AuthoringRevision(4),
            readiness,
            new CompiledInputSpaceBinding(
                SourceSpace,
                SourceSlot,
                CompiledInputInstancePolicy.Singleton));

        Assert.Equal(ResolvedChildReadiness.PendingInput, result.Readiness);
        Assert.False(result.CanSelect);
        Assert.Null(result.InspectionLifecycle);
        Assert.Null(result.FileStamp);
        Assert.Equal("reference-base", result.ReadinessNextAction!.SubjectId);
        Assert.Equal(route.CapabilityFingerprint, result.CapabilityFingerprint);
        Assert.Null(result.CompilationFingerprint);
    }

    /// <summary>Compiled readiness carries the exact composition identity without inventing health.</summary>
    [Fact]
    public void CompiledReadinessProjectsCompilationIdentityWithoutInspectionHealth()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.ProjectReadiness(
            capability,
            new AuthoringRevision(5),
            ReadySelection(),
            SourceSpace);

        Assert.Equal(
            capability.CompiledComposition.CompilationFingerprint,
            result.CompilationFingerprint);
        Assert.Null(result.InspectionLifecycle);
        Assert.Null(result.FileStamp);
        Assert.Null(result.Inspection);
    }

    /// <summary>Checking is an explicit transient projection without fabricated terminal health.</summary>
    [Fact]
    public void SelectedReadyArtifactBeginsInChecking()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.BeginInspection(
            capability,
            new AuthoringRevision(5),
            ReadySelection(),
            SourceSpace);

        Assert.Equal(AuthoringSlotLifecycle.Checking, result.InspectionLifecycle);
        Assert.Null(result.Inspection);
        Assert.Null(result.FileStamp);
        AuthoringInputSlotPublicationResult publication =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                result,
                InspectionLease(
                    capability,
                    new AuthoringRevision(5),
                    FileStamp.FromBytes([])),
                "checking");
        Assert.False(publication.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.InvalidPublication, publication.Issue!.Code);
    }

    /// <summary>Per-slot health cannot be published through another derived-result channel.</summary>
    [Fact]
    public void TerminalInspectionRejectsNonInspectionPublicationLease()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        byte[] source = [1, 2, 3, 4];
        AuthoringInputSlotStatus inspected = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(6),
            ReadySelection(),
            SourceSpace,
            source);

        AuthoringInputSlotPublicationResult publication =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    capability,
                    new AuthoringRevision(6),
                    FileStamp.FromBytes(source),
                    kind: AuthoringDerivedResultKind.Preview),
                "wrong-channel");

        Assert.False(publication.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.InvalidPublication, publication.Issue!.Code);
    }

    /// <summary>Inspection admission requires one ready selection bound by the compiler.</summary>
    [Fact]
    public void InspectionRejectsUnreadySelectionAndMismatchedBinding()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        InputSelectionMemberReadiness unselected = ReadySelection() with
        {
            IsSelected = false,
        };
        InputSelectionMemberReadiness wrongSlot = ReadySelection() with
        {
            SlotId = "different-slot",
        };

        _ = Assert.Throws<ArgumentException>(() =>
            AuthoringInputSlotInspectionService.BeginInspection(
                capability,
                new AuthoringRevision(6),
                unselected,
                SourceSpace));
        _ = Assert.Throws<ArgumentException>(() =>
            AuthoringInputSlotInspectionService.ProjectReadiness(
                capability,
                new AuthoringRevision(6),
                wrongSlot,
                SourceSpace));
    }

    /// <summary>Every targeted workflow reaches terminal typed health without Presentation.</summary>
    [Theory]
    [InlineData(ExperienceIds.StandardMerge, 4, AuthoringSlotLifecycle.Verified)]
    [InlineData(ExperienceIds.AbMerge, 4, AuthoringSlotLifecycle.Verified)]
    [InlineData(ExperienceIds.DpReplace, 4, AuthoringSlotLifecycle.Verified)]
    [InlineData(ExperienceIds.CtrlRamReplace, 6, AuthoringSlotLifecycle.Warning)]
    public void FourWorkflowsPublishTerminalCompiledInspection(
        string workflowId,
        int sourceLength,
        AuthoringSlotLifecycle expectedLifecycle)
    {
        ResolvedCapability capability = CreateCapability(workflowId);
        byte[] source = [.. Enumerable.Range(0, sourceLength).Select(static value => (byte)value)];

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(6),
            ReadySelection(),
            SourceSpace,
            source);

        Assert.Equal(workflowId, result.WorkflowId);
        Assert.Equal(expectedLifecycle, result.InspectionLifecycle);
        Assert.Equal(FileStamp.FromBytes(source), result.FileStamp);
        Assert.Equal(SourceSlot, result.SlotId);
        Assert.Equal(capability.ResolutionToken, result.ResolutionToken);
        Assert.Equal(CapabilityFingerprint, result.CapabilityFingerprint);
        Assert.Equal(capability.CompiledComposition.CompilationFingerprint, result.CompilationFingerprint);
        Assert.NotNull(result.Inspection);
    }

    /// <summary>A short concrete CtrlRAM source is terminal Error rather than endless Checking.</summary>
    [Fact]
    public void ShortCtrlRamSourcePublishesBlockingError()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.CtrlRamReplace);

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(7),
            ReadySelection(),
            SourceSpace,
            new byte[3]);

        Assert.Equal(AuthoringSlotLifecycle.Error, result.InspectionLifecycle);
        Assert.True(result.Inspection!.BlocksBuild);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.Inspection.IssueCode);
    }

    /// <summary>Terminal publication rejects a different exact compilation in the same capability.</summary>
    [Fact]
    public void PublicationRejectsCompilationFingerprintDrift()
    {
        ResolvedCapability inspectedCapability = CreateCapability(
            ExperienceIds.StandardMerge,
            targetStart: 0);
        ResolvedCapability currentCapability = CreateCapability(
            ExperienceIds.StandardMerge,
            targetStart: 1);
        var revision = new AuthoringRevision(8);
        byte[] source = [1, 2, 3, 4];
        AuthoringInputSlotStatus inspected = AuthoringInputSlotInspectionService.Inspect(
            inspectedCapability,
            revision,
            ReadySelection(),
            SourceSpace,
            source);

        AuthoringInputSlotPublicationResult stale =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    currentCapability,
                    revision,
                    FileStamp.FromBytes(source)),
                "stale-compilation");
        AuthoringInputSlotPublicationResult accepted =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    inspectedCapability,
                    revision,
                    FileStamp.FromBytes(source)),
                "current-compilation");

        Assert.NotEqual(
            inspectedCapability.CompiledComposition.CompilationFingerprint,
            currentCapability.CompiledComposition.CompilationFingerprint);
        Assert.False(stale.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, stale.Issue!.Code);
        Assert.True(accepted.Succeeded);
        Assert.Equal(
            inspected.CompilationFingerprint,
            accepted.Publication!.CompilationFingerprint);
    }

    /// <summary>Content and revision changes also invalidate terminal publication.</summary>
    [Fact]
    public void PublicationRejectsRevisionAndFileIdentityDrift()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        byte[] source = [1, 2, 3, 4];
        var revision = new AuthoringRevision(9);
        AuthoringInputSlotStatus inspected = AuthoringInputSlotInspectionService.Inspect(
            capability,
            revision,
            ReadySelection(),
            SourceSpace,
            source);

        AuthoringInputSlotPublicationResult staleRevision =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    capability,
                    revision.Next(),
                    FileStamp.FromBytes(source)),
                "stale-revision");
        AuthoringInputSlotPublicationResult staleFile =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    capability,
                    revision,
                    FileStamp.FromBytes([4, 3, 2, 1])),
                "stale-file");

        Assert.False(staleRevision.Succeeded);
        Assert.False(staleFile.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, staleRevision.Issue!.Code);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, staleFile.Issue!.Code);
    }

    /// <summary>Slot definition and catalog publication are independent stale-result guards.</summary>
    [Fact]
    public void PublicationRejectsSlotAndResolutionTokenDrift()
    {
        ResolvedCapability inspectedCapability = CreateCapability(ExperienceIds.StandardMerge);
        ResolvedCapability reloadedCapability = CreateCapability(
            ExperienceIds.StandardMerge,
            publicationToken: "reloaded-publication");
        var revision = new AuthoringRevision(10);
        byte[] source = [1, 2, 3, 4];
        AuthoringInputSlotStatus inspected = AuthoringInputSlotInspectionService.Inspect(
            inspectedCapability,
            revision,
            ReadySelection(),
            SourceSpace,
            source);

        AuthoringInputSlotPublicationResult staleSlot =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    inspectedCapability,
                    revision,
                    FileStamp.FromBytes(source),
                    slotId: "different-slot"),
                "stale-slot");
        AuthoringInputSlotPublicationResult staleResolution =
            AuthoringInputSlotInspectionService.TryCreatePublication(
                inspected,
                InspectionLease(
                    reloadedCapability,
                    revision,
                    FileStamp.FromBytes(source)),
                "stale-resolution");

        Assert.False(staleSlot.Succeeded);
        Assert.False(staleResolution.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, staleSlot.Issue!.Code);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, staleResolution.Issue!.Code);
    }

    /// <summary>Picker admission is separate from inspecting an already-selected ready artifact.</summary>
    [Fact]
    public void SelectedReadyArtifactCanBeInspectedWhenPickerTransitionIsDisabled()
    {
        ResolvedCapability capability = CreateCapability(ExperienceIds.StandardMerge);
        InputSelectionMemberReadiness selected = ReadySelection() with
        {
            CanSelect = false,
        };

        AuthoringInputSlotStatus result = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(11),
            selected,
            SourceSpace,
            new byte[4]);

        Assert.Equal(AuthoringSlotLifecycle.Verified, result.InspectionLifecycle);
        Assert.False(result.CanSelect);
    }

    private static InputSelectionMemberReadiness ReadySelection()
    {
        return new InputSelectionMemberReadiness(
            SourceSlot,
            IsSelected: true,
            ResolvedChildReadiness.Ready,
            CanSelect: true,
            Reason: null,
            NextAction: null);
    }

    private static AuthoringPublicationLease InspectionLease(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        FileStamp fileStamp,
        string slotId = SourceSlot,
        AuthoringDerivedResultKind kind = AuthoringDerivedResultKind.Inspection)
    {
        return new AuthoringPublicationLease(
            new object(),
            kind,
            capability.ResolutionToken,
            authoringRevision,
            capability.Identity.RouteId,
            capability.CapabilityFingerprint,
            [new AuthoringSlotPublicationIdentity(slotId, "selected.bin", fileStamp)],
            capability.CompiledComposition.CompilationFingerprint);
    }

    private static ResolvedCapability CreateCapability(
        string workflowId,
        long targetStart = 0,
        string publicationToken = "headless-publication")
    {
        bool replace = workflowId is ExperienceIds.DpReplace or ExperienceIds.CtrlRamReplace;
        InputOversizePolicy sourcePolicy = workflowId switch
        {
            ExperienceIds.StandardMerge or ExperienceIds.AbMerge =>
                InputOversizePolicy.ExtractDeclaredRange,
            ExperienceIds.CtrlRamReplace => InputOversizePolicy.TruncateWithWarning,
            _ => InputOversizePolicy.Reject,
        };
        var source = new AddressSpace(
            SourceSpace,
            4,
            AddressSpaceMutability.Immutable,
            inputOversizePolicy: sourcePolicy);
        var output = new AddressSpace("output-image", 8, AddressSpaceMutability.Mutable);
        List<AddressSpace> spaces = [source, output];
        ImageInitialization initialization;
        if (replace)
        {
            spaces.Insert(0, new AddressSpace("reference-base", 8, AddressSpaceMutability.Immutable));
            initialization = ImageInitialization.Reference("output-image", "reference-base", 8);
        }
        else
        {
            initialization = ImageInitialization.Blank("output-image", 8, 0xFF);
        }

        CompositionOperation operation = replace
            ? CompositionOperation.ReplaceRange(
                "replace-source",
                100,
                SourceSpace,
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(targetStart, 4),
                OverlapPolicy.Reject,
                "Synthetic replacement.")
            : CompositionOperation.CopyRange(
                "copy-source",
                100,
                SourceSpace,
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(targetStart, 4),
                OverlapPolicy.Reject,
                "Synthetic merge.");
        var plan = new CompositionPlan(initialization, spaces, [operation]);
        string mapId = $"{workflowId}-map";
        CompiledComposition composition = CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                $"synthetic-{workflowId}",
                "1.0.0",
                "NT-HEADLESS",
                workflowId,
                workflowId,
                replace ? CompositionKind.Replace : CompositionKind.Merge),
            $"synthetic-{workflowId}.bin",
            icNumberPolicy: replace
                ? CompiledIcNumberPolicy.SingleSelector
                : CompiledIcNumberPolicy.NotApplicable,
            mapId: mapId);
        var identity = new CapabilityRouteIdentity(
            "NT-HEADLESS",
            workflowId,
            "none",
            mapId);
        var token = new ResolutionToken(publicationToken);
        return new ResolvedCapability(
            identity,
            CapabilityFingerprint,
            composition,
            Decision(identity, CapabilityAuthoringAvailability.Available),
            Decision(identity, CapabilityPublicationStatus.TestOnly),
            Decision(identity, CapabilityEvidenceStatus.SyntheticOracle),
            MetadataPlanDefinition.Empty.Resolve(token),
            token);
    }

    private static ResolvedCapabilityRoute CreateRoute(string workflowId)
    {
        var identity = new CapabilityRouteIdentity(
            "NT-HEADLESS",
            workflowId,
            "none",
            $"{workflowId}-map");
        var contract = new CanonicalCapabilityCompilationContract(
            $"synthetic-{workflowId}",
            "1.0.0",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            [$"{workflowId}-map"],
            CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId);
        string fingerprint = CapabilityDefinitionFingerprint.Compute(
            identity,
            contract.ProfileId,
            contract.ProfileVersion,
            contract.TrustedDefinitionSha256,
            contract.AllowedMapVariantIds,
            contract.CompilerSemanticId,
            contract.SemanticBindingIds);
        var definition = new CanonicalDynamicCapabilityDefinition(
            identity,
            fingerprint,
            contract,
            Decision(identity, fingerprint, CapabilityAuthoringAvailability.Available),
            Decision(identity, fingerprint, CapabilityPublicationStatus.TestOnly),
            Decision(identity, fingerprint, CapabilityEvidenceStatus.SyntheticOracle));
        return new ResolvedCapabilityRoute(
            definition,
            new ResolutionToken("headless-pending-publication"));
    }

    private static PinnedCapabilityDecision<T> Decision<T>(
        CapabilityRouteIdentity identity,
        T value)
        where T : struct, Enum
    {
        return Decision(identity, CapabilityFingerprint, value);
    }

    private static PinnedCapabilityDecision<T> Decision<T>(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        T value)
        where T : struct, Enum
    {
        return new PinnedCapabilityDecision<T>(
            $"headless-{typeof(T).Name}",
            identity.RouteId,
            capabilityFingerprint,
            value,
            "synthetic-headless-contract");
    }
}
