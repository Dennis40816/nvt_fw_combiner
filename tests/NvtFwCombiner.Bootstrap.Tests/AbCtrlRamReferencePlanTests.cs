using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Bank composition contracts, separate from final firmware/golden certification.</summary>
public sealed class AbCtrlRamReferencePlanTests
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

    /// <summary>Different A/B payloads survive local processing and only selected banks change.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SelectedBanksUseTheirOwnReferenceAndRestoreOnlyBAddresses(bool selectA, bool selectB)
    {
        byte[] original = ReadReference();
        original[0x43000] ^= 0x53;
        original[0x5FC00] ^= 0x6A;
        byte[] frozen = [.. original];
        V2RuntimeReferenceBankReplacePlan prepared = Prepare(original, Requests(selectA, selectB));
        original[0x3000] ^= 0xFF;
        var source = new byte[] { 0xA5 };
        var calls = new List<string>();
        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(prepared.Plan,
            prepared.CreateExecutionInput(new Dictionary<string, byte[]> { ["source"] = source }),
            (operation, input, _, _, _) =>
            {
                // This hook verifies orchestration only; real Combiner parity is tested separately.
                V2RuntimeReferenceBankReplaceBinding bank = prepared.Banks.Single(binding => binding.WorkspaceId == operation.TargetSpaceId);
                calls.Add(bank.BankInstanceId);
                Assert.Equal(0x40000, input.Length);
                Assert.Equal(frozen.AsSpan(0x7164, 12).ToArray(), input.Span.Slice(0x7164, 12).ToArray());
                Assert.Equal(frozen[checked((int)bank.OutputRange.Start) + 0x3000], input.Span[0x3000]);
                Assert.Equal(0xA5, input.Span[0x1FC00]);
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(input));
            }, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        byte[] expected = [.. frozen];
        if (selectA)
        {
            expected[0x1FC00] = 0xA5;
        }

        if (selectB)
        {
            expected[0x5FC00] = 0xA5;
        }

        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal(new byte[] { 0xA5 }, source);
        Assert.Equal(prepared.Banks.Select(static bank => bank.BankInstanceId), calls);
        Assert.Equal(frozen, prepared.Reference.Bytes.ToArray());
        Assert.DoesNotContain(prepared.Plan.OrderedOperations, static operation => operation.OperationId.Contains("dp-ab", StringComparison.Ordinal));
    }

    /// <summary>In-range wrong offsets are rejected, including when only A was selected.</summary>
    [Theory]
    [InlineData(0x7164, true)]
    [InlineData(0x7168, true)]
    [InlineData(0x716C, true)]
    [InlineData(0x7164, false)]
    [InlineData(0x7168, false)]
    [InlineData(0x716C, false)]
    public void EveryRelocatedAddressMustEqualA(int field, bool selectB)
    {
        byte[] reference = ReadReference();
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(reference.AsSpan(0x40000 + field, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x40000 + field, 4), value + 1);
        ArgumentException error = Assert.Throws<ArgumentException>(() => Prepare(reference, Requests(!selectB, selectB)));
        Assert.Contains("AB address mismatch", error.Message, StringComparison.Ordinal);
        Assert.Contains("expected A=0x", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Underflow and non-local ranges never reach an external processor.</summary>
    [Theory]
    [InlineData(0U, 0U)]
    [InlineData(0x40000U, 0x80000U)]
    [InlineData(uint.MaxValue, uint.MaxValue)]
    public void InvalidAddressArithmeticFailsPreparation(uint a, uint b)
    {
        byte[] reference = ReadReference();
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x7164, 4), a);
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x47164, 4), b);
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, Requests(true, true)));
    }

    /// <summary>Valid start addresses alone do not admit out-of-bounds CRC section reads.</summary>
    [Theory]
    [InlineData(0x7108)]
    [InlineData(0x7114)]
    [InlineData(0x47108)]
    [InlineData(0x47114)]
    public void OversizedSectionsFailPreparation(int field)
    {
        byte[] reference = ReadReference();
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(field, 4), uint.MaxValue);
        ArgumentException error = Assert.Throws<ArgumentException>(() => Prepare(reference, Requests(true, true)));
        Assert.Contains("section out of bounds", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Malformed selections and parent/source identities fail closed.</summary>
    [Fact]
    public void RejectsWrongLengthDuplicateBankAndDifferentSharedSources()
    {
        byte[] reference = ReadReference();
        V2RuntimeReferenceBankReplaceRequest[] requests = Requests(true, true);
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference[..^1], requests));
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, [requests[0], requests[0]]));
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, [requests[0] with { BankInstanceId = "other" }]));
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, []));
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference,
            [requests[0], new V2RuntimeReferenceBankReplaceRequest("b-bank", ReplaceRequest("different-source"))]));
        V2RuntimeReferenceBankReplacePlan prepared = Prepare(reference, requests);
        Assert.All(prepared.Banks, static bank => Assert.Equal("nt51929-ctrlram-replace-fw200-single", bank.LocalComposition.V2Details.ProfileId));
        Assert.NotEqual(prepared.Reference.Identity.Sha256, prepared.Banks[0].Reference.Identity.Sha256);
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, requests, prepared.Banks[0].LocalComposition));
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference,
            [new V2RuntimeReferenceBankReplaceRequest("a-bank", ReplaceRequest("ab-replace/a-bank/work"))]));
    }

    /// <summary>Version postconditions remain attached to the exact bank and captured local Reference.</summary>
    [Fact]
    public void RetainsNonemptyLocalValidationObligations()
    {
        V2RuntimeReferenceReplaceCompileRequest plain = ReplaceRequest("source");
        var edit = new V2RuntimeReferenceReplaceFirmwareVersionEdit(new ByteRange(0x1F200, 2), new ByteRange(0x1F203, 1),
            0x21, 0x32, "test.invalid-backup", "test.version-mismatch");
        var request = new V2RuntimeReferenceReplaceCompileRequest(plain.Bindings, plain.Mappings, edit);
        V2RuntimeReferenceBankReplacePlan prepared = Prepare(ReadReference(), [new("b-bank", request)]);
        V2RuntimeReferenceBankReplaceBinding bank = Assert.Single(prepared.Banks);
        CompiledFirmwareConfigBackupVersionValidation validation = Assert.IsType<CompiledFirmwareConfigBackupVersionValidation>(
            Assert.Single(bank.LocalComposition.V2Details.Provenance.ValidationRequirements));
        Assert.Equal(CompiledValidationStage.FinalOutput, validation.Stage);
        Assert.Equal("test.version-mismatch", validation.IssueCode);
        Assert.Equal("test.invalid-backup", validation.InvalidIssueCode);
        Assert.Equal(0x21, validation.FirmwareVersion);
        Assert.Equal(0x32, validation.FirmwareSubVersion);
        Assert.Equal(0x40000, bank.OutputRange.Start);
        Assert.Equal(prepared.Reference.Bytes.Slice(0x40000, 0x40000).ToArray(), bank.Reference.Bytes.ToArray());
        Assert.Contains(prepared.Plan.AddressSpaces, space => space.AddressSpaceId == bank.WorkspaceId);
    }

    /// <summary>The native tool's count and pointer are validated independently of AB address equality.</summary>
    [Theory]
    [InlineData(0x702B, 0U, 1)]
    [InlineData(0x702B, 2U, 1)]
    [InlineData(0x4702B, 0U, 1)]
    [InlineData(0x4702B, 2U, 1)]
    [InlineData(0x7038, 0x3FFFFU, 4)]
    [InlineData(0x47038, 0x3FFFFU, 4)]
    [InlineData(0x7038, 0x1F204U, 4)]
    [InlineData(0x47038, 0x5F200U, 4)]
    public void UnsafeNativeHeaderFailsBeforeAnyTool(int offset, uint value, int width)
    {
        byte[] reference = ReadReference();
        if (width == 1)
        {
            reference[offset] = checked((byte)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(offset, width), value);
        }

        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, Requests(true, true)));
    }

    /// <summary>A/B agreement cannot authorize a Backup outside the Single map's full envelope.</summary>
    [Theory]
    [InlineData(0x3FFFFU)]
    [InlineData(0x2E000U)]
    public void MatchingButUnsafeBackupDestinationFails(uint diff)
    {
        byte[] reference = ReadReference();
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x716C, 4), diff);
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x4716C, 4), diff + 0x40000);
        _ = Assert.Throws<ArgumentException>(() => Prepare(reference, Requests(true, true)));
    }

    /// <summary>Real 1.13 execution uses each bank's own content and audited local write authority.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RealCombinerMatchesIndependentLocalControls(bool selectA, bool selectB)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Real Combiner evidence requires Windows.");
        }

        byte[] original = ReadReference();
        original[0x61000] ^= 0x34;
        original[0x5F200] = 0x29;
        original[0x5F201] = 0xD6;
        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile("NT51929", out LegacyCombinerPostbuildProfile? profile));
        var selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]);
        LegacyCombinerPostbuildCommandPlan toolPlan = profile!.ResolvePlan(selection);
        V2RuntimeReferenceBankReplaceRequest[] requests = [.. Requests(selectA, selectB).Select(bank => bank with
        {
            Replace = new V2RuntimeReferenceReplaceCompileRequest(bank.Replace.Bindings, bank.Replace.Mappings,
                processorProtocolPlan: toolPlan.ProtocolPlan),
        })];
        V2RuntimeReferenceBankReplacePlan prepared = Prepare(original, requests);
        using TempWorkspace workspace = TempWorkspace.Create("nfc-ab-bank-real-tool");
        string toolRoot = Path.Combine(RepositoryPaths.FindRepositoryRoot(), "external-tools");
        using JsonDocument manifestJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json")));
        JsonElement m = manifestJson.RootElement;
        string S(string name)
        {
            return m.GetProperty(name).GetString()!;
        }

        var manifest = new ExternalCombinerToolManifest(S("schemaVersion"), S("toolBindingId"), S("toolId"), S("toolVersion"),
            S("displayName"), S("platform"), S("executableName"), S("sha256"), S("adapterId"), S("inputMode"),
            [.. m.GetProperty("argumentTemplate").EnumerateArray().Select(static item => item.GetString()!)],
            S("workingDirectoryPolicy"), m.GetProperty("timeoutSeconds").GetInt32(),
            [.. m.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(static item => item.GetString()!)]);
        var processor = new LegacyCombinerPostbuildProcessor(new ExternalCombinerToolRegistry([manifest]), toolRoot,
            workspace.PathFor("staging"), new SystemExternalProcessRunner());
        int calls = 0;
        async ValueTask<CompositionExternalProcessorResult> Run(CompositionOperation operation, ReadOnlyMemory<byte> input,
            IReadOnlyList<ExternalProcessorStagedSource> sources, IReadOnlyList<ExternalProcessorStagedArtifact> artifacts, CancellationToken ct)
        {
            calls++;
            ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
            ExternalProcessorResult result = await processor.TransformAsync(new ExternalProcessorRequest($"bank-{calls}",
                invocation.ProcessorId, invocation.ToolBindingId, input, invocation.AllowedWriteRanges, selection,
                sources, stagedArtifacts: artifacts, protocolPlan: invocation.ProtocolPlan), ct);
            Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
            return CompositionExternalProcessorResult.Success(result.OutputBytes);
        }

        CompositionExecutionResult output = await CompositionEngine.ExecuteAsync(prepared.Plan,
            prepared.CreateExecutionInput(new Dictionary<string, byte[]> { ["source"] = [0xA5] }), Run, TestContext.Current.CancellationToken);
        Assert.True(output.Status == CompositionExecutionStatus.Succeeded,
            string.Join("; ", output.Issues.Select(static issue => issue.Message)));
        Assert.Equal(prepared.Banks.Count, calls);
        byte[] expected = [.. original];
        foreach (V2RuntimeReferenceBankReplaceBinding bank in prepared.Banks)
        {
            byte[] control = bank.Reference.Bytes.ToArray();
            if (bank.BankInstanceId == "b-bank")
            {
                foreach (int field in new[] { 0x7164, 0x7168, 0x716C })
                {
                    uint address = BinaryPrimitives.ReadUInt32LittleEndian(control.AsSpan(field, 4));
                    BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(field, 4), checked(address - 0x40000));
                }
            }

            control[0x1FC00] = 0xA5;
            CompositionOperation tool = bank.LocalComposition.Plan.OrderedOperations.Single(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor);
            CompositionExternalProcessorResult processed = await Run(tool, control, [], [], TestContext.Current.CancellationToken);
            byte[] bytes = processed.OutputBytes.ToArray();
            if (bank.BankInstanceId == "b-bank")
            {
                foreach (int field in new[] { 0x7164, 0x7168, 0x716C })
                {
                    uint address = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(field, 4));
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field, 4), checked(address + 0x40000));
                }
            }

            bytes.CopyTo(expected, checked((int)bank.OutputRange.Start));
        }

        Assert.Equal(expected, output.OutputBytes.ToArray());
        Assert.Equal(original, prepared.Reference.Bytes.ToArray());
        ExportCandidateEvidence(selectA, selectB, original, output.OutputBytes.ToArray(), manifest.Sha256);
    }

    private static void ExportCandidateEvidence(bool selectA, bool selectB, byte[] reference, byte[] output,
        string toolSha256)
    {
        string? evidenceParent = Environment.GetEnvironmentVariable("NFC_AB_CTRLRAM_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(evidenceParent))
        {
            return;
        }

        string testAreaRoot = Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")
            ?? throw new InvalidOperationException("NFC_TEST_AREA_ROOT is required for private AB evidence export.");
        string root = Path.GetFullPath(testAreaRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string parent = Path.GetFullPath(evidenceParent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!parent.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AB evidence must remain inside NFC_TEST_AREA_ROOT.");
        }

        string caseName = selectA && selectB ? "both" : selectA ? "a-only" : "b-only";
        string caseDirectory = Path.Combine(parent, "nt51929-ab-ctrlram-" + caseName);
        _ = Directory.CreateDirectory(caseDirectory);
        File.WriteAllBytes(Path.Combine(caseDirectory, "reference-input.bin"), reference);
        File.WriteAllBytes(Path.Combine(caseDirectory, "candidate-output.bin"), output);
        File.WriteAllText(Path.Combine(caseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            status = "candidate only; local control uses the same Combiner, not an independent firmware golden",
            caseName,
            source = "AB Merge NT51929 t05-d06 output; B 0x61000 XOR 0x34, B 0x5F200..0x5F201 set to bytes 0x29, 0xD6",
            replacement = "single byte 0xA5 at selected bank NF start (A 0x1FC00, B 0x5FC00)",
            toolVersion = "1.13.0",
            toolSha256,
            referenceSha256 = Convert.ToHexString(SHA256.HashData(reference)).ToLowerInvariant(),
            outputSha256 = Convert.ToHexString(SHA256.HashData(output)).ToLowerInvariant(),
            outputLength = output.Length,
            changedRanges = ByteDiff.FindChangedRanges(reference, output),
        }, EvidenceJsonOptions));
    }

    private static V2RuntimeReferenceBankReplacePlan Prepare(byte[] reference,
        IReadOnlyList<V2RuntimeReferenceBankReplaceRequest> requests, CompiledComposition? layoutOverride = null)
    {
        TrustedProfileBundleCatalog ab = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51919-nt51929-nt51932-ab-merge", "5acf2fd4d0757d7b757bf7491ff2f268d07cf70a76588f528d36b616e1e5eed0");
        V2CompositionPlanCompileResult compiled = ab.Compile("nt51929-ab-merge", "0.4.0", "NT51929", ExperienceIds.AbMerge,
            0x80000, null, [], selectedInputSlotIds: ["dp-ab-input"]);
        Assert.True(compiled.IsCompiled);
        TrustedProfileBundleCatalog local = V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
            "nt51929-ctrlram-replace-candidate", "309f29e33a8fb672e92ed441d6633fab829bee3bd4c94a93fd842a7f3bb157d0");
        return V2CompositionPlanCompiler.PrepareAbRuntimeReferenceReplace(layoutOverride ?? compiled.CompiledComposition!,
            new FirmwareArtifactPayload("reference-base", reference), local, requests);
    }

    private static V2RuntimeReferenceBankReplaceRequest[] Requests(bool a, bool b)
    {
        var requests = new List<V2RuntimeReferenceBankReplaceRequest>();
        if (a)
        {
            requests.Add(new V2RuntimeReferenceBankReplaceRequest("a-bank", ReplaceRequest("source")));
        }

        if (b)
        {
            requests.Add(new V2RuntimeReferenceBankReplaceRequest("b-bank", ReplaceRequest("source")));
        }

        return [.. requests];
    }

    private static V2RuntimeReferenceReplaceCompileRequest ReplaceRequest(string source)
    {
        return new V2RuntimeReferenceReplaceCompileRequest(
            [new V2ExplicitMappingInputBinding("reference-base", "reference-base", 0x40000), new V2ExplicitMappingInputBinding(source, "ctrlram-source", 1)],
            [new ExplicitMapping("replace-nf", 100, ExplicitMappingOperationKind.ReplaceRange, source, new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage, new ByteRange(0x1FC00, 1), OverlapPolicy.Reject, alignment: 1, reason: "Replace selected NF byte.")]);
    }

    private static byte[] ReadReference()
    {
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("ab-merge", "NT51929", "expected-output", "t05-d06"));
    }
}
