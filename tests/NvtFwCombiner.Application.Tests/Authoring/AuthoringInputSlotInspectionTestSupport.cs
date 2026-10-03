using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

internal static class AuthoringInputSlotInspectionTestSupport
{
    internal const string CapabilityFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string SourceSpace = "source-input";
    internal const string SourceSlot = "source-input-slot";

    internal static ResolvedCapability CreateCapability(
        string workflowId,
        long targetStart = 0,
        string publicationToken = "headless-publication",
        CompiledValidationRequirement? validationRequirement = null,
        bool observeAbVersion = false,
        bool includeExternalProcessor = false,
        bool tpMaximum = false,
        MetadataPlanDefinition? metadataPlan = null)
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
        List<CompositionOperation> operations = [operation];
        if (includeExternalProcessor)
        {
            operations.Add(CompositionOperation.RunExternalProcessor(
                "refresh-integrity",
                200,
                "output-image",
                new ByteRange(0, 8),
                new ExternalProcessorInvocation(
                    "crc-worker",
                    "nvt-crc-worker",
                    [new ByteRange(0, 8)],
                    [new ByteRange(0, 8)]),
                OverlapPolicy.ReplaceExisting,
                "Synthetic external integrity refresh."));
        }

        var plan = new CompositionPlan(initialization, spaces, operations);
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
            observeAbVersion
                ? CompiledOutputNamingRequirement.AbCodeV1Template
                : $"synthetic-{workflowId}.bin",
            icNumberInputMode: replace
                ? IcNumberInputMode.SingleSelector
                : null,
            validationRequirements: validationRequirement is null ? null : [validationRequirement],
            mapId: mapId,
            inputRolesByAddressSpace: observeAbVersion
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SourceSpace] = "tp-a",
                }
                : null,
            outputRequiredTokenIds: observeAbVersion
                ? ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"]
                : null,
            allowOutputOverride: false,
            inputLengthRequirement: tpMaximum
                ? new CompiledSourceViewCoverageInputLengthRequirement(
                    maximumBytes: InputLengthPolicyLimits.MaximumTpFirmwareBytes)
                : null);
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
            (metadataPlan ?? MetadataPlanDefinition.Empty).Resolve(token),
            token);
    }

    internal static ResolvedCapabilityRoute CreateRoute(string workflowId)
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

    internal static PinnedCapabilityDecision<T> Decision<T>(
        CapabilityRouteIdentity identity,
        T value)
        where T : struct, Enum
    {
        return Decision(identity, CapabilityFingerprint, value);
    }

    internal static PinnedCapabilityDecision<T> Decision<T>(
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
