using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Executes one exact accepted authoring session through the shared composition
/// service without reopening an operator-selected input path.
/// </summary>
internal static class AcceptedSessionCompositionExecution
{
    internal static async ValueTask<CompositionRunResult> ExecuteAsync(
        ICanonicalCapabilityQuery capabilities,
        string runId,
        ActiveSessionSnapshot acceptedSession,
        ResolvedCapability acceptedCapability,
        IReadOnlyList<InputArtifactBinding> bindings,
        IReadOnlyDictionary<string, byte[]> acceptedArtifacts,
        string outputFileName,
        bool build,
        ISystemClock clock,
        ICompositionOutputWriter? outputWriter,
        IExternalProcessor? externalProcessor,
        ICompositionDeliveryWriter? deliveryWriter,
        IcNumberSelection? icNumberSelection,
        bool outputFileNameIsOverride,
        TopologySelection? abMergeTopologySelection,
        IReadOnlyList<CompositionIssue>? advisoryIssues,
        GeneralAuthoringAdmissionSummary? generalAdmission,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(progress);
        var inputs = AcceptedSessionExecutionInputs.Create(
            capabilities,
            acceptedSession,
            acceptedCapability,
            bindings,
            acceptedArtifacts);
        AcceptedOutputNamingPublication? outputNaming =
            AcceptedOutputNamingInspection.TryAcceptForCompiledRenderer(acceptedSession);
        var request = new CompositionRunRequest(
            runId,
            inputs.Capability.CompiledComposition,
            inputs.Bindings,
            outputFileName,
            icNumberSelection: icNumberSelection,
            outputFileNameIsOverride: outputFileNameIsOverride,
            abMergeTopologySelection: abMergeTopologySelection,
            advisoryIssues: advisoryIssues,
            generalAdmission: generalAdmission,
            outputNamingInspection: outputNaming?.Inspection,
            outputNamingAdmission: outputNaming?.Admission,
            resolvedCapability: inputs.Capability);
        var service = new CompositionRunService(
            inputs.Reader,
            clock,
            outputWriter,
            externalProcessor,
            deliveryWriter);
        CompositionRunResult result = await service
            .PreviewOrBuildAsync(request, build, progress, cancellationToken)
            .ConfigureAwait(false);
        result.ResolvedCapability = inputs.Capability;
        return result;
    }
}

/// <summary>Application-owned immutable inputs admitted for one accepted execution.</summary>
internal sealed class AcceptedSessionExecutionInputs
{
    private AcceptedSessionExecutionInputs(
        ResolvedCapability capability,
        IReadOnlyList<InputArtifactBinding> bindings,
        IArtifactReader reader)
    {
        Capability = capability;
        Bindings = bindings;
        Reader = reader;
    }

    internal ResolvedCapability Capability { get; }

    internal IReadOnlyList<InputArtifactBinding> Bindings { get; }

    internal IArtifactReader Reader { get; }

    internal static (
        InputArtifactBinding[] Bindings,
        IReadOnlyDictionary<string, byte[]> Artifacts) CreateBindings(
        CompiledComposition compiledComposition,
        ActiveSessionSnapshot acceptedSession)
    {
        return CreateBindings(
            compiledComposition,
            acceptedSession,
            status => status.SelectedPathHint ?? throw new InvalidOperationException(
                $"Input slot '{status.SlotId}' has no accepted selected path."));
    }

    internal static (
        InputArtifactBinding[] Bindings,
        IReadOnlyDictionary<string, byte[]> Artifacts) CreateBindings(
        CompiledComposition compiledComposition,
        ActiveSessionSnapshot acceptedSession,
        Func<AuthoringInputSlotStatus, string> resolveSelectedPath)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ArgumentNullException.ThrowIfNull(resolveSelectedPath);
        var statuses = acceptedSession
            .InputSlotStatuses
            .Where(static status => status.AcceptedByteArray is not null)
            .ToDictionary(static status => status.AddressSpaceId, StringComparer.Ordinal);
        if (!acceptedSession.HasCurrentInputInspection ||
            compiledComposition.Plan.RequiredInputAddressSpaceIds.Any(
                addressSpaceId => !statuses.ContainsKey(addressSpaceId)))
        {
            throw new InvalidOperationException(
                "Execution requires one current immutable inspection publication for every required input.");
        }

        (InputArtifactBinding Binding, byte[] Bytes)[] accepted =
        [
            .. compiledComposition.Plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId =>
                {
                    AuthoringInputSlotStatus status = statuses[addressSpaceId];
                    return (
                        CreateBinding(
                            compiledComposition,
                            addressSpaceId,
                            resolveSelectedPath(status),
                            acceptedSession,
                            status.SlotId),
                        status.AcceptedByteArray!);
                }),
        ];
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        return (
            [.. accepted.Select(static input => input.Binding)],
            accepted.ToDictionary(
                static input => input.Binding.ArtifactId,
                static input => input.Bytes,
                pathComparer));
    }

    internal static (
        InputArtifactBinding[] Bindings,
        IReadOnlyDictionary<string, byte[]> Artifacts) CreateGeneralBindings(
        CompiledComposition compiledComposition,
        ActiveSessionSnapshot acceptedSession,
        IEnumerable<InputArtifactBinding> plannedBindings,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>>? virtualArtifacts = null)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ArgumentNullException.ThrowIfNull(plannedBindings);
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        List<InputArtifactBinding> bindings = [];
        Dictionary<string, byte[]> artifacts = new(pathComparer);
        foreach (InputArtifactBinding planned in plannedBindings)
        {
            if (virtualArtifacts?.TryGetValue(
                    planned.ArtifactId,
                    out ReadOnlyMemory<byte> virtualBytes) == true)
            {
                bindings.Add(CreateCompiledBinding(
                    compiledComposition,
                    planned.AddressSpaceId,
                    planned.ArtifactId));
                artifacts.Add(planned.ArtifactId, virtualBytes.ToArray());
                continue;
            }

            AuthoringSlotState slot = acceptedSession.Slots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.DefinitionId, planned.BindingId)) ??
                throw new InvalidOperationException(
                    $"The accepted session does not contain General input '{planned.BindingId}'.");
            bool pathMatches = slot.SelectedPath is { } selectedPath &&
                string.Equals(
                    Path.GetFullPath(selectedPath),
                    Path.GetFullPath(planned.ArtifactId),
                    pathComparison);
            if (!pathMatches ||
                slot.FileStamp is not { } acceptedStamp ||
                (planned.AcceptedContentStamp is { } plannedStamp &&
                 plannedStamp != acceptedStamp) ||
                slot.AcceptedByteArray is null)
            {
                throw new InvalidOperationException(
                    $"General input '{planned.BindingId}' has no current immutable accepted bytes.");
            }

            bindings.Add(CreateCompiledBinding(
                compiledComposition,
                planned.AddressSpaceId,
                planned.ArtifactId,
                acceptedContentStamp: acceptedStamp));

            if (artifacts.TryGetValue(planned.ArtifactId, out byte[]? existingBytes))
            {
                if (!existingBytes.AsSpan().SequenceEqual(slot.AcceptedByteArray))
                {
                    throw new InvalidOperationException(
                        $"General input '{planned.ArtifactId}' has conflicting accepted bytes.");
                }
            }
            else
            {
                artifacts.Add(planned.ArtifactId, slot.AcceptedByteArray);
            }
        }

        return ([.. bindings], artifacts);
    }

    internal static (
        InputArtifactBinding[] Bindings,
        IReadOnlyDictionary<string, byte[]> Artifacts) CreateGeneralReplaceBindings(
        CompiledComposition compiledComposition,
        ActiveSessionSnapshot acceptedSession,
        IEnumerable<InputArtifactBinding> plannedBindings,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> virtualArtifacts,
        string referenceAddressSpaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceAddressSpaceId);
        (InputArtifactBinding[] mappings, IReadOnlyDictionary<string, byte[]> accepted) =
            CreateGeneralBindings(
                compiledComposition,
                acceptedSession,
                plannedBindings,
                virtualArtifacts);
        AuthoringSlotState reference = acceptedSession.Slots.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.DefinitionId, referenceAddressSpaceId)) ??
            throw new InvalidOperationException(
                $"The accepted session does not contain reference input '{referenceAddressSpaceId}'.");
        string selectedPath = reference.SelectedPath ??
            throw new InvalidOperationException(
                "The accepted General Replace reference has no selected path identity.");
        byte[] referenceBytes = reference.AcceptedByteArray ??
            throw new InvalidOperationException(
                "The accepted General Replace reference has no immutable bytes.");
        InputArtifactBinding referenceBinding = CreateBinding(
            compiledComposition,
            referenceAddressSpaceId,
            selectedPath,
            acceptedSession,
            referenceAddressSpaceId);
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        Dictionary<string, byte[]> artifacts = new(accepted, pathComparer);
        _ = artifacts.TryAdd(referenceBinding.ArtifactId, referenceBytes) ||
            artifacts[referenceBinding.ArtifactId].AsSpan().SequenceEqual(referenceBytes)
                ? true
                : throw new InvalidOperationException(
                    "The accepted General Replace reference conflicts with another accepted artifact.");

        return ([referenceBinding, .. mappings], artifacts);
    }

    internal static string ResolveReferenceImageAddressSpaceId(
        CompiledComposition compiledComposition)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        V2CompiledCompositionDetails details = compiledComposition.V2Details;
        string[] referenceSlots =
        [
            .. details.InputContract.Slots
                .Where(static slot =>
                    slot.ArtifactClass == CompiledInputArtifactClass.ReferenceImage)
                .Select(static slot => slot.SlotId),
        ];
        return referenceSlots.Length == 1
            ? details.InputContract.SpaceBindings.Single(binding =>
                StringComparer.Ordinal.Equals(
                    binding.SlotId,
                    referenceSlots[0])).AddressSpaceId
            : throw new InvalidOperationException(
                "General Replace execution requires one compiled reference-image input.");
    }

    internal static ResolvedCapability RequireCapability(
        ActiveSessionSnapshot session,
        string workflowId,
        string icId,
        AuthoringDerivedResultKind resultKind)
    {
        ArgumentNullException.ThrowIfNull(session);
        ResolvedCapability? capability = session.GetAcceptedCapability(resultKind);
        return capability is not null &&
            StringComparer.Ordinal.Equals(session.WorkflowId, workflowId) &&
            StringComparer.Ordinal.Equals(
                capability.Identity.IcId,
                NormalizeIcId(icId)) &&
            (resultKind != AuthoringDerivedResultKind.Inspection || session.HasCurrentInputInspection)
                ? capability
                : throw new InvalidOperationException(
                    "The run requires one exact current accepted authoring compilation.");
    }

    internal static InputArtifactBinding CreateBinding(
        CompiledComposition compiledComposition,
        string addressSpaceId,
        string selectedPath,
        ActiveSessionSnapshot acceptedSession,
        string? slotDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        string definitionId = ResolveSlotDefinitionId(
            compiledComposition.V2Details.InputContract.SpaceBindings,
            addressSpaceId,
            slotDefinitionId);
        AuthoringSlotState slot = acceptedSession.Slots.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.DefinitionId, definitionId)) ??
            throw new InvalidOperationException(
                $"The accepted session does not contain input slot '{definitionId}'.");
        string fullPath = Path.GetFullPath(selectedPath);
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool pathMatches = slot.SelectedPath is { } acceptedPath &&
            string.Equals(Path.GetFullPath(acceptedPath), fullPath, pathComparison);
        FileStamp stamp = pathMatches &&
            slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning &&
            slot.FileStamp is { } acceptedStamp
                ? acceptedStamp
                : throw new InvalidOperationException(
                    $"Input slot '{definitionId}' does not match its accepted inspected file.");

        return CreateCompiledBinding(
            compiledComposition,
            addressSpaceId,
            fullPath,
            stamp);
    }

    internal static string ResolveSlotDefinitionId(
        IReadOnlyList<CompiledInputSpaceBinding> bindings,
        string addressSpaceId,
        string? explicitSlotDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        return explicitSlotDefinitionId ?? bindings.Single(binding =>
            StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId)).SlotId;
    }

    internal static InputArtifactBinding CreateCompiledBinding(
        CompiledComposition compiledComposition,
        string addressSpaceId,
        string artifactId,
        FileStamp? acceptedContentStamp = null)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        V2CompiledCompositionDetails details = compiledComposition.V2Details;
        CompiledInputSpaceBinding spaceBinding = details.InputContract.SpaceBindings.SingleOrDefault(binding =>
            StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId)) ??
            throw new InvalidOperationException(
                $"V2 compiled input contract does not declare address space '{addressSpaceId}'.");
        CompiledInputSlotRequirement slot = details.InputContract.Slots.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.SlotId, spaceBinding.SlotId)) ??
            throw new InvalidOperationException(
                $"V2 compiled input contract does not declare slot '{spaceBinding.SlotId}'.");
        string originalFileName = Path.GetFileName(artifactId);
        return string.IsNullOrWhiteSpace(originalFileName)
            ? throw new ArgumentException(
                "V2 input artifacts require a plain original filename.",
                nameof(artifactId))
            : new InputArtifactBinding(
                addressSpaceId,
                addressSpaceId,
                artifactId,
                originalFileName,
                slot.ArtifactClass,
                acceptedContentStamp);
    }

    internal static AcceptedSessionExecutionInputs Create(
        ICanonicalCapabilityQuery capabilities,
        ActiveSessionSnapshot acceptedSession,
        ResolvedCapability acceptedCapability,
        IReadOnlyList<InputArtifactBinding> bindings,
        IReadOnlyDictionary<string, byte[]> acceptedArtifacts)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ArgumentNullException.ThrowIfNull(acceptedCapability);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(acceptedArtifacts);
        CompiledComposition composition = acceptedCapability.CompiledComposition;
        ResolvedCapability current = capabilities.ResolveCurrentCompilation(
                composition,
                acceptedCapability) ??
            throw new InvalidOperationException(
                "Execution requires the exact accepted compilation to remain current.");
        if (!ReferenceEquals(current, acceptedCapability))
        {
            throw new InvalidOperationException(
                "Execution requires the catalog to retain the exact accepted capability.");
        }

        if (!ReferenceEquals(acceptedSession.ExactCapability, acceptedCapability))
        {
            throw new InvalidOperationException(
                "Execution requires the session to retain the exact accepted capability.");
        }

        if (!StringComparer.Ordinal.Equals(
                acceptedSession.CompilationFingerprint,
                composition.CompilationFingerprint))
        {
            throw new InvalidOperationException(
                "Execution requires the session to retain the exact compilation fingerprint.");
        }

        bool hasAcceptedPublication = ReferenceEquals(
                acceptedSession.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection),
                acceptedCapability) ||
            ReferenceEquals(
                acceptedSession.GetAcceptedCapability(AuthoringDerivedResultKind.Validation),
                acceptedCapability);
        if (!acceptedSession.ExecutionAdmitted || !hasAcceptedPublication)
        {
            throw new InvalidOperationException(
                "Execution requires one current admitted inspection or validation publication.");
        }

        InputArtifactBinding[] copiedBindings = [.. bindings];
        StringComparer artifactComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var copiedArtifacts = new Dictionary<string, byte[]>(artifactComparer);
        foreach ((string artifactId, byte[] bytes) in acceptedArtifacts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
            ArgumentNullException.ThrowIfNull(bytes);
            copiedArtifacts.Add(artifactId, [.. bytes]);
        }

        foreach (InputArtifactBinding binding in copiedBindings)
        {
            if (!copiedArtifacts.ContainsKey(binding.ArtifactId))
            {
                throw new InvalidOperationException(
                    $"Accepted execution input '{binding.ArtifactId}' has no immutable bytes.");
            }
        }

        return new AcceptedSessionExecutionInputs(
            current,
            Array.AsReadOnly(copiedBindings),
            new AcceptedArtifactReader(copiedArtifacts));
    }

    private sealed class AcceptedArtifactReader(
        IReadOnlyDictionary<string, byte[]> artifacts) : IArtifactReader
    {
        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !artifacts.TryGetValue(artifactId, out byte[]? bytes)
                ? ValueTask.FromException<ReadOnlyMemory<byte>>(
                    new FileNotFoundException(
                        "The accepted immutable input artifact is unavailable.",
                        artifactId))
                : ValueTask.FromResult<ReadOnlyMemory<byte>>(bytes);
        }
    }

    private static string NormalizeIcId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}
