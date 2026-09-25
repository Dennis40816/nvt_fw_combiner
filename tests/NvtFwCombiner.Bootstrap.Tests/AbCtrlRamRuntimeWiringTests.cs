using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Candidate catalog admission and the real Application Preview/Build boundary.</summary>
public sealed class AbCtrlRamRuntimeWiringTests
{
    /// <summary>The shared runner executes selected banks and publishes once after an approved preview.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CandidateRouteRunsPreviewAndBuildWithRealCombiner(bool a, bool b)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        byte[] original = Reference();
        original[0x61000] ^= 0x34;
        original[0x5F200] = 0x29;
        original[0x5F201] = 0xD6;
        CompiledComposition compiled = Compile(original, a, b, editVersions: true);
        ResolvedCapability capability = Resolve(compiled);
        var writer = new Writer();
        using TempWorkspace workspace = TempWorkspace.Create("ab-runtime-wiring");
        var processor = new CountingProcessor(RealProcessor(workspace));
        CompositionRunService service = Service(original, processor, writer);
        CompositionRunRequest request = Request(capability);
        CompositionRunResult preview = await service.PreviewAsync(request, TestContext.Current.CancellationToken);
        AssertSucceeded(preview);
        Assert.Equal(0, writer.Count);
        int banks = (a ? 1 : 0) + (b ? 1 : 0);
        Assert.Equal(banks, processor.Calls);
        CompositionRunResult build = await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), TestContext.Current.CancellationToken);
        AssertSucceeded(build);
        Assert.Equal(1, writer.Count);
        Assert.Equal(banks * 2, processor.Calls);
        Assert.Equal(preview.OutputBytes.ToArray(), build.OutputBytes.ToArray());
        Assert.Equal(build.OutputBytes.ToArray(), writer.Bytes);
        Assert.Equal(a ? 0xA5 : original[0x1FC00], build.OutputBytes.Span[0x1FC00]);
        Assert.Equal(b ? 0xA5 : original[0x5FC00], build.OutputBytes.Span[0x5FC00]);
        if (!a)
        {
            Assert.Equal(original[..0x40000], build.OutputBytes[..0x40000].ToArray());
        }

        if (!b)
        {
            Assert.Equal(original[0x40000..], build.OutputBytes[0x40000..].ToArray());
        }

        // The existing core parity test compares entire outputs to independent local tool controls.
        // This boundary additionally proves that the real runner uses the captured plan and one publisher.
        Assert.Equal(original.AsSpan(0x7164, 12).ToArray(), build.OutputBytes.Slice(0x7164, 12).ToArray());
        Assert.Equal(original.AsSpan(0x47164, 12).ToArray(), build.OutputBytes.Slice(0x47164, 12).ToArray());
        foreach (int start in new[] { a ? 0 : -1, b ? 0x40000 : -1 }.Where(static start => start >= 0))
        {
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(build.OutputBytes.Span.Slice(start, 0x40000), out FirmwareConfigMetadata metadata));
            Assert.Equal(start == 0 ? 0x21 : 0x29, metadata.FirmwareVersion);
            Assert.Equal(start == 0 ? 0x32 : 0x41, metadata.FirmwareSubVersion);
        }
        Assert.Equal(CapabilityPublicationStatus.Candidate, capability.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, capability.Evidence.Value);
    }

    /// <summary>A Reference changed after compilation fails before the native tool and publication.</summary>
    [Fact]
    public async Task ChangedReferenceIsRejectedBeforeTool()
    {
        byte[] original = Reference();
        ResolvedCapability capability = Resolve(Compile(original, true, true));
        original[0x61000] ^= 1;
        var processor = new CountingProcessor(null);
        var writer = new Writer();
        CompositionRunResult result = await Service(original, processor, writer).PreviewAsync(Request(capability), TestContext.Current.CancellationToken);
        Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Report.Issues, static issue => issue.Code == "input.bank-reference.identity-mismatch");
        Assert.Equal(0, processor.Calls);
        Assert.Equal(0, writer.Count);
    }

    /// <summary>A failed local tool cannot publish a partly updated AB image.</summary>
    [Fact]
    public async Task ToolFailureNeverPublishes()
    {
        byte[] original = Reference();
        var processor = new CountingProcessor(null);
        var writer = new Writer();
        CompositionRunResult result = await Service(original, processor, writer).PreviewAsync(Request(Resolve(Compile(original, true, true))), TestContext.Current.CancellationToken);
        Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Report.Issues, static issue => issue.Code == "test.tool.failure");
        Assert.Equal(1, processor.Calls);
        Assert.Equal(0, writer.Count);
    }

    /// <summary>Even after A succeeds, a B tool or version postcondition failure discards the whole Build.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondBankFailurePreventsCommitAfterSuccessfulPreview(bool corruptBackup)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        byte[] original = Reference();
        using TempWorkspace workspace = TempWorkspace.Create("ab-runtime-failure");
        var processor = new CountingProcessor(RealProcessor(workspace)) { FailOnCall = 4, CorruptBackup = corruptBackup };
        var writer = new Writer();
        CompositionRunService service = Service(original, processor, writer);
        CompositionRunRequest request = Request(Resolve(Compile(original, true, true, editVersions: true)));
        CompositionRunResult preview = await service.PreviewAsync(request, TestContext.Current.CancellationToken);
        AssertSucceeded(preview);
        CompositionRunResult build = await service.BuildAsync(request.WithApprovedPreviewToken(preview.PreviewToken!), TestContext.Current.CancellationToken);
        Assert.NotEqual(CompositionExecutionStatus.Succeeded, build.Status);
        Assert.Contains(build.Report.Issues, issue => issue.Code == (corruptBackup ? "test.version-invalid" : "test.tool.failure"));
        Assert.Equal(4, processor.Calls);
        Assert.Equal(0, writer.Count);
        Assert.Empty(writer.Bytes);
    }

    /// <summary>Selections change run identity but never redefine the admitted composite capability.</summary>
    [Fact]
    public void SelectionAndReferenceAreBoundSeparatelyFromDefinition()
    {
        byte[] reference = Reference();
        CompiledComposition a = Compile(reference, true, false);
        CompiledComposition b = Compile(reference, false, true);
        CompiledComposition both = Compile(reference, true, true);
        reference[0x61000] ^= 1;
        CompiledComposition changed = Compile(reference, true, true);
        Assert.Equal(4, new[] { a, b, both, changed }.Select(static item => item.CompilationFingerprint).Distinct().Count());
        _ = Assert.Single(new[] { a, b, both, changed }.Select(static item => Resolve(item).CapabilityFingerprint).Distinct());
        RuntimeReferenceCompilationProof proof = Proof(a);
        _ = Assert.Throws<ArgumentException>(() => proof.ValidateAndGetSemanticBindings(b));
        _ = Assert.Throws<ArgumentException>(() => proof.BindCapabilityCompilation(a, b));
        _ = Assert.Throws<ArgumentException>(() => RuntimeReferenceCompilationProof.CreateBankReplace(both,
            new Dictionary<string, LegacyCombinerPostbuildCommandPlan> { ["a-bank"] = ToolPlan() }));
        _ = Assert.Throws<ArgumentException>(() => RequestWithoutCapability(a));
    }

    /// <summary>Proof must retain all parent obligations and the exact checked outer plan.</summary>
    [Fact]
    public void MissingValidationOrSubstitutedPlanCannotReceiveProof()
    {
        CompiledComposition original = Compile(Reference(), true, true, editVersions: true);
        CompiledComposition stripped = WithValidations(original, []);
        _ = Assert.Throws<ArgumentException>(() => Proof(stripped));
        var changedPlan = new CompositionPlan(original.Plan.Initializations, original.Plan.OutputSpaceId,
            original.Plan.AddressSpaces, original.Plan.OrderedOperations.Where(static operation => operation.OperationId != "b-bank/publish"));
        _ = Assert.Throws<ArgumentException>(() => Proof(CompiledComposition.CreateV2(changedPlan, original.V2Details)));
        _ = Assert.Throws<ArgumentException>(() => Resolve(original, CapabilityPublicationStatus.Supported));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest("injected-bank", Resolve(original).CompiledComposition,
            [.. Bindings(), new InputArtifactBinding("ab-replace/b-bank/reference", "forged", "forged", "forged.bin", CompiledInputArtifactClass.ReferenceImage)],
            "ab.bin", icNumberSelection: Selection(), outputFileNameIsOverride: true, resolvedCapability: Resolve(original)));
    }

    /// <summary>Existing input/final validators receive their bank's Reference and restored local view.</summary>
    [Theory]
    [InlineData("uniform")]
    [InlineData("authority")]
    [InlineData("expected-address")]
    public async Task ParentValidationIsForwardedAtOriginalStage(string rule)
    {
        byte[] reference = Reference();
        reference[0x3000] = 1;
        reference[0x3001] = 2;
        reference[0x43000] = reference[0x43001] = 0;
        // Keep A/B references distinct while exercising the local immutable Reference validator.
        reference[0x6F100] ^= 0x51;
        CompiledValidationRequirement validation = rule switch
        {
            "uniform" => CompiledValidationRequirements.RejectUniformInputRanges("uniform", CompiledValidationSeverity.Error,
                "test.uniform", "reference-base", [new ByteRange(0x3000, 2)]),
            "authority" => CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority("authority", "test.authority",
                "test.inactive", "reference-base", new ByteRange(0x2E000, 0x1000), 0x1000),
            _ => CompiledValidationRequirements.FirmwareConfigBackupExpectedAddress("expected", "test.expected", 0x2D000),
        };
        // Synthetic validation obligations test forwarding; they do not add a production/profile admission.
        CompiledComposition compiled = Compile(reference, false, true, extraTestValidation: validation);
        var processor = new CountingProcessor(new PassthroughProcessor());
        var writer = new Writer();
        CompositionRunResult result = await Service(reference, processor, writer).PreviewAsync(Request(Resolve(compiled)), TestContext.Current.CancellationToken);
        if (rule == "uniform")
        {
            Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
            Assert.Contains(result.Report.Issues, static issue => issue.Code == "test.uniform");
            Assert.Equal(0, processor.Calls);
        }
        else
        {
            AssertSucceeded(result);
            Assert.Equal(1, processor.Calls);
            if (rule == "expected-address")
            {
                Assert.Contains(result.Report.Issues, static issue => issue.Code == "test.expected" && issue.Severity == "warning");
            }
        }

        Assert.Equal(0, writer.Count);
    }

    internal static CompiledComposition Compile(byte[] reference, bool a, bool b, bool editVersions = false,
        CompiledValidationRequirement? extraTestValidation = null)
    {
        TrustedProfileBundleCatalog ab = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51919-nt51929-nt51932-ab-merge", "ece8e9ee7a81b3f00ce04bd7c1aa053acde26835d75c3042bf1a86302d8de793");
        CompiledComposition layout = ab.Compile("nt51929-ab-merge", "0.4.0", "NT51929", ExperienceIds.AbMerge,
            0x80000, null, [], selectedInputSlotIds: ["dp-ab-input"]).CompiledComposition!;
        TrustedProfileBundleCatalog local = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51929-ctrlram-replace-candidate", "309f29e33a8fb672e92ed441d6633fab829bee3bd4c94a93fd842a7f3bb157d0");
        BankReferenceReplaceDefinition definition = ab.CreateBankReplaceDefinition(local, "NT51929",
            "nt51929-ab-merge", "0.4.0", "nt51929-ab-merge-512k",
            "nt51929-ctrlram-replace-fw200-single", "0.3.0", "nt51929-ctrlram-fw200-single-full-flash");
        LegacyCombinerPostbuildCommandPlan plan = ToolPlan();
        ByteRange[] staged = [.. LegacyCombinerPostbuildPlanCompiler.GetStagedFileBlocks(plan).Select(static block => block.FirmwareRange)];
        var request = new V2RuntimeReferenceReplaceCompileRequest(
            [new("reference-base", "reference-base", 0x40000), new("source", "ctrlram-source", 1)],
            [new ExplicitMapping("replace-nf", 100, ExplicitMappingOperationKind.ReplaceRange, "source", new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage, new ByteRange(0x1FC00, 1), OverlapPolicy.Reject, alignment: 1, reason: "Replace NF byte.")],
            postbuildWriteRangeSections: LegacyCombinerPostbuildPlanCompiler.GetAllowedWriteRangeSectionsForStagedSources(plan, 0x40000, staged, staged),
            processorProtocolPlan: plan.ProtocolPlan);
        var banks = new List<V2RuntimeReferenceBankReplaceRequest>();
        if (a)
        {
            banks.Add(new("a-bank", WithVersion(0x21, 0x32)));
        }

        if (b)
        {
            banks.Add(new("b-bank", WithVersion(0x29, 0x41)));
        }

        V2RuntimeReferenceBankReplacePlan prepared = V2CompositionPlanCompiler.PrepareAbRuntimeReferenceReplace(
            layout, new FirmwareArtifactPayload("reference-base", reference), definition, 1, local, banks);
        if (extraTestValidation is not null)
        {
            prepared = new V2RuntimeReferenceBankReplacePlan(prepared.AbLayout, prepared.Reference,
                prepared.Definition, prepared.Plan,
                prepared.Banks.Select(bank => bank with { LocalComposition = WithValidations(bank.LocalComposition, [extraTestValidation]) }));
        }

        return V2CompositionPlanCompiler.CompileAbRuntimeReferenceReplace(prepared);

        V2RuntimeReferenceReplaceCompileRequest WithVersion(byte version, byte subVersion)
        {
            return new V2RuntimeReferenceReplaceCompileRequest(request.Bindings, request.Mappings,
                editVersions ? new V2RuntimeReferenceReplaceFirmwareVersionEdit(new ByteRange(0x1F200, 2), new ByteRange(0x1F200 + FirmwareConfigLayout.FirmwareSubVersionOffset, 1),
                    version, subVersion, "test.version-invalid", "test.version-mismatch") : null,
                postbuildWriteRangeSections: request.PostbuildWriteRangeSections, processorProtocolPlan: request.ProcessorProtocolPlan);
        }
    }

    private static CompiledComposition WithValidations(CompiledComposition original, IEnumerable<CompiledValidationRequirement> validations)
    {
        V2CompiledCompositionDetails d = original.V2Details;
        V2CompilationProvenance p = d.Provenance;
        var provenance = new V2CompilationProvenance(p.Bundle, p.ProfileEntry, p.Context, p.Promotion, p.ProfileEvidenceRefs,
            validations, p.RequiredCapabilities);
        return CompiledComposition.CreateV2(original.Plan, new V2CompiledCompositionDetails(d.ProfileId, d.ProfileVersion,
            d.ExperienceId, d.CompositionKind, provenance, d.InputContract, d.RegionAccessContract, d.OutputNamingRequirement, d.IcNumberInputMode));
    }

    internal static RuntimeReferenceCompilationProof Proof(CompiledComposition composition)
    {
        var context = (RuntimeReferenceBankReplaceV2CompilationContext)composition.V2Details.Provenance.Context;
        return RuntimeReferenceCompilationProof.CreateBankReplace(composition, context.Banks.ToDictionary(static bank => bank.BankId, static _ => ToolPlan()));
    }

    private static ResolvedCapability Resolve(CompiledComposition composition, CapabilityPublicationStatus publication = CapabilityPublicationStatus.Candidate)
    {
        var context = (RuntimeReferenceBankReplaceV2CompilationContext)composition.V2Details.Provenance.Context;
        RuntimeReferenceCompilationProof proof = Proof(composition);
        var identity = new CapabilityRouteIdentity("NT51929", ExperienceIds.CtrlRamReplace, "single", context.ResolvedMap.ImageMap.MapId);
        var contract = new CanonicalCapabilityCompilationContract(context.Definition.DefinitionId, context.Definition.Version,
            context.Definition.ContentHash, [identity.MapVariant], BankReferenceReplaceDefinition.CompilerSemanticId, proof.ValidateAndGetSemanticBindings(composition));
        string fingerprint = CapabilityDefinitionFingerprint.Compute(identity, contract.ProfileId, contract.ProfileVersion,
            contract.TrustedDefinitionSha256, contract.AllowedMapVariantIds, contract.CompilerSemanticId, contract.SemanticBindingIds);
        var definition = new CanonicalDynamicCapabilityDefinition(identity, fingerprint, contract,
            Decision(CapabilityAuthoringAvailability.Available), Decision(publication), Decision(CapabilityEvidenceStatus.ContractOnly));
        var catalog = new CanonicalCapabilityCatalog(new Source(definition));
        CapabilityCatalogReloadResult load = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, string.Join("; ", load.Issues.Select(static issue => issue.Message)));
        return catalog.ResolveDynamicRoute(identity.RouteId).Route!.BindCompilation(composition, runtimeReferenceProof: proof);

        PinnedCapabilityDecision<T> Decision<T>(T value) where T : struct, Enum
        {
            return new PinnedCapabilityDecision<T>("test-" + typeof(T).Name, identity.RouteId, fingerprint, value, "runtime-wiring-test");
        }
    }

    private static CompositionRunRequest Request(ResolvedCapability capability)
    {
        return new CompositionRunRequest("ab-runtime", capability.CompiledComposition, Bindings(), "ab-replaced.bin",
            icNumberSelection: Selection(), outputFileNameIsOverride: true, resolvedCapability: capability);
    }

    private static CompositionRunRequest RequestWithoutCapability(CompiledComposition composition)
    {
        return new CompositionRunRequest("ab-runtime", composition, Bindings(), "ab-replaced.bin", icNumberSelection: Selection(), outputFileNameIsOverride: true);
    }

    private static InputArtifactBinding[] Bindings()
    {
        return [
            new("reference-base", "reference-base", "reference", "reference.bin", CompiledInputArtifactClass.ReferenceImage),
            new("source", "source", "source", "source.bin", CompiledInputArtifactClass.CtrlRamReplacement),
        ];
    }

    private static CompositionRunService Service(byte[] reference, IExternalProcessor processor, Writer writer)
    {
        return new CompositionRunService(new FakeArtifactReader(new Dictionary<string, byte[]> { ["reference"] = reference, ["source"] = [0xA5] }),
            new FakeClock(Enumerable.Repeat(new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero), 20)), writer, processor);
    }

    private static LegacyCombinerPostbuildCommandPlan ToolPlan()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile("NT51929", out LegacyCombinerPostbuildProfile? profile));
        return profile!.ResolvePlan(Selection());
    }

    private static IcNumberSelection Selection()
    {
        return new(IcNumberInputMode.SingleSelector, ["single"]);
    }
    private static byte[] Reference()
    {
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51929", "expected-output", "t05-d06"));
    }

    private static LegacyCombinerPostbuildProcessor RealProcessor(TempWorkspace workspace)
    {
        string root = Path.Combine(RepositoryPaths.FindRepositoryRoot(), "external-tools");
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "legacy-combiner", "1.13.0", "manifest.json")));
        JsonElement m = json.RootElement;
        string S(string name)
        {
            return m.GetProperty(name).GetString()!;
        }
        var manifest = new ExternalCombinerToolManifest(S("schemaVersion"), S("toolBindingId"), S("toolId"), S("toolVersion"),
            S("displayName"), S("platform"), S("executableName"), S("sha256"), S("adapterId"), S("inputMode"),
            [.. m.GetProperty("argumentTemplate").EnumerateArray().Select(static item => item.GetString()!)],
            S("workingDirectoryPolicy"), m.GetProperty("timeoutSeconds").GetInt32(),
            [.. m.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(static item => item.GetString()!)]);
        return new LegacyCombinerPostbuildProcessor(new ExternalCombinerToolRegistry([manifest]), root, workspace.PathFor("staging"), new SystemExternalProcessRunner());
    }

    private static void AssertSucceeded(CompositionRunResult result)
    {
        Assert.True(result.Status == CompositionExecutionStatus.Succeeded, string.Join("; ", result.Report.Issues.Select(static issue => issue.Code + ": " + issue.Message)));
    }

    private sealed class Source(CanonicalDynamicCapabilityDefinition definition) : ICanonicalCapabilityCatalogSource
    {
        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            return CapabilityCatalogLoadResult.Success(new CanonicalCapabilityCatalogCandidate("test", "1", new string('b', 64), [], [definition]));
        }
    }

    private sealed class CountingProcessor(IExternalProcessor? inner) : IExternalProcessor
    {
        public int Calls { get; private set; }
        public int? FailOnCall { get; init; }
        public bool CorruptBackup { get; init; }
        public async ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (inner is null || (Calls == FailOnCall && !CorruptBackup))
            {
                return ExternalProcessorResult.Failed([new CompositionIssue("test.tool.failure", "Deliberate tool failure.")]);
            }

            ExternalProcessorResult result = await inner.TransformAsync(request, cancellationToken);
            if (Calls == FailOnCall && CorruptBackup && result.Succeeded)
            {
                byte[] output = result.OutputBytes.ToArray();
                Assert.True(FirmwareConfigMetadataReader.TryReadBackup(output, out FirmwareConfigMetadata metadata));
                output[checked((int)metadata.StructureStart) + FirmwareConfigLayout.FirmwareVersionBarOffset] ^= 1;
                return ExternalProcessorResult.Success(output, result.ChangedRanges, result.ExecutedCommands);
            }

            return result;
        }
    }

    private sealed class PassthroughProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, [], []));
        }
    }

    private sealed class Writer : ICompositionOutputWriter
    {
        public int Count { get; private set; }
        public byte[] Bytes { get; private set; } = [];
        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(string fileName, ReadOnlyMemory<byte> outputBytes, CancellationToken cancellationToken)
        {
            Count++;
            Bytes = outputBytes.ToArray();
            return ValueTask.FromResult(CompositionOutputCommitReceipt.CreateLoose("committed:" + fileName, fileName, outputBytes.Span));
        }
    }
}
