using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Path-free format evidence captured once at AB execution admission, never reinterpreted on Save.</summary>
public sealed class AbMergeFormatRunSummary
{
    private AbMergeFormatRunSummary(
        AbMergeFormatSelection selection, PrimaryEvidence tpA, PrimaryEvidence tpB)
    {
        FormatId = selection.FormatId;
        DisplayName = selection.DisplayName;
        ConfigurationGeneration = selection.ConfigurationGeneration;
        ConfigurationSourceSha256 = selection.ConfigurationSourceSha256;
        FamilyId = selection.FamilyId;
        FamilyVersion = selection.FamilyVersion;
        FamilyContentHash = selection.FamilyContentHash;
        TpA = tpA;
        TpB = tpB;
    }

    /// <summary>Maintainer-declared effective format id.</summary>
    public string FormatId { get; }
    /// <summary>Display alias from the captured configuration, not the current settings.</summary>
    public string DisplayName { get; }
    /// <summary>Host configuration generation captured at execution admission.</summary>
    public long ConfigurationGeneration { get; }
    /// <summary>SHA-256 of the captured configuration source.</summary>
    public string ConfigurationSourceSha256 { get; }
    /// <summary>Canonical family that owns the format policy.</summary>
    public string FamilyId { get; }
    /// <summary>Captured canonical family version.</summary>
    public string FamilyVersion { get; }
    /// <summary>Captured canonical family content hash.</summary>
    public string FamilyContentHash { get; }
    /// <summary>TP A primary evidence linked to a parent report input binding.</summary>
    public PrimaryEvidence TpA { get; }
    /// <summary>TP B primary evidence linked to a parent report input binding.</summary>
    public PrimaryEvidence TpB { get; }

    internal static AbMergeFormatRunSummary Create(
        AbMergeFormatSelection selection,
        CompiledComposition composition,
        IReadOnlyList<InputArtifactBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(bindings);
        return !composition.IsV2AbMergeRuntimeRoute ||
            composition.V2Details.Provenance.Context is not MapBoundV2CompilationContext context ||
            selection.MapId != context.ResolvedMap.ImageMap.MapId ||
            selection.FamilyId != context.FamilyId ||
            selection.FamilyVersion != context.FamilyVersion ||
            selection.FamilyContentHash != context.FamilyContentHash
                ? throw new InvalidOperationException("Captured AB format must belong to the exact compiled map and family.")
                : new(selection,
                    CapturePrimary(selection.TpAPrimary, selection.TpAFormatByte,
                        CompositionAddressSpaceIds.TpAInput, context, composition, bindings),
                    CapturePrimary(selection.TpBPrimary, selection.TpBFormatByte,
                        CompositionAddressSpaceIds.TpBInput, context, composition, bindings));
    }

    private static PrimaryEvidence CapturePrimary(
        FirmwareMetadataStructureResolution primary, byte formatByte,
        string expectedSpace, MapBoundV2CompilationContext context,
        CompiledComposition composition, IReadOnlyList<InputArtifactBinding> bindings)
    {
        FirmwareResolvedMetadataStructure resolved = primary.Resolved ??
            throw new InvalidOperationException("Captured format requires resolved primary evidence.");
        FirmwareMetadataStructure canonical = context.ResolvedMap.ImageMap.MetadataSetBindings
            .Where(binding => binding.EffectiveKey.MemberId == context.MemberId)
            .SelectMany(static binding => binding.Value.Structures)
            .Single(structure => structure.StructureId == primary.MetadataStructureId);
        CompiledInputSpaceBinding space = composition.V2Details.InputContract.SpaceBindings.Single(binding =>
            binding.AddressSpaceId == expectedSpace);
        InputArtifactBinding binding = bindings.Single(binding => binding.AddressSpaceId == space.AddressSpaceId);
        FirmwareArtifactIdentity identity = resolved.ArtifactIdentity;
        return canonical.ArtifactBindingId != expectedSpace || primary.ArtifactBindingId != expectedSpace ||
            !ReferenceEquals(canonical, resolved.StructureDefinition) || identity.ArtifactId != space.AddressSpaceId ||
            binding.AcceptedContentStamp is not FileStamp stamp ||
            stamp.AcceptedLength != identity.LengthBytes || stamp.Sha256 != identity.Sha256
                ? throw new InvalidOperationException("Captured primary evidence must match the immutable execution input.")
                : new PrimaryEvidence(binding.BindingId, primary.MetadataStructureId,
                    resolved.LocatorOutcome.ResolvedRange, formatByte);
    }

    /// <summary>Scalar projection of canonical primary resolution, without artifact locators or source bytes.</summary>
    public sealed class PrimaryEvidence
    {
        internal PrimaryEvidence(string inputBindingId, string structureId, FirmwareAddressedRange range, byte formatByte)
        {
            InputBindingId = inputBindingId;
            StructureId = structureId;
            AddressSpaceId = range.AddressSpaceId;
            Start = range.Range.Start;
            EndExclusive = range.Range.EndExclusive;
            FormatByte = formatByte;
        }

        /// <summary>Report-safe input binding id, not its infrastructure locator.</summary>
        public string InputBindingId { get; }
        /// <summary>Canonical primary metadata structure identity.</summary>
        public string StructureId { get; }
        /// <summary>Canonical address space of the resolved structure.</summary>
        public string AddressSpaceId { get; }
        /// <summary>Inclusive resolved structure start.</summary>
        public long Start { get; }
        /// <summary>Exclusive resolved structure end.</summary>
        public long EndExclusive { get; }
        /// <summary>Observed primary format byte; A and B may differ while selecting the same format.</summary>
        public byte FormatByte { get; }
    }
}

[JsonSerializable(typeof(AbMergeFormatRunSummary))]
internal sealed partial class AbMergeFormatReportJsonContext : JsonSerializerContext;
