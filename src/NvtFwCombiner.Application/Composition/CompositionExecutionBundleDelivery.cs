using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Path-free exact accepted source shown before bundle delivery.</summary>
public sealed class CompositionOutputBundleSourceSummary
{
    internal CompositionOutputBundleSourceSummary(
        string bindingId,
        string slotId,
        string originalFileName,
        FileStamp fileStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        BindingId = bindingId;
        SlotId = slotId;
        OriginalFileName = originalFileName;
        Size = fileStamp.AcceptedLength;
        Sha256 = fileStamp.Sha256;
    }

    /// <summary>Canonical compiled input binding.</summary>
    public string BindingId { get; }

    /// <summary>Canonical compiled slot.</summary>
    public string SlotId { get; }

    /// <summary>Safe original plain filename.</summary>
    public string OriginalFileName { get; }

    /// <summary>Accepted complete source size.</summary>
    public long Size { get; }

    /// <summary>Accepted complete source SHA-256.</summary>
    public string Sha256 { get; }
}

/// <summary>One immutable accepted source retained privately for atomic delivery.</summary>
internal sealed class CompositionExecutionBundleSource
{
    internal CompositionExecutionBundleSource(
        string bindingId,
        string slotId,
        string acceptedIdentity,
        FileStamp fileStamp,
        string originalFileName,
        ReadOnlySpan<byte> bytes)
    {
        Summary = new CompositionOutputBundleSourceSummary(
            bindingId,
            slotId,
            originalFileName,
            fileStamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedIdentity);
        AcceptedIdentity = acceptedIdentity;
        FileStamp = fileStamp;
        Bytes = bytes.ToArray();
    }

    internal CompositionOutputBundleSourceSummary Summary { get; }

    internal string AcceptedIdentity { get; }

    internal FileStamp FileStamp { get; }

    internal ReadOnlyMemory<byte> Bytes { get; }
}

/// <summary>Exact accepted-session, naming, and source admission retained by one proposal.</summary>
internal sealed class CompositionOutputBundleAdmission
{
    internal CompositionOutputBundleAdmission(
        ActiveSessionSnapshot session,
        CompositionOutputPreparation outputPreparation,
        IReadOnlyList<CompositionExecutionBundleSource> sources)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(outputPreparation);
        ArgumentNullException.ThrowIfNull(sources);
        AcceptedCapability = session.ExactCapability ?? throw new InvalidOperationException(
            "Bundle preparation requires one exact accepted capability.");
        WorkflowId = session.WorkflowId;
        SelectedRouteId = session.SelectedRouteId;
        ResolutionToken = session.ResolutionToken;
        AuthoringRevision = session.AuthoringRevision;
        CapabilityFingerprint = session.CapabilityFingerprint;
        CompilationFingerprint = session.CompilationFingerprint ??
            throw new InvalidOperationException(
                "Bundle preparation requires one exact compilation fingerprint.");
        if (!StringComparer.Ordinal.Equals(
                AcceptedCapability.CompiledComposition.CompilationFingerprint,
                CompilationFingerprint))
        {
            throw new InvalidOperationException(
                "Bundle preparation compilation does not match its accepted capability.");
        }

        ValidateCanonicalOutput(outputPreparation.OutputName);
        OutputPreparation = outputPreparation;
        Sources = Array.AsReadOnly([.. sources]);
        PublicationIdentity = new CompositionOutputBundleAdmissionIdentity(
            WorkflowId,
            SelectedRouteId,
            ResolutionToken,
            AuthoringRevision,
            CapabilityFingerprint,
            CompilationFingerprint);
    }

    internal ResolvedCapability AcceptedCapability { get; }

    internal string WorkflowId { get; }

    internal string SelectedRouteId { get; }

    internal ResolutionToken ResolutionToken { get; }

    internal AuthoringRevision AuthoringRevision { get; }

    internal string CapabilityFingerprint { get; }

    internal string CompilationFingerprint { get; }

    internal CompositionOutputPreparation OutputPreparation { get; }

    internal IReadOnlyList<CompositionExecutionBundleSource> Sources { get; }

    internal CompositionOutputBundleAdmissionIdentity PublicationIdentity { get; }

    internal OutputNameResolution PreparedOutputName => new(
        OutputPreparation.OutputName.FileName,
        OutputPreparation.OutputName.OutputNaming,
        OutputPreparation.OutputName.Issues);

    internal void ValidateSession(ActiveSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!PublicationIdentity.Matches(
                session.WorkflowId,
                session.SelectedRouteId,
                session.ResolutionToken,
                session.AuthoringRevision,
                session.CapabilityFingerprint,
                session.CompilationFingerprint,
                ReferenceEquals(session.ExactCapability, AcceptedCapability),
                session.ExecutionAdmitted))
        {
            throw new InvalidOperationException(
                "Bundle admission does not match the exact accepted session publication.");
        }
    }

    private static void ValidateCanonicalOutput(CompositionOutputNamePreview output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanUseAutomaticName ||
            output.OutputNaming is { IsExplicitOverride: true } ||
            (output.OutputNaming is { } naming && !StringComparer.Ordinal.Equals(
                output.FileName,
                naming.AutomaticFileName)))
        {
            throw new InvalidOperationException(
                "Bundle preparation requires one canonical automatic output name.");
        }
    }
}

/// <summary>Exact accepted publication coordinates retained independently of host paths.</summary>
internal sealed record CompositionOutputBundleAdmissionIdentity(
    string WorkflowId,
    string SelectedRouteId,
    ResolutionToken ResolutionToken,
    AuthoringRevision AuthoringRevision,
    string CapabilityFingerprint,
    string CompilationFingerprint)
{
    internal bool Matches(
        string workflowId,
        string selectedRouteId,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string capabilityFingerprint,
        string? compilationFingerprint,
        bool exactCapabilityMatches,
        bool executionAdmitted)
    {
        return StringComparer.Ordinal.Equals(workflowId, WorkflowId) &&
            StringComparer.Ordinal.Equals(selectedRouteId, SelectedRouteId) &&
            resolutionToken == ResolutionToken &&
            authoringRevision == AuthoringRevision &&
            StringComparer.Ordinal.Equals(capabilityFingerprint, CapabilityFingerprint) &&
            StringComparer.Ordinal.Equals(compilationFingerprint, CompilationFingerprint) &&
            exactCapabilityMatches &&
            executionAdmitted;
    }
}

/// <summary>Application-owned canonical source planner for prepared bundle delivery.</summary>
internal static class CompositionOutputBundleSourcePlanner
{
    internal static IReadOnlyList<CompositionExecutionBundleSource> Create(
        ActiveSessionSnapshot session,
        ICompositionArtifactIdentityPolicy identityPolicy)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(identityPolicy);
        ResolvedCapability capability = session.ExactCapability ??
            throw new InvalidOperationException(
                "Bundle source planning requires one exact accepted capability.");
        Dictionary<string, AuthoringInputSlotStatus> statuses = session.InputSlotStatuses
            .Where(static status => status.AcceptedByteArray is not null)
            .ToDictionary(static status => status.AddressSpaceId, StringComparer.Ordinal);
        List<CompositionOutputBundleSourceCandidate> candidates = [];
        foreach (CompiledInputSpaceBinding binding in capability.CompiledComposition.V2Details
                     .InputContract.SpaceBindings)
        {
            if (!statuses.TryGetValue(
                    binding.AddressSpaceId,
                    out AuthoringInputSlotStatus? status) ||
                status.SelectedPathHint is not { } artifactLocator ||
                VirtualArtifactLocator.IsVirtual(artifactLocator))
            {
                continue;
            }

            byte[] bytes = status.AcceptedByteArray!;
            FileStamp stamp = status.FileStamp ?? throw new InvalidOperationException(
                $"Bundle source '{binding.AddressSpaceId}' has no accepted content stamp.");
            if (stamp != FileStamp.FromBytes(bytes))
            {
                throw new InvalidOperationException(
                    $"Bundle source '{binding.AddressSpaceId}' bytes conflict with its accepted stamp.");
            }

            candidates.Add(new CompositionOutputBundleSourceCandidate(
                binding.AddressSpaceId,
                status.SlotId,
                artifactLocator,
                stamp,
                bytes));
        }

        return Canonicalize(candidates, identityPolicy);
    }

    internal static IReadOnlyList<CompositionExecutionBundleSource> Canonicalize(
        IEnumerable<CompositionOutputBundleSourceCandidate> candidates,
        ICompositionArtifactIdentityPolicy identityPolicy)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(identityPolicy);
        List<CompositionExecutionBundleSource> sources = [];
        foreach (CompositionOutputBundleSourceCandidate candidate in candidates)
        {
            if (VirtualArtifactLocator.IsVirtual(candidate.ArtifactLocator))
            {
                continue;
            }

            if (candidate.FileStamp != FileStamp.FromBytes(candidate.Bytes.Span))
            {
                throw new InvalidOperationException(
                    $"Bundle source '{candidate.BindingId}' bytes conflict with its accepted stamp.");
            }

            CompositionAcceptedArtifactIdentity identity =
                identityPolicy.Resolve(candidate.ArtifactLocator);
            CompositionExecutionBundleSource? duplicate = sources.FirstOrDefault(source =>
                StringComparer.Ordinal.Equals(
                    source.AcceptedIdentity,
                    identity.CanonicalIdentity));
            if (duplicate is not null)
            {
                if (duplicate.FileStamp != candidate.FileStamp ||
                    !duplicate.Bytes.Span.SequenceEqual(candidate.Bytes.Span))
                {
                    throw new InvalidOperationException(
                        $"Bundle source identity '{candidate.BindingId}' has conflicting accepted content.");
                }

                continue;
            }

            sources.Add(new CompositionExecutionBundleSource(
                candidate.BindingId,
                candidate.SlotId,
                identity.CanonicalIdentity,
                candidate.FileStamp,
                identity.OriginalFileName,
                candidate.Bytes.Span));
        }

        return new ReadOnlyCollection<CompositionExecutionBundleSource>(sources);
    }
}

/// <summary>Path-bearing internal source input consumed only by the platform identity seam.</summary>
internal sealed class CompositionOutputBundleSourceCandidate
{
    internal CompositionOutputBundleSourceCandidate(
        string bindingId,
        string slotId,
        string artifactLocator,
        FileStamp fileStamp,
        ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactLocator);
        BindingId = bindingId;
        SlotId = slotId;
        ArtifactLocator = artifactLocator;
        FileStamp = fileStamp;
        Bytes = bytes.ToArray();
    }

    internal string BindingId { get; }

    internal string SlotId { get; }

    internal string ArtifactLocator { get; }

    internal FileStamp FileStamp { get; }

    internal ReadOnlyMemory<byte> Bytes { get; }
}

/// <summary>Application-owned delivery projection consumed directly from one prepared admission.</summary>
internal sealed class CompositionExecutionBundleDelivery
{
    internal CompositionExecutionBundleDelivery(CompositionOutputBundleIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ParentDirectory = intent.ParentDirectory;
        FolderName = intent.FolderName;
        Admission = intent.Admission;
        Sources = intent.Admission.Sources;
        AdditionalDelivery = intent.AdditionalDelivery;
    }

    internal string ParentDirectory { get; }

    internal string FolderName { get; }

    internal CompositionOutputBundleAdmission Admission { get; }

    internal IReadOnlyList<CompositionExecutionBundleSource> Sources { get; }

    internal CompositionAdditionalDeliveryPlan? AdditionalDelivery { get; }

    internal static CompositionAdditionalDeliveryPlan? ResolveAdditionalDelivery(
        IReadOnlyList<CompositionAdditionalDeliveryPlan> preparedDeliveries,
        string? additionalDeliveryKind)
    {
        ArgumentNullException.ThrowIfNull(preparedDeliveries);
        if (additionalDeliveryKind is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(additionalDeliveryKind);
        return preparedDeliveries.SingleOrDefault(delivery => StringComparer.Ordinal.Equals(
                delivery.DeliveryKind,
                additionalDeliveryKind)) ??
            throw new ArgumentException(
                $"Bundle additional delivery '{additionalDeliveryKind}' is not declared by the exact prepared output.",
                nameof(additionalDeliveryKind));
    }
}
