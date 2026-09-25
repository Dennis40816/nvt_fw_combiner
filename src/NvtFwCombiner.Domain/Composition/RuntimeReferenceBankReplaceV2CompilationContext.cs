using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Trusted AB finalization already declared by the selected layout's processor contract.</summary>
public enum BankReferenceFinalizationKind
{
    /// <summary>Restore the three checked B Header addresses after the local postbuild.</summary>
    RestoreAddresses,
    /// <summary>Run the trusted AB stage after local postbuild to relocate B Header addresses and CRC.</summary>
    RunAbHeaderProcessor,
}

/// <summary>Pure trusted definition identity, independent of an artifact or resolved runtime metadata.</summary>
public sealed class BankReferenceDefinitionSource
{
    internal BankReferenceDefinitionSource(string profileId, string profileVersion, ProfileBundleIdentity bundle,
        ProfileBundleEntryIdentity entry, string familyHash, string memberId, string mapId, long capacityBytes)
    {
        ProfileId = RequiredValue.NotBlank(profileId);
        ProfileVersion = RequiredValue.NotBlank(profileVersion);
        Bundle = RequiredValue.NotNull(bundle);
        Entry = RequiredValue.NotNull(entry);
        FamilyHash = CanonicalSha256.Require(familyHash, nameof(familyHash));
        MemberId = RequiredValue.NotBlank(memberId);
        MapId = RequiredValue.NotBlank(mapId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);
        CapacityBytes = capacityBytes;
    }

    /// <summary>Exact source profile identity.</summary>
    public string ProfileId { get; }
    /// <summary>Exact source profile version.</summary>
    public string ProfileVersion { get; }
    /// <summary>Real trusted source bundle.</summary>
    public ProfileBundleIdentity Bundle { get; }
    /// <summary>Real allowlisted profile entry.</summary>
    public ProfileBundleEntryIdentity Entry { get; }
    /// <summary>Trusted canonical family content hash.</summary>
    public string FamilyHash { get; }
    /// <summary>Declared member selection.</summary>
    public string MemberId { get; }
    /// <summary>Exact declared physical map.</summary>
    public string MapId { get; }
    /// <summary>Capacity declared by the exact hashed map.</summary>
    public long CapacityBytes { get; }
}

/// <summary>Closed composite definition; both bundle entries remain real sources, never synthesized entries.</summary>
public sealed class BankReferenceReplaceDefinition
{
    /// <summary>Versioned semantics of the compiler-owned checked bank composition.</summary>
    public const string CompilerSemanticId = "nfc.compiler.profile-bundle-v2.runtime-bank-reference-replace.v1";

    internal BankReferenceReplaceDefinition(CompiledComposition layout, CompiledComposition local,
        ByteRange? localBankRange = null, BankReferenceFinalizationKind finalizationKind = BankReferenceFinalizationKind.RestoreAddresses)
        : this(Source(layout, layoutSource: true), Source(local, layoutSource: false), localBankRange, finalizationKind)
    {
    }

    internal BankReferenceReplaceDefinition(BankReferenceDefinitionSource layout, BankReferenceDefinitionSource local,
        ByteRange? localBankRange = null, BankReferenceFinalizationKind finalizationKind = BankReferenceFinalizationKind.RestoreAddresses)
    {
        Layout = RequiredValue.NotNull(layout);
        Local = RequiredValue.NotNull(local);
        ClosedEnum.ThrowIfUndefined(finalizationKind, "Unknown bank finalization kind.");
        DomainInvariant.Reject(Layout.MemberId != Local.MemberId || Layout.CapacityBytes % 2 != 0,
            "Bank Replace parents must describe the same requested member and two equal AB banks.");
        BankCapacityBytes = Layout.CapacityBytes / 2;
        LocalBankRange = localBankRange ?? new ByteRange(0, Local.CapacityBytes);
        FinalizationKind = finalizationKind;
        DomainInvariant.Reject(LocalBankRange.Length != Local.CapacityBytes ||
            LocalBankRange.EndExclusive > BankCapacityBytes,
            "The local CtrlRAM Reference must be an exact declared slice of one AB bank.");
        const string localSegment = "-ctrlram-replace-";
        int segment = Local.ProfileId.IndexOf(localSegment, StringComparison.Ordinal);
        DomainInvariant.Reject(segment < 0, "Bank Replace local profile has no canonical CtrlRAM identity segment.");
        DefinitionId = Local.ProfileId.Insert(segment + 1, "ab-");
        Version = "1.0.0";
        var builder = new StringBuilder();
        AppendField(builder, "definition.id", DefinitionId);
        AppendField(builder, "definition.version", Version);
        AppendField(builder, "compiler", CompilerSemanticId);
        if (FinalizationKind != BankReferenceFinalizationKind.RestoreAddresses ||
            LocalBankRange != new ByteRange(0, BankCapacityBytes))
        {
            AppendField(builder, "bank.capacity", BankCapacityBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendField(builder, "local.start", LocalBankRange.Start.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendField(builder, "local.length", LocalBankRange.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendField(builder, "bank.finalization", FinalizationKind.ToString());
        }
        AppendSource("layout", Layout);
        AppendSource("local", Local);
        ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));

        void AppendSource(string prefix, BankReferenceDefinitionSource details)
        {
            AppendField(builder, prefix + ".profile", details.ProfileId);
            AppendField(builder, prefix + ".version", details.ProfileVersion);
            AppendField(builder, prefix + ".bundle", details.Bundle.ContentHash);
            AppendField(builder, prefix + ".entry", details.Entry.ContentHash);
            AppendField(builder, prefix + ".family", details.FamilyHash);
            AppendField(builder, prefix + ".member", details.MemberId);
            AppendField(builder, prefix + ".map", details.MapId);
        }
    }

    private static BankReferenceDefinitionSource Source(CompiledComposition composition, bool layoutSource)
    {
        V2CompiledCompositionDetails details = composition.V2Details;
        V2CompilationProvenance provenance = details.Provenance;
        DomainInvariant.Reject(layoutSource ? provenance.Context is not ResolvedMapV2CompilationContext
            : provenance.Context is not RuntimeReferenceReplaceV2CompilationContext, "Wrong bank definition source context.");
        return new BankReferenceDefinitionSource(details.ProfileId, details.ProfileVersion, provenance.Bundle, provenance.ProfileEntry,
            provenance.Context.FamilyContentHash, provenance.Context.MemberId, provenance.ResolvedMap.ImageMap.MapId,
            provenance.ResolvedMap.ImageMap.CapacityBytes);
    }

    /// <summary>Identity of this composite definition, distinct from either parent profile.</summary>
    public string DefinitionId { get; }
    /// <summary>Closed composition contract version.</summary>
    public string Version { get; }
    /// <summary>Stable hash of both definition sources and lowering semantics, without user input bytes.</summary>
    public string ContentHash { get; }
    /// <summary>True AB layout definition source.</summary>
    public BankReferenceDefinitionSource Layout { get; }
    /// <summary>True local Replace definition source.</summary>
    public BankReferenceDefinitionSource Local { get; }
    /// <summary>Physical capacity of each complete AB bank.</summary>
    public long BankCapacityBytes { get; }
    /// <summary>Exact bank-relative slice accepted by the existing local CtrlRAM profile.</summary>
    public ByteRange LocalBankRange { get; }
    /// <summary>Checked end-of-bank finalization selected by the exact trusted AB profile.</summary>
    public BankReferenceFinalizationKind FinalizationKind { get; }
}

/// <summary>One selected bank's immutable local compilation, reference identity and final output placement.</summary>
public sealed class CompiledReferenceBank
{
    internal CompiledReferenceBank(string bankId, string workspaceId, ByteRange outputRange,
        FirmwareArtifactIdentity reference, CompiledComposition local)
    {
        BankId = RequiredValue.NotBlank(bankId);
        WorkspaceId = RequiredValue.NotBlank(workspaceId);
        OutputRange = outputRange;
        Reference = reference;
        LocalComposition = local;
    }

    /// <summary>Canonical region-instance identity.</summary>
    public string BankId { get; }
    /// <summary>Private engine-owned local workspace.</summary>
    public string WorkspaceId { get; }
    /// <summary>Bank placement in the complete AB output address space.</summary>
    public ByteRange OutputRange { get; }
    /// <summary>Exact derived local reference, identified in the reserved private-input namespace.</summary>
    public FirmwareArtifactIdentity Reference { get; }
    /// <summary>Original trusted local compiler result, including every validation obligation.</summary>
    public CompiledComposition LocalComposition { get; }
}

/// <summary>Candidate CtrlRAM composition over an immutable AB layout; the source map keeps its original mode.</summary>
public sealed class RuntimeReferenceBankReplaceV2CompilationContext : MapBoundV2CompilationContext
{
    internal RuntimeReferenceBankReplaceV2CompilationContext(CompiledComposition layout, FirmwareArtifactIdentity reference,
        BankReferenceReplaceDefinition definition, CompositionPlan checkedPlan, IEnumerable<CompiledReferenceBank> banks)
        : base(layout.V2Details.Provenance.ResolvedMap, ExperienceIds.CtrlRamReplace)
    {
        LayoutComposition = layout;
        Reference = reference;
        CheckedPlan = checkedPlan;
        Banks = Array.AsReadOnly(banks.ToArray());
        DomainInvariant.Reject(Banks.Count is < 1 or > 2 || Banks.Select(static bank => bank.BankId).Distinct(StringComparer.Ordinal).Count() != Banks.Count,
            "A checked AB compilation requires one or two unique canonical banks.");
        Definition = RequiredValue.NotNull(definition);
        DomainInvariant.Reject(new BankReferenceReplaceDefinition(layout, Banks[0].LocalComposition,
                Definition.LocalBankRange, Definition.FinalizationKind).ContentHash != Definition.ContentHash,
            "The checked AB definition must match both compiled trusted parents and its local bank slice.");
        foreach (CompiledReferenceBank bank in Banks)
        {
            DomainInvariant.Reject(bank.BankId is not ("a-bank" or "b-bank") ||
                !bank.Reference.ArtifactId.StartsWith("ab-replace/", StringComparison.Ordinal) ||
                bank.OutputRange.EndExclusive > reference.LengthBytes || bank.OutputRange.Length != bank.Reference.LengthBytes ||
                new BankReferenceReplaceDefinition(layout, bank.LocalComposition,
                    Definition.LocalBankRange, Definition.FinalizationKind).ContentHash != Definition.ContentHash,
                "Bank identity, reference, placement and parent definition must agree.");
        }
    }

    /// <summary>Real composite definition whose primary source is the retained AB layout.</summary>
    public BankReferenceReplaceDefinition Definition { get; }
    /// <summary>AB Merge compilation used only for layout and relocation authority.</summary>
    public CompiledComposition LayoutComposition { get; }
    /// <summary>Actual immutable source extent retained by the compiled AB layout, when declared.</summary>
    public SourceEnvelopeExtent? SourceEnvelope =>
        (LayoutComposition.V2Details.Provenance.Context as ResolvedMapV2CompilationContext)?.SourceEnvelope;
    /// <summary>Full Reference identity checked before deriving any private bank input.</summary>
    public FirmwareArtifactIdentity Reference { get; }
    /// <summary>Selected bank obligations in deterministic order.</summary>
    public IReadOnlyList<CompiledReferenceBank> Banks { get; }
    // Exact plan emitted by the checked compiler; capability binding cannot substitute host operations.
    internal CompositionPlan CheckedPlan { get; }
}
