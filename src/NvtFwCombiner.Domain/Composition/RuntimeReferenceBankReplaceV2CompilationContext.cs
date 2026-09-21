using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

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

    internal BankReferenceReplaceDefinition(CompiledComposition layout, CompiledComposition local)
        : this(Source(layout, layoutSource: true), Source(local, layoutSource: false))
    {
    }

    internal BankReferenceReplaceDefinition(BankReferenceDefinitionSource layout, BankReferenceDefinitionSource local)
    {
        Layout = RequiredValue.NotNull(layout);
        Local = RequiredValue.NotNull(local);
        DomainInvariant.Reject(Layout.ProfileId != "nt51929-ab-merge" || Layout.ProfileVersion != "0.4.0" ||
            Local.ProfileId != "nt51929-ctrlram-replace-fw200-single" || Local.ProfileVersion != "0.3.0" ||
            Layout.MemberId != "NT51929" || Local.MemberId != "NT51929" ||
            Layout.MapId != "nt51929-ab-merge-512k" || Local.MapId != "nt51929-ctrlram-fw200-single-full-flash",
            "Bank Replace definition is closed to the admitted NT51929 layout/local profile pair.");
        DefinitionId = "nt51929-ab-ctrlram-replace-fw200-single";
        Version = "1.0.0";
        var builder = new StringBuilder();
        AppendField(builder, "definition.id", DefinitionId);
        AppendField(builder, "definition.version", Version);
        AppendField(builder, "compiler", CompilerSemanticId);
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
        CompositionPlan checkedPlan, IEnumerable<CompiledReferenceBank> banks)
        : base(layout.V2Details.Provenance.ResolvedMap, ExperienceIds.CtrlRamReplace)
    {
        LayoutComposition = layout;
        Reference = reference;
        CheckedPlan = checkedPlan;
        Banks = Array.AsReadOnly(banks.ToArray());
        DomainInvariant.Reject(Banks.Count is < 1 or > 2 || Banks.Select(static bank => bank.BankId).Distinct(StringComparer.Ordinal).Count() != Banks.Count,
            "A checked AB compilation requires one or two unique canonical banks.");
        Definition = new BankReferenceReplaceDefinition(layout, Banks[0].LocalComposition);
        foreach (CompiledReferenceBank bank in Banks)
        {
            DomainInvariant.Reject(bank.BankId is not ("a-bank" or "b-bank") ||
                !bank.Reference.ArtifactId.StartsWith("ab-replace/", StringComparison.Ordinal) ||
                bank.OutputRange.EndExclusive > reference.LengthBytes || bank.OutputRange.Length != bank.Reference.LengthBytes ||
                new BankReferenceReplaceDefinition(layout, bank.LocalComposition).ContentHash != Definition.ContentHash,
                "Bank identity, reference, placement and parent definition must agree.");
        }
    }

    /// <summary>Real composite definition whose primary source is the retained AB layout.</summary>
    public BankReferenceReplaceDefinition Definition { get; }
    /// <summary>AB Merge compilation used only for layout and relocation authority.</summary>
    public CompiledComposition LayoutComposition { get; }
    /// <summary>Full Reference identity checked before deriving any private bank input.</summary>
    public FirmwareArtifactIdentity Reference { get; }
    /// <summary>Selected bank obligations in deterministic order.</summary>
    public IReadOnlyList<CompiledReferenceBank> Banks { get; }
    // Exact plan emitted by the checked compiler; capability binding cannot substitute host operations.
    internal CompositionPlan CheckedPlan { get; }
}
