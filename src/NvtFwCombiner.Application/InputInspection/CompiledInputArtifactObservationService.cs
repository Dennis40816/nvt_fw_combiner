using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Closed version facts decoded from one compiled input role.</summary>
public enum CompiledInputVersionKind
{
    /// <summary>DP bank A CMI version.</summary>
    DpA,

    /// <summary>DP bank B CMI version.</summary>
    DpB,

    /// <summary>TP bank A firmware version.</summary>
    TpA,

    /// <summary>TP bank B firmware version.</summary>
    TpB,
}

/// <summary>One typed version observation from the accepted immutable source snapshot.</summary>
public sealed record CompiledInputVersionObservation
{
    internal CompiledInputVersionObservation(
        CompiledInputVersionKind kind,
        byte? major,
        byte? minor,
        ushort? trackerId = null)
    {
        if (major.HasValue != minor.HasValue || (!major.HasValue && trackerId.HasValue))
        {
            throw new ArgumentException("Known version components must be supplied together.");
        }

        Kind = kind;
        Major = major;
        Minor = minor;
        TrackerId = trackerId;
    }

    /// <summary>Compiled semantic version role.</summary>
    public CompiledInputVersionKind Kind { get; }

    /// <summary>Major version byte, or null when unreadable.</summary>
    public byte? Major { get; }

    /// <summary>Minor version byte, or null when unreadable.</summary>
    public byte? Minor { get; }

    /// <summary>Optional project tracker id decoded with a DP CMI version.</summary>
    public ushort? TrackerId { get; }

    /// <summary>True only when both version components were decoded.</summary>
    public bool IsKnown => Major.HasValue;
}

/// <summary>One stable non-blocking diagnostic owned by compiled input inspection.</summary>
public sealed record CompiledInputArtifactInspectionAdvisory(
    string IssueCode,
    CompiledInputArtifactInspectionNextAction NextAction);

/// <summary>Immutable typed observations and advisories from one canonical input inspection.</summary>
public sealed class CompiledInputArtifactObservationResult
{
    internal CompiledInputArtifactObservationResult(
        IEnumerable<CompiledInputVersionObservation> versions,
        IEnumerable<CompiledInputArtifactInspectionAdvisory> advisories)
    {
        Versions = Array.AsReadOnly([.. versions]);
        Advisories = Array.AsReadOnly([.. advisories]);
    }

    /// <summary>No role-specific observations or advisories.</summary>
    public static CompiledInputArtifactObservationResult Empty { get; } = new([], []);

    /// <summary>Typed version facts, if declared by the compiled input role.</summary>
    public IReadOnlyList<CompiledInputVersionObservation> Versions { get; }

    /// <summary>Stable accepted-input advisories ordered by issue code.</summary>
    public IReadOnlyList<CompiledInputArtifactInspectionAdvisory> Advisories { get; }
}

/// <summary>Application-owned observation policy selected only by compiled role and naming contracts.</summary>
internal static class CompiledInputArtifactObservationService
{
    private const string DpRole = "dp-ab";
    private const string TpARole = "tp-a";
    private const string TpBRole = "tp-b";

    internal static CompiledInputArtifactObservationResult Observe(
        CompiledComposition composition,
        string addressSpaceId,
        ReadOnlyMemory<byte>? sourceBytes,
        CompiledInputArtifactInspectionResult? inspection)
    {
        CompiledOutputNamingRequirement naming = composition.V2Details.OutputNamingRequirement;
        if (naming.RendererKind != CompiledOutputNameRendererKind.AbCodeV1)
        {
            return CompiledInputArtifactObservationResult.Empty;
        }

        CompiledInputSpaceBinding binding = composition.V2Details.InputContract.SpaceBindings.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
        CompiledInputSlotRequirement slot = composition.V2Details.InputContract.Slots.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.SlotId, binding.SlotId));
        CompiledInputVersionObservation[] versions = slot.Role switch
        {
            DpRole => ObserveDp(composition, sourceBytes, inspection),
            TpARole => [DecodeTp(
                CompiledInputVersionKind.TpA,
                GetAcceptedSnapshot(sourceBytes, inspection))],
            TpBRole => [DecodeTp(
                CompiledInputVersionKind.TpB,
                GetAcceptedSnapshot(sourceBytes, inspection))],
            _ => throw new InvalidOperationException(
                $"AB Code naming declares unsupported compiled input role '{slot.Role}'."),
        };
        CompiledInputArtifactInspectionAdvisory[] advisories =
            inspection is { BlocksBuild: false } && versions.Any(static version => !version.IsKnown)
                ? [new(
                    InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown,
                    CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion)]
                : [];
        return new CompiledInputArtifactObservationResult(versions, advisories);
    }

    private static CompiledInputVersionObservation[] ObserveDp(
        CompiledComposition composition,
        ReadOnlyMemory<byte>? sourceBytes,
        CompiledInputArtifactInspectionResult? inspection)
    {
        ReadOnlyMemory<byte> snapshot = GetAcceptedSnapshot(sourceBytes, inspection);
        return
        [
            DecodeDpRegion(
                composition,
                CompiledInputVersionKind.DpA,
                "a-cmi-dp-version",
                snapshot),
            DecodeDpRegion(
                composition,
                CompiledInputVersionKind.DpB,
                "b-cmi-dp-version",
                snapshot),
        ];
    }

    internal static CompiledInputVersionObservation DecodeDpRegion(
        CompiledComposition composition,
        CompiledInputVersionKind kind,
        string regionId,
        ReadOnlyMemory<byte> snapshot)
    {
        FirmwareRegion? region = composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions
            .SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.RegionId, regionId));
        if (region is null || region.Range.Length != 3 || region.Range.Start < 0 ||
            region.Range.EndExclusive > snapshot.Length || region.Range.Start > int.MaxValue)
        {
            return Unknown(kind);
        }

        ReadOnlySpan<byte> registers = snapshot.Span.Slice(checked((int)region.Range.Start), 3);
        byte register16 = registers[0];
        byte register18 = registers[2];
        ushort trackerId = (ushort)(register16 | ((register18 & 0x0F) << 8));
        return new CompiledInputVersionObservation(
            kind,
            registers[1],
            (byte)(register18 >> 4),
            trackerId == 0 ? null : trackerId);
    }

    internal static CompiledInputVersionObservation DecodeTp(
        CompiledInputVersionKind kind,
        ReadOnlyMemory<byte> snapshot)
    {
        return FirmwareConfigMetadataReader.TryReadBackup(snapshot.Span, out FirmwareConfigMetadata metadata) &&
            metadata.IsFirmwareVersionBarValid
                ? new CompiledInputVersionObservation(
                    kind,
                    metadata.FirmwareVersion,
                    metadata.FirmwareSubVersion)
                : Unknown(kind);
    }

    private static ReadOnlyMemory<byte> GetAcceptedSnapshot(
        ReadOnlyMemory<byte>? sourceBytes,
        CompiledInputArtifactInspectionResult? inspection)
    {
        return sourceBytes is { } source &&
            inspection?.AcceptedSnapshotRange is { Start: 0 } accepted &&
            accepted.EndExclusive <= source.Length &&
            accepted.Length <= int.MaxValue
                ? source[..checked((int)accepted.Length)]
                : ReadOnlyMemory<byte>.Empty;
    }

    private static CompiledInputVersionObservation Unknown(CompiledInputVersionKind kind)
    {
        return new CompiledInputVersionObservation(kind, null, null);
    }
}
