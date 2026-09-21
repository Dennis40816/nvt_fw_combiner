using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed composite definition; both bundle entries remain real sources, never synthesized entries.</summary>
public sealed class BankReferenceReplaceDefinition
{
    /// <summary>Versioned semantics of the compiler-owned checked bank composition.</summary>
    public const string CompilerSemanticId = "nfc.compiler.profile-bundle-v2.runtime-bank-reference-replace.v1";

    internal BankReferenceReplaceDefinition(CompiledComposition layout, CompiledComposition local)
    {
        Layout = layout.V2Details;
        Local = local.V2Details;
        DomainInvariant.Reject(Layout.ProfileId != "nt51929-ab-merge" || Layout.ProfileVersion != "0.4.0" ||
            Local.ProfileId != "nt51929-ctrlram-replace-fw200-single" || Local.ProfileVersion != "0.3.0" ||
            Layout.Provenance.Context.MemberId != "NT51929" || Local.Provenance.Context.MemberId != "NT51929" ||
            Layout.Provenance.Context is not ResolvedMapV2CompilationContext ||
            Local.Provenance.Context is not RuntimeReferenceReplaceV2CompilationContext,
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

        void AppendSource(string prefix, V2CompiledCompositionDetails details)
        {
            V2CompilationProvenance provenance = details.Provenance;
            AppendField(builder, prefix + ".profile", details.ProfileId);
            AppendField(builder, prefix + ".version", details.ProfileVersion);
            AppendField(builder, prefix + ".bundle", provenance.Bundle.ContentHash);
            AppendField(builder, prefix + ".entry", provenance.ProfileEntry.ContentHash);
            AppendField(builder, prefix + ".family", provenance.Context.FamilyContentHash);
            AppendField(builder, prefix + ".member", provenance.Context.MemberId);
            AppendField(builder, prefix + ".map", provenance.ResolvedMap.ImageMap.MapId);
        }
    }

    /// <summary>Identity of this composite definition, distinct from either parent profile.</summary>
    public string DefinitionId { get; }
    /// <summary>Closed composition contract version.</summary>
    public string Version { get; }
    /// <summary>Stable hash of both definition sources and lowering semantics, without user input bytes.</summary>
    public string ContentHash { get; }
    /// <summary>True AB layout definition source.</summary>
    public V2CompiledCompositionDetails Layout { get; }
    /// <summary>True local Replace definition source.</summary>
    public V2CompiledCompositionDetails Local { get; }
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
