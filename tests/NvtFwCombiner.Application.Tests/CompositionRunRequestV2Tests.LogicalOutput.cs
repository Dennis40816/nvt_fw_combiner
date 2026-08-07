using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunRequestV2Tests
{
    /// <summary>Logical-output compilation identity also references, rather than repeats, capability provenance.</summary>
    [Fact]
    public void CapabilityBoundLogicalOutputFingerprintAddsOnlyExactCompilationState()
    {
        const string capabilityFingerprint =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        CompiledComposition baseline = CreateLogicalOutputCandidate()
            .BindCapabilityFingerprint(capabilityFingerprint);
        CompiledComposition changedDefinitionProvenance =
            CreateLogicalOutputCandidate(
                    bundleContentHash: new string('e', 64),
                    profileEvidenceReference: "other-evidence")
                .BindCapabilityFingerprint(capabilityFingerprint);

        Assert.Equal(
            "eecd905e3d35caee8b93fc268c6a8853b686811186b771b7e98f2ae33553e879",
            baseline.CompilationFingerprint);
        Assert.Equal(
            baseline.CompilationFingerprint,
            changedDefinitionProvenance.CompilationFingerprint);
    }

    /// <summary>Verifies the narrow logical-output candidate is admitted without admitting map-bound V2 plan candidates.</summary>
    [Fact]
    public void RequestAcceptsExecutableCandidateLogicalOutputBindings()
    {
        CompiledComposition candidate = CreateLogicalOutputCandidate();
        var request = new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [CreateLogicalBinding("source-a", "source-a.bin"), CreateLogicalBinding("source-b", "source-b.bin")],
            "logical-output.bin");

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, request.CompiledComposition.Eligibility);
        _ = Assert.IsType<LogicalOutputV2CompilationContext>(request.CompiledComposition.V2Details.Provenance.Context);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, request.CompiledComposition.V2Details.Provenance.Promotion.Stage);
    }

    /// <summary>Verifies logical-output candidate admission remains unavailable below the executable-candidate promotion stage.</summary>
    [Fact]
    public void RequestRejectsLogicalOutputCandidateBelowExecutableCandidatePromotion()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            CreateLogicalOutputCandidate(CompiledProfilePromotionStage.Compilable),
            [CreateLogicalBinding("source-a", "source-a.bin"), CreateLogicalBinding("source-b", "source-b.bin")],
            "logical-output.bin"));

        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies logical-output admission does not widen to a different promotion stage.</summary>
    [Fact]
    public void RequestRejectsLogicalOutputPlanAtSupportedPromotion()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            CreateLogicalOutputCandidate(CompiledProfilePromotionStage.Supported),
            [CreateLogicalBinding("source-a", "source-a.bin"), CreateLogicalBinding("source-b", "source-b.bin")],
            "logical-output.bin"));

        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies logical-output bindings retain their compiled per-binding identity at the Application boundary.</summary>
    [Fact]
    public void RequestRejectsLogicalOutputBindingIdentityMismatch()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            CreateLogicalOutputCandidate(),
            [
                new InputArtifactBinding(
                    "source-a",
                    "swapped-binding",
                    "source-a-artifact",
                    "source-a.bin",
                    CompiledInputArtifactClass.Auxiliary),
                CreateLogicalBinding("source-b", "source-b.bin"),
            ],
            "logical-output.bin"));

        Assert.Contains("compiled input slot contract", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies logical-output runtime admission rejects incomplete, extra, or malformed concrete bindings.</summary>
    [Fact]
    public void RequestRejectsLogicalOutputBindingContractMismatch()
    {
        CompiledComposition candidate = CreateLogicalOutputCandidate();
        ArgumentException missing = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [CreateLogicalBinding("source-a", "source-a.bin")],
            "logical-output.bin"));
        Assert.Contains("exactly match", missing.Message, StringComparison.Ordinal);

        ArgumentException extra = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [
                CreateLogicalBinding("source-a", "source-a.bin"),
                CreateLogicalBinding("source-b", "source-b.bin"),
                CreateLogicalBinding("source-extra", "source-extra.bin"),
            ],
            "logical-output.bin"));
        Assert.Contains("exactly match", extra.Message, StringComparison.Ordinal);

        ArgumentException wrongClass = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [
                new InputArtifactBinding(
                    "source-a",
                    "source-a",
                    "source-a-artifact",
                    "source-a.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                CreateLogicalBinding("source-b", "source-b.bin"),
            ],
            "logical-output.bin"));
        Assert.Contains("compiled input slot contract", wrongClass.Message, StringComparison.Ordinal);

        ArgumentException missingName = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [
                new InputArtifactBinding(
                    "source-a",
                    "source-a",
                    "source-a-artifact",
                    originalFileName: null,
                    CompiledInputArtifactClass.Auxiliary),
                CreateLogicalBinding("source-b", "source-b.bin"),
            ],
            "logical-output.bin"));
        Assert.Contains("compiled input slot contract", missingName.Message, StringComparison.Ordinal);

        ArgumentException wrongExtension = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [CreateLogicalBinding("source-a", "source-a.txt"), CreateLogicalBinding("source-b", "source-b.bin")],
            "logical-output.bin"));
        Assert.Contains("unaccepted original file extension", wrongExtension.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies logical-output candidates execute through the shared engine and preserve preview-to-build parity.</summary>
    [Fact]
    public async Task LogicalOutputCandidateRunsThroughPreviewAndBuild()
    {
        CompiledComposition candidate = CreateLogicalOutputCandidate();
        var writer = new RecordingLogicalOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["source-a-artifact"] = [0xA1, 0xA2],
                ["source-b-artifact"] = [0xB1, 0xB2],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var request = new CompositionRunRequest(
            "logical-general-merge",
            candidate,
            [CreateLogicalBinding("source-a", "source-a.bin"), CreateLogicalBinding("source-b", "source-b.bin")],
            "logical-output.bin");

        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);
        CompositionRunResult build = await service.BuildAsync(
            request.WithApprovedPreviewToken(Assert.IsType<string>(preview.PreviewToken)),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal([0xA1, 0xA2, 0x00, 0x00, 0xB1, 0xB2], preview.OutputBytes.ToArray());
        Assert.Equal(preview.OutputBytes.ToArray(), build.OutputBytes.ToArray());
        Assert.True(writer.WasCalled);
    }

    /// <summary>Verifies a logical input size change fails that request before shared-engine execution.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task LogicalOutputCandidateRejectsSourceBytesThatDifferFromCompiledBindingLength(int sourceALength)
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["source-a-artifact"] = [.. Enumerable.Repeat((byte)0xA1, sourceALength)],
                ["source-b-artifact"] = [0xB1, 0xB2],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        var request = new CompositionRunRequest(
            "logical-general-merge",
            CreateLogicalOutputCandidate(),
            [CreateLogicalBinding("source-a", "source-a.bin"), CreateLogicalBinding("source-b", "source-b.bin")],
            "logical-output.bin");

        CompositionRunResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Contains(result.Report.Issues, issue =>
            issue.Code == CompositionIssueCodes.InputAddressSpaceLengthMismatch &&
            issue.OperationId == "source-a");
    }

    private static CompiledComposition CreateLogicalOutputCandidate(
        CompiledProfilePromotionStage stage = CompiledProfilePromotionStage.ExecutableCandidate,
        string bundleContentHash =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        string profileEvidenceReference = "logical-profile-evidence")
    {
        var plan = new CompositionPlan(
            [ImageInitialization.Blank("output-image", 6, 0)],
            "output-image",
            [
                new AddressSpace("source-a", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("source-b", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 6, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-a",
                    10,
                    "source-a",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "copy first logical input"),
                CompositionOperation.CopyRange(
                    "copy-b",
                    20,
                    "source-b",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(4, 2),
                    OverlapPolicy.Reject,
                    "copy second logical input"),
            ]);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "logical-bundle-v2",
                "1.0.0",
                bundleContentHash,
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "logical-profile-entry",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            new LogicalOutputV2CompilationContext(
                "synthetic-family",
                "1.0.0",
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                "NT-SYNTHETIC"),
            new CompiledProfilePromotion(stage, []),
            [profileEvidenceReference],
            [],
            []);
        var details = new V2CompiledCompositionDetails(
            "logical-general-merge",
            "1.0.0",
            ExperienceIds.GeneralMerge,
            CompositionKind.Merge,
            provenance,
            new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
                        "source-slot",
                        "source",
                        CompiledInputArtifactClass.Auxiliary,
                        required: true,
                        CompiledInputSlotCardinality.OneOrMore,
                        [".bin"],
                        new CompiledBoundedInputLengthRequirement(1, int.MaxValue),
                        new CompiledNoInputNormalization())],
                [
                    new CompiledInputSpaceBinding("source-a", "source-slot", CompiledInputInstancePolicy.PerBinding),
                    new CompiledInputSpaceBinding("source-b", "source-slot", CompiledInputInstancePolicy.PerBinding),
                ]),
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement(
                "logical-output.bin",
                allowOverride: false,
                CompiledOutputInvalidCharacterPolicy.Reject,
                []));
        return CompiledComposition.CreateV2(plan, details);
    }

    private static InputArtifactBinding CreateLogicalBinding(string bindingId, string originalFileName)
    {
        return new InputArtifactBinding(
            bindingId,
            bindingId,
            $"{bindingId}-artifact",
            originalFileName,
            CompiledInputArtifactClass.Auxiliary);
    }

    private sealed class RecordingLogicalOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }
}
