using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    private static CompositionRunService CreateService(out FakeOutputWriter writer)
    {
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["dp-artifact"] = [1, 2, 3, 4],
            ["tp-artifact"] = [9, 8, 7, 6],
        });
        writer = new FakeOutputWriter();
        return new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
    }

    private static CompositionRunRequest CreateRequest(
        IReadOnlyList<InputArtifactBinding>? bindings = null,
        string? outputFileName = null)
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return new CompositionRunRequest(
            "run-standard-synthetic",
            ToRunProfile(profile),
            compile.Plan!,
            bindings ?? DefaultBindings(),
            outputFileName ?? profile.DefaultOutputFileName);
    }

    private static CompositionRunRequest CreateScratchRequest()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-scratch",
                    10,
                    "scratch",
                    new ByteRange(2, 1),
                    "output-image",
                    new ByteRange(1, 1),
                    OverlapPolicy.Reject,
                    "copy scratch seed"),
            ]);
        return new CompositionRunRequest(
            "run-scratch",
            new CompositionRunProfile(
                "scratch-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "scratch",
                "general-merge",
                CompositionKind.Merge),
            plan,
            [new InputArtifactBinding("scratch", "scratch-safe", "scratch-artifact")],
            "scratch.bin");
    }

    private static CompositionRunRequest CreatePaddedInputRequest(byte? inputPaddingByte, string artifactId)
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("short-input", 4, AddressSpaceMutability.Immutable, inputPaddingByte),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-padded-input",
                    10,
                    "short-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy padded input"),
            ],
            new CompositionPlanProvenance(
                "padded-input-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge));

        return new CompositionRunRequest(
            "run-padded-input",
            new CompositionRunProfile(
                "padded-input-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge),
            plan,
            [new InputArtifactBinding("short-input", "short-safe", artifactId)],
            "padded.bin");
    }

    private static CompositionRunRequest CreateCtrlRamReplaceRequest(
        InputOversizePolicy inputOversizePolicy,
        string ctrlRamArtifactId)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("ctrlram-input", 2, AddressSpaceMutability.Immutable, inputOversizePolicy: inputOversizePolicy),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var provenance = new CompositionPlanProvenance(
            "ctrlram-replace-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace);
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.ReplaceRange(
                    "replace-ctrlram",
                    10,
                    "ctrlram-input",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(1, 2),
                    OverlapPolicy.Reject,
                    "replace ctrlram"),
            ],
            provenance);

        return new CompositionRunRequest(
            "run-ctrlram-replace",
            new CompositionRunProfile(
                provenance.ProfileId,
                provenance.ProfileVersion,
                provenance.IcId,
                provenance.ModeId,
                provenance.ExperienceId,
                provenance.CompositionKind,
                IcNumberInputMode.SingleSelector),
            plan,
            [
                new InputArtifactBinding("reference-base", "reference-safe", "reference-artifact"),
                new InputArtifactBinding("ctrlram-input", "ctrlram-safe", ctrlRamArtifactId),
            ],
            "ctrlram.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["SYNTHETIC"]));
    }

    private static CompositionRunRequest CreateDpReplaceRequest(string icNumber)
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticDpReplace;
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        return new CompositionRunRequest(
            "run-dp-replace",
            ToRunProfile(profile),
            compile.Plan!,
            CreateDpReplaceBindings(),
            profile.DefaultOutputFileName,
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, [icNumber]));
    }

    private static CompositionRunRequest CreateNumericReplaceRequest(string icCount)
    {
        var provenance = new CompositionPlanProvenance(
            "numeric-replace",
            "1.0.0",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            CompositionKind.Replace);
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [],
            provenance);
        var profile = new CompositionRunProfile(
            provenance.ProfileId,
            provenance.ProfileVersion,
            provenance.IcId,
            provenance.ModeId,
            provenance.ExperienceId,
            provenance.CompositionKind,
            IcNumberInputMode.NumericSelector);

        return new CompositionRunRequest(
            "run-numeric-replace",
            profile,
            plan,
            [new InputArtifactBinding("reference-base", "reference-safe", "reference-artifact")],
            "numeric.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.NumericSelector, [icCount]));
    }

    private static IReadOnlyList<InputArtifactBinding> CreateDpReplaceBindings()
    {
        return
        [
            new InputArtifactBinding("reference-base", "reference-safe", "reference-artifact"),
            new InputArtifactBinding("dp-replacement", "dp-safe", "dp-artifact"),
            new InputArtifactBinding("ld-replacement", "ld-safe", "ld-artifact"),
        ];
    }

    private static CompositionRunRequest CreateOverwriteRequest(string runId, byte firstFillByte)
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 1, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 1, 0),
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-first",
                    10,
                    "output-image",
                    new ByteRange(0, 1),
                    firstFillByte,
                    OverlapPolicy.Reject,
                    "first overwritten fill"),
                CompositionOperation.FillRange(
                    "fill-second",
                    20,
                    "output-image",
                    new ByteRange(0, 1),
                    0x22,
                    OverlapPolicy.ReplaceExisting,
                    "final fill"),
            ]);
        return new CompositionRunRequest(
            runId,
            new CompositionRunProfile(
                "overwrite-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "overwrite",
                "general-merge",
                CompositionKind.Merge),
            plan,
            [],
            "overwrite.bin");
    }

    private static CompositionRunRequest CreateExternalProcessorRequest()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(1, 1)]),
                    OverlapPolicy.Reject,
                    "run synthetic external processor"),
            ]);
        return new CompositionRunRequest(
            "run-external",
            new CompositionRunProfile(
                "external-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "external",
                "standard-merge",
                CompositionKind.Merge),
            plan,
            [],
            "external.bin");
    }

    private static CompositionRunRequest CreateStagedSourceExternalProcessorRequest(
        ByteRange? firmwareRange = null,
        string stagedSourceSpaceId = "ctrlram-input",
        IEnumerable<ExternalProcessorWriteRangeSection>? writeRangeSections = null)
    {
        ByteRange stagedFirmwareRange = firmwareRange ?? new ByteRange(1, 2);
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new(stagedSourceSpaceId, 2, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "processor-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(0, 4)],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                stagedSourceSpaceId,
                                new ByteRange(0, 2),
                                stagedFirmwareRange),
                        ],
                        allowedWriteRangeSections: writeRangeSections),
                    OverlapPolicy.ReplaceExisting,
                    "run synthetic postbuild"),
            ]);
        return new CompositionRunRequest(
            "run-staged-source",
            new CompositionRunProfile(
                "external-staged-source-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "external",
                "ctrlram-replace",
                CompositionKind.Replace,
                IcNumberInputMode.SingleSelector),
            plan,
            [
                new InputArtifactBinding("reference-base", "reference-base", "reference-artifact"),
                new InputArtifactBinding(stagedSourceSpaceId, stagedSourceSpaceId, "ctrlram-artifact"),
            ],
            "external-staged-source.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static IReadOnlyList<InputArtifactBinding> DefaultBindings()
    {
        return
        [
            new InputArtifactBinding("dp-input", "dp-input", "dp-artifact"),
            new InputArtifactBinding("tp-input", "tp-input", "tp-artifact"),
        ];
    }

    private static CompositionRunProfile ToRunProfile(CompositionProfileDefinition profile)
    {
        return new CompositionRunProfile(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.IcId,
            profile.ModeId,
            profile.ExperienceId,
            profile.CompositionKind,
            profile.IcNumberInputMode);
    }

    private sealed class FakeOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        internal string? FileName { get; private set; }

        internal byte[] OutputBytes { get; private set; } = [];

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            FileName = fileName;
            OutputBytes = outputBytes.ToArray();
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }

    private sealed class FakeExternalProcessor : IExternalProcessor
    {
        private readonly Func<ExternalProcessorRequest, ExternalProcessorResult> _transform;

        internal FakeExternalProcessor(Func<ExternalProcessorRequest, ExternalProcessorResult> transform)
        {
            _transform = transform;
        }

        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(_transform(request));
        }
    }
}
