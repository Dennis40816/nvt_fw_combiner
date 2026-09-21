using System.Security.Cryptography;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Configuration;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Real compiled primary metadata drives one Application format decision; not runtime publication.</summary>
public sealed partial class AbMergeFormatAdmissionTests
{
    private const string ConfigHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static AbMergeFormatAdmissionResult AssessFormat(FirmwareFamilyResolutionDefinition family, string ic,
        EventBufferFormatConfigurationState? state, TopologySelection? topology,
        IReadOnlyCollection<FirmwareBinInspectionArtifact>? artifacts)
    {
        return AbMergeFormatAdmission.Assess(family, ic, state, topology, artifacts, InputCandidates(ic, topology));
    }

    private static AbMergeFormatAdmissionResult AssessFormat(FirmwareFamilyResolutionDefinition family, string ic,
        EventBufferFormatConfigurationState? state, MetadataInspectionSnapshot? primary, TopologySelection? topology,
        IReadOnlyCollection<FirmwareBinInspectionArtifact>? artifacts)
    {
        return AbMergeFormatAdmission.Assess(family, ic, state, primary, topology, artifacts, InputCandidates(ic, topology));
    }

    private static CompiledComposition[] InputCandidates(string ic, TopologySelection? topology)
    {
        TopologySelection? effective = ic == "NT51950" ? topology ?? Topology(1) : null;
        var compositions = new List<CompiledComposition>();
        foreach (ResolvedCapabilityRoute route in BootstrapTestHost.Services.Catalog.GetCurrentSnapshot().DynamicRoutes.Where(route =>
            route.Identity.WorkflowId == ExperienceIds.AbMerge && route.Identity.IcId == ic &&
            (route.AbMergeTopologyChoice is { } choice ? effective is not null && choice.Selection.ChipCount == 1 == (effective.ChipCount == 1) : effective is null)))
        {
            Assert.True(BootstrapTestHost.Services.Compiler.TryCompilePublishedDynamicCapability(route.Identity, null, null,
                out CompiledComposition? compiled, out _, out IReadOnlyList<CompositionIssue> issues, effective), string.Join(',', issues.Select(static issue => issue.Code)));
            compositions.Add(compiled!);
        }
        Assert.NotEmpty(compositions);
        return [.. compositions];
    }

    /// <summary>Real persisted configuration must be consumable immediately and after a fresh session reload.</summary>
    [Theory]
    [InlineData(false, 0x97, "desay", "nt51951-ab-desay-1024k")]
    [InlineData(true, 0x97, "desay", "nt51951-ab-desay-1024k")]
    [InlineData(false, 0x84, "common", "nt51951-ab-merge-1024k")]
    [InlineData(true, 0x84, "common", "nt51951-ab-merge-1024k")]
    public async Task PersistedConfigurationIsAcceptedByFormatAdmissionAsync(bool reload, byte raw, string format, string map)
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        FirmwareAbFormatPolicy policy = Assert.IsType<FirmwareAbFormatPolicy>(family.AbFormatPolicy);
        EventBufferFormatIdentity[] identities = [.. policy.Formats.Select(item =>
            new EventBufferFormatIdentity(item.UniqueId, item.DisplayName))];
        EventBufferFormatDraftEntry[] defaults = [.. policy.Formats.Select(item => new EventBufferFormatDraftEntry(
            item.UniqueId, null, [.. item.DefaultRecognitionValues.Select(value => (int)value)]))];
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.PathFor("config/event-buffer-format.v1.json");
        using var savedSession = new EventBufferFormatConfigurationSession(policy.ScopeId, identities, defaults,
            new EventBufferFormatConfigurationStorage(new LocalFileStore(), path));
        EventBufferFormatConfigurationOperationResult saved = await savedSession.SaveAsync(
            savedSession.CreateDefaultsDraft(), TestContext.Current.CancellationToken);
        Assert.True(saved.Succeeded);
        using var freshSession = new EventBufferFormatConfigurationSession(policy.ScopeId, identities, defaults,
            new EventBufferFormatConfigurationStorage(new LocalFileStore(), path));
        EventBufferFormatConfigurationOperationResult current = reload
            ? await freshSession.ReloadAsync(TestContext.Current.CancellationToken)
            : saved;
        Assert.True(current.Succeeded);
        byte[] tp = Tp(raw);
        AbMergeFormatAdmissionResult result = AssessFormat(family, "NT51951", current.State,
            Inspect(plan, tp, tp), null, Artifacts(tp, tp));

        Assert.True(result.Succeeded, string.Join(" | ", result.Issues.Select(issue => issue.Code)));
        AbMergeFormatSelection selected = Assert.IsType<AbMergeFormatSelection>(result.Selection);
        Assert.Equal(format, selected.FormatId);
        Assert.Equal(map, selected.MapId);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), selected.ConfigurationSourceSha256);
        Assert.Equal(saved.State.SourceSha256, current.State.SourceSha256);
    }

    /// <summary>Raw Desay IDs may differ; Common exact-two requires both observed counts.</summary>
    [Theory]
    [InlineData("NT51950", 1, 1, 1, 0x97, 0xA6, "desay", "nt51950-ab-desay-single-1024k")]
    [InlineData("NT51950", 2, 3, 3, 0xA6, 0x97, "desay", "nt51950-ab-desay-cascade-1024k")]
    [InlineData("NT51950", 2, 2, 2, 0x84, 0x85, "common", "nt51950-ab-common-exact2-1024k")]
    [InlineData("NT51950", 2, 3, 3, 0x84, 0x85, "common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 3, 3, 0x84, 0x84, "common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 1, 1, 1, 0x84, 0x84, "common", "nt51950-ab-merge-512k")]
    [InlineData("NT51951", 0, 1, 1, 0x97, 0xA6, "desay", "nt51951-ab-desay-1024k")]
    [InlineData("NT51951", 0, 2, 2, 0x84, 0x84, "common", "nt51951-ab-merge-1024k")]
    public void CompiledPrimaryAndSuccessfulTopologySelectDeclaredMap(
        string ic, int requestedCount, byte countA, byte countB, byte rawA, byte rawB, string format, string map)
    {
        MetadataPlanDefinition plan = Plan(ic, requestedCount);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        byte[] tpA = Tp(rawA, countA);
        byte[] tpB = Tp(rawB, countB);

        MetadataInspectionSnapshot primary = Inspect(plan, tpA, tpB);
        TopologySelection? topology = Topology(requestedCount);
        AbMergeFormatAdmissionResult result = AssessFormat(family, ic, Configuration(family), primary,
            topology, Artifacts(tpA, tpB));

        Assert.True(result.Succeeded, string.Join(" | ", result.Issues.Select(issue => issue.Code)));
        AbMergeFormatSelection selected = Assert.IsType<AbMergeFormatSelection>(result.Selection);
        Assert.Equal(map, selected.MapId);
        Assert.Equal(format, selected.FormatId);
        Assert.Equal(rawA, selected.TpAFormatByte);
        Assert.Equal(rawB, selected.TpBFormatByte);
        Assert.Equal(ConfigHash, selected.ConfigurationSourceSha256);
        Assert.Equal(family.FamilyContentHash, selected.FamilyContentHash);
        Assert.Same(primary, selected.PrimaryInspection);
    }

    /// <summary>Changing alias alone cannot select another map; configured bytes actually change recognition.</summary>
    [Theory]
    [InlineData(0x97, "customer alias", "desay", "customer alias")]
    [InlineData(0x84, "customer alias", "common", "Common")]
    public void ConfigurationValuesAndDisplayAliasHaveSeparateAuthority(byte recognition, string alias, string expectedFormat, string label)
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        AbMergeFormatAdmissionResult result = AssessFormat(family, "NT51951",
            Configuration(family, values: [recognition], alias: alias), Inspect(plan, Tp(0x97), Tp(0x97)), null, Artifacts(Tp(0x97), Tp(0x97)));
        Assert.True(result.Succeeded);
        Assert.Equal(expectedFormat, result.Selection!.FormatId);
        Assert.Equal(label, result.Selection.DisplayName);
    }

    /// <summary>Two valid but different effective formats never deliver a selected map.</summary>
    [Fact]
    public void EffectiveFormatMismatchBlocks()
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        AssertBlocked(AssessFormat(family, "NT51951", Configuration(family),
            Inspect(plan, Tp(0x97), Tp(0x84)), null, Artifacts(Tp(0x97), Tp(0x84))), "AB_FORMAT_MISMATCH");
    }

    /// <summary>Invalid primary relations cannot borrow valid Backup values or imply Common.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidOrTruncatedPrimaryBlocks(bool truncate)
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        byte[] tpA = Tp(0x84);
        if (truncate)
        {
            tpA = tpA[..0x22228];
        }
        else
        {
            tpA[0x22201] = 0;
        }

        AssertBlocked(AssessFormat(family, "NT51951", Configuration(family),
            Inspect(plan, tpA, Tp(0x84)), null, Artifacts(tpA, Tp(0x84))), "AB_FORMAT_PRIMARY_INVALID");
    }

    /// <summary>Absent, invalid or wrong-scope config cannot reuse LastSaved or become Common.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConfigurationFailureNeverFallsBack(int failure)
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        EventBufferFormatConfigurationState saved = Configuration(family);
        EventBufferFormatConfigurationState? state = failure switch
        {
            0 => null,
            1 => saved with { Status = EventBufferFormatConfigurationStatus.Invalid, Configuration = null },
            2 => Configuration(family, scope: "different-scope"),
            _ => saved with { SourceSha256 = null },
        };
        AssertBlocked(AssessFormat(family, "NT51951", state,
            Inspect(plan, Tp(0x84), Tp(0x84)), null, Artifacts(Tp(0x84), Tp(0x84))), "AB_FORMAT_CONFIGURATION_INVALID");
    }

    /// <summary>Missing or duplicate observations cannot lend a fact to another slot.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrimaryBindingMustHaveOneResult(bool duplicate)
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        MetadataInspectionSnapshot source = Inspect(plan, Tp(0x97), Tp(0xA6));
        var primary = new MetadataInspectionSnapshot(source.ResolutionToken, source.AuthoringRevision, source.ArtifactIdentities,
            duplicate ? [.. source.Results, source.Results[0]] : [source.Results[1]]);
        AssertBlocked(AssessFormat(family, "NT51951", Configuration(family), primary, null, Artifacts(Tp(0x97), Tp(0xA6))),
            "AB_FORMAT_PRIMARY_INVALID");
    }

    /// <summary>Rejected topology cannot be hidden by two apparent exact counts.</summary>
    [Fact]
    public void TopologyFailurePrecedesExactOverride()
    {
        MetadataPlanDefinition plan = Plan("NT51950", 1);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        byte[] a = Tp(0x84, 2);
        byte[] b = Tp(0x84, 2);
        TopologySelection topology = Topology(1)!;
        AssertBlocked(AssessFormat(family, "NT51950", Configuration(family), Inspect(plan, a, b),
            topology, Artifacts(a, b)), "AB_TP_TOPOLOGY_SELECTION_MISMATCH");
    }

    /// <summary>An observation from another selected member or without artifact provenance cannot be borrowed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignOrUnboundObservationsAreRejected(bool missingIdentities)
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        MetadataInspectionSnapshot primary = Inspect(plan, Tp(0x97), Tp(0x97));
        primary = missingIdentities
            ? new(primary.ResolutionToken, primary.AuthoringRevision, [], primary.Results)
            : Inspect(Plan("NT51950", 1), Tp(0x97, 1), Tp(0x97, 1));

        byte count = missingIdentities ? (byte)2 : (byte)1;
        AssertBlocked(AssessFormat(family, "NT51951", Configuration(family), primary, null, Artifacts(Tp(0x97, count), Tp(0x97, count))),
            "AB_FORMAT_PRIMARY_INVALID");
    }

    /// <summary>Matching scope alone does not admit a now-unknown configured identity.</summary>
    [Fact]
    public void StaleConfigurationCatalogIsRejected()
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        EventBufferFormatConfigurationState state = Configuration(family);
        EventBufferFormatConfiguration stale = EventBufferFormatConfigurationAdmission.Admit(
            state.Configuration!.ScopeId, [new("unknown-vendor", "Old vendor")],
            [new("unknown-vendor", null, [0x97])]).Configuration!;
        AssertBlocked(AssessFormat(family, "NT51951", state with { Configuration = stale },
            Inspect(plan, Tp(0x97), Tp(0x97)), null, Artifacts(Tp(0x97), Tp(0x97))), "AB_FORMAT_CONFIGURATION_INVALID");
    }

    /// <summary>A valid result object for the other slot cannot satisfy this slot's primary binding.</summary>
    [Fact]
    public void CrossSlotResolutionIsRejected()
    {
        MetadataPlanDefinition plan = Plan("NT51951", 0);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        MetadataInspectionSnapshot primary = Inspect(plan, Tp(0x97), Tp(0xA6));
        MetadataInspectionResult[] results = [.. primary.Results];
        results[0] = results[0] with { Resolution = results[1].Resolution };
        primary = new(primary.ResolutionToken, primary.AuthoringRevision, primary.ArtifactIdentities, results);
        AssertBlocked(AssessFormat(family, "NT51951", Configuration(family), primary, null, Artifacts(Tp(0x97), Tp(0xA6))),
            "AB_FORMAT_PRIMARY_INVALID");
    }

    /// <summary>Old count admission cannot be combined with new inputs or a changed selector.</summary>
    [Theory]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    public void OldTopologySuccessCannotAuthorizeAnotherInputOrSelector(byte currentCount, int selectedCount)
    {
        MetadataPlanDefinition plan = Plan("NT51950", selectedCount);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        MetadataPlanDefinition oldPlan = Plan("NT51950", 2);
        FirmwareFamilyResolutionDefinition oldFamily = oldPlan.Entries[0].FamilyDefinition;
        AbMergeFormatAdmissionResult previous = AssessFormat(oldFamily, "NT51950", Configuration(oldFamily),
            Inspect(oldPlan, Tp(0x84, 2), Tp(0x84, 2)), Topology(2), Artifacts(Tp(0x84, 2), Tp(0x84, 2)));
        Assert.True(previous.Succeeded);
        Assert.Equal("nt51950-ab-common-exact2-1024k", previous.Selection!.MapId);
        AbMergeFormatAdmissionResult result = AssessFormat(family, "NT51950", Configuration(family),
            Inspect(plan, Tp(0x84, currentCount), Tp(0x84, currentCount)), Topology(selectedCount), Artifacts(Tp(0x84, currentCount), Tp(0x84, currentCount)));
        if (selectedCount == 1)
        {
            AssertBlocked(result, "AB_TP_TOPOLOGY_SELECTION_MISMATCH");
        }
        else
        {
            Assert.True(result.Succeeded);
            Assert.Equal("nt51950-ab-merge-1024k", result.Selection!.MapId);
        }
    }

    /// <summary>The same slot name cannot conceal changed bytes, an absent input or duplicate bindings.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CurrentPayloadMustMatchExactPrimaryIdentity(int mutation)
    {
        MetadataPlanDefinition plan = Plan("NT51950", 2);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        MetadataInspectionSnapshot primary = Inspect(plan, Tp(0x84, 2), Tp(0x84, 2));
        byte[] changed = Tp(0x84, 2);
        changed[0x10] ^= 1;
        FirmwareBinInspectionArtifact[]? artifacts = mutation switch
        {
            0 => Artifacts(changed, Tp(0x84, 2)),
            1 => [Artifacts(Tp(0x84, 2), Tp(0x84, 2))[0]],
            2 => [.. Artifacts(Tp(0x84, 2), Tp(0x84, 2)), new("tp-a-input", Tp(0x84, 2))],
            _ => null,
        };
        AssertBlocked(AssessFormat(family, "NT51950", Configuration(family), primary, Topology(2), artifacts),
            "AB_FORMAT_PRIMARY_INVALID");
    }

    private static FirmwareBinInspectionArtifact[] Artifacts(byte[] a, byte[] b)
    {
        return [new("tp-a-input", a), new("tp-b-input", b)];
    }

    private static void AssertBlocked(AbMergeFormatAdmissionResult result, string code)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.Selection);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    private static EventBufferFormatConfigurationState Configuration(
        FirmwareFamilyResolutionDefinition family, string? scope = null, IReadOnlyList<int>? values = null, string? alias = null)
    {
        FirmwareAbFormatPolicy policy = Assert.IsType<FirmwareAbFormatPolicy>(family.AbFormatPolicy);
        EventBufferFormatIdentity[] identities = [.. policy.Formats.Select(format => new EventBufferFormatIdentity(format.UniqueId, format.DisplayName))];
        EventBufferFormatDraftEntry[] draft = [.. policy.Formats.Select(format => new EventBufferFormatDraftEntry(
            format.UniqueId, alias, values ?? [.. format.DefaultRecognitionValues.Select(value => (int)value)]))];
        EventBufferFormatConfiguration configuration = EventBufferFormatConfigurationAdmission.Admit(scope ?? policy.ScopeId, identities, draft).Configuration!;
        return new(1, EventBufferFormatConfigurationStatus.Ready, configuration, ConfigHash, configuration, ConfigHash);
    }

    private static MetadataPlanDefinition Plan(string ic, int count)
    {
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.FindAbMergeRegistration(ic, ic == "NT51950" ? "nt51950-ab-merge-maps" : "nt51951-ab-merge-1024k")!;
        registration.TryCompile(null, Topology(count), out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return registration.CreateMetadataPlan(Assert.IsType<CompiledComposition>(composition));
    }

    private static MetadataInspectionSnapshot Inspect(MetadataPlanDefinition plan, byte[] a, byte[] b)
    {
        return FirmwareMetadataInspector.Inspect(plan.Resolve(new ResolutionToken("ab-format-test")),
            [new FirmwareArtifactPayload("tp-a-input", a), new FirmwareArtifactPayload("tp-b-input", b)]);
    }

    private static TopologySelection? Topology(int count)
    {
        return count == 0 ? null : new(count, "test", TopologySelectionSource.Requested, "test");
    }

    private static byte[] Tp(byte format, byte count = 2)
    {
        byte[] bytes = new byte[0x37000];
        bytes[0x22200] = 0x31;
        bytes[0x22201] = 0xCE;
        bytes[0x2220C] = format;
        bytes[0x36000] = 0x42;
        bytes[0x36001] = 0xBD;
        bytes[0x3600C] = 0x84;
        bytes[0x36017] = count;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(bytes, 0x36FFC);
        return bytes;
    }
}
