using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    private static CompositionRunRequest CreateRequest(
        IReadOnlyList<InputArtifactBinding>? bindings = null,
        string? outputFileName = null,
        string? approvedPreviewToken = null)
    {
        return new CompositionRunRequest(
            "run-standard-synthetic",
            CreateStandardMergeCompiledComposition(),
            bindings ?? DefaultBindings(),
            outputFileName ?? "synthetic-standard-merge.bin",
            approvedPreviewToken: approvedPreviewToken);
    }

    private static CompiledComposition CreateStandardMergeCompiledComposition()
    {
        AddressSpace[] addressSpaces =
        [
            new("dp-input", 4, AddressSpaceMutability.Immutable),
            new("tp-input", 4, AddressSpaceMutability.Immutable),
            new("output-image", 8, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 8, 0xFF),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-dp",
                    100,
                    "dp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Copy synthetic DP input into the output DP range."),
                CompositionOperation.CopyRange(
                    "copy-tp",
                    200,
                    "tp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "Copy synthetic TP input into the output TP range."),
            ]);
        return CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "synthetic-standard-merge",
                "0.3.0",
                "NT-SYNTHETIC",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge),
            "synthetic-standard-merge.bin",
            allowOutputOverride: true);
    }

    private static CompositionRunRequest CreateScratchRequest(
        IReadOnlyList<InputArtifactBinding>? bindings = null)
    {
        AddressSpace[] addressSpaces =
        [
            new("v2-test-input", 1, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 4, 0),
                ImageInitialization.Blank("scratch", 4, 3),
            ],
            "output-image",
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
        CompiledComposition compiledComposition = CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "scratch-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "scratch",
                "general-merge",
                CompositionKind.Merge),
            "scratch.bin");
        return new CompositionRunRequest(
            "run-scratch",
            compiledComposition,
            bindings ??
            [
                new InputArtifactBinding(
                    "v2-test-input",
                    "v2-test-input",
                    "v2-test-input-artifact",
                    "v2-test-input.bin",
                    CompiledInputArtifactClass.TpFirmware),
            ],
            "scratch.bin");
    }

    private static CompositionRunRequest CreateInitializerFingerprintRequest(
        byte scratchFillByte,
        string outputSpaceId)
    {
        AddressSpace[] addressSpaces =
        [
            new("v2-test-input", 1, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Blank("output-image", 4, 0),
                ImageInitialization.Blank("scratch", 4, scratchFillByte),
            ],
            outputSpaceId,
            addressSpaces,
            []);
        CompiledComposition compiledComposition = CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "initializer-fingerprint-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "fingerprint",
                "general-merge",
                CompositionKind.Merge),
            "fingerprint.bin");
        return new CompositionRunRequest(
            "run-initializer-fingerprint",
            compiledComposition,
            [new InputArtifactBinding(
                "v2-test-input",
                "v2-test-input",
                "v2-test-input-artifact",
                "v2-test-input.bin",
                CompiledInputArtifactClass.TpFirmware)],
            "fingerprint.bin");
    }

    private static CompositionRunRequest CreateMultiReferenceReplaceRequest()
    {
        AddressSpace[] addressSpaces =
        [
            new("output-reference", 4, AddressSpaceMutability.Immutable),
            new("scratch-reference", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("scratch", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            [
                ImageInitialization.Reference("output-image", "output-reference", 4),
                ImageInitialization.Reference("scratch", "scratch-reference", 4),
            ],
            "output-image",
            addressSpaces,
            [
                CompositionOperation.FillRange(
                    "fill-output",
                    10,
                    "output-image",
                    new ByteRange(1, 1),
                    0x99,
                    OverlapPolicy.Reject,
                    "mutate selected output"),
                CompositionOperation.FillRange(
                    "fill-scratch",
                    20,
                    "scratch",
                    new ByteRange(1, 1),
                    0xEE,
                    OverlapPolicy.Reject,
                    "mutate non-output work buffer"),
            ]);
        CompiledComposition compiledComposition = CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "multi-reference-replace-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "multi-reference",
                "general-replace",
                CompositionKind.Replace),
            "multi-reference.bin",
            CompiledIcNumberPolicy.SingleSelector);
        return new CompositionRunRequest(
            "run-multi-reference-replace",
            compiledComposition,
            [
                new InputArtifactBinding(
                    "output-reference",
                    "output-reference",
                    "output-reference-artifact",
                    "output-reference.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "scratch-reference",
                    "scratch-reference",
                    "scratch-reference-artifact",
                    "scratch-reference.bin",
                    CompiledInputArtifactClass.ReferenceImage),
            ],
            "multi-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
    }

    private static CompositionRunRequest CreatePaddedInputRequest(byte? inputPaddingByte, string artifactId)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
            new("short-input", 4, AddressSpaceMutability.Immutable, inputPaddingByte),
        ];
        var identity = new TestCompiledCompositionIdentity(
            "padded-input-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "dp-replace",
            "dp-replace",
            CompositionKind.Replace);
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 4),
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
            ]);

        return new CompositionRunRequest(
            "run-padded-input",
            CreateCompiledComposition(
                plan,
                identity,
                "padded.bin",
                CompiledIcNumberPolicy.SingleSelector),
            [
                new InputArtifactBinding(
                    "reference-base",
                    "reference-base",
                    "padding-reference-artifact",
                    "reference-base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "short-input",
                    "short-safe",
                    artifactId,
                    "short-input.bin",
                    inputPaddingByte is null
                        ? CompiledInputArtifactClass.TpFirmware
                        : CompiledInputArtifactClass.DpFirmware),
            ],
            "padded.bin",
            icNumberSelection: new IcNumberSelection(
                IcNumberInputMode.SingleSelector,
                ["single"]));
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
        var identity = new TestCompiledCompositionIdentity(
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
            ]);

        return new CompositionRunRequest(
            "run-ctrlram-replace",
            CreateCompiledComposition(
                plan,
                identity,
                "ctrlram.bin",
                CompiledIcNumberPolicy.SingleSelector),
            [
                new InputArtifactBinding(
                    "reference-base", "reference-safe", "reference-artifact", "reference-base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "ctrlram-input", "ctrlram-safe", ctrlRamArtifactId, "ctrlram-input.bin",
                    inputOversizePolicy == InputOversizePolicy.TruncateWithWarning
                        ? CompiledInputArtifactClass.CtrlRamReplacement
                        : CompiledInputArtifactClass.TpFirmware),
            ],
            "ctrlram.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["SYNTHETIC"]));
    }

    private static CompositionRunRequest CreateDpReplaceRequest(string icNumber)
    {
        return new CompositionRunRequest(
            "run-dp-replace",
            CreateDpReplaceCompiledComposition(),
            CreateDpReplaceBindings(),
            "synthetic-dp-replace.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, [icNumber]));
    }

    private static CompiledComposition CreateDpReplaceCompiledComposition()
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 8, AddressSpaceMutability.Immutable),
            new("dp-replacement", 8, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            new("ldc-replacement", 8, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
            new("output-image", 8, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", 8),
            addressSpaces,
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp",
                    100,
                    "dp-replacement",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Replace synthetic DP declared partition."),
                CompositionOperation.ReplaceRange(
                    "replace-ld",
                    110,
                    "ldc-replacement",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(6, 2),
                    OverlapPolicy.Reject,
                    "Replace synthetic LD declared partition under DP Replace."),
            ]);
        return CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "synthetic-dp-replace",
                "0.5.0",
                "NT-SYNTHETIC",
                "dp-replace",
                "dp-replace",
                CompositionKind.Replace),
            "synthetic-dp-replace.bin",
            CompiledIcNumberPolicy.SingleSelector);
    }

    private static CompositionRunRequest CreateNumericReplaceRequest(string icCount)
    {
        var identity = new TestCompiledCompositionIdentity(
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
            []);
        return new CompositionRunRequest(
            "run-numeric-replace",
            CreateCompiledComposition(
                plan,
                identity,
                "numeric.bin",
                CompiledIcNumberPolicy.NumericSelector),
            [new InputArtifactBinding(
                "reference-base", "reference-safe", "reference-artifact", "reference-base.bin",
                CompiledInputArtifactClass.ReferenceImage)],
            "numeric.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.NumericSelector, [icCount]));
    }

    private static CompositionRunRequest CreateOverwriteRequest(string runId, byte firstFillByte)
    {
        AddressSpace[] addressSpaces =
        [
            new("v2-test-input", 1, AddressSpaceMutability.Immutable),
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
        CompiledComposition compiledComposition = CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "overwrite-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "overwrite",
                "general-merge",
                CompositionKind.Merge),
            "overwrite.bin");
        return new CompositionRunRequest(
            runId,
            compiledComposition,
            [new InputArtifactBinding(
                "v2-test-input",
                "v2-test-input",
                "v2-test-input-artifact",
                "v2-test-input.bin",
                CompiledInputArtifactClass.TpFirmware)],
            "overwrite.bin");
    }

    private static IReadOnlyList<InputArtifactBinding> CreateDpReplaceBindings()
    {
        return
        [
            new InputArtifactBinding(
                "reference-base", "reference-safe", "reference-artifact", "reference-base.bin",
                CompiledInputArtifactClass.ReferenceImage),
            new InputArtifactBinding(
                "dp-replacement", "dp-safe", "dp-artifact", "dp-replacement.bin",
                CompiledInputArtifactClass.DpFirmware),
            new InputArtifactBinding(
                "ldc-replacement", "ldc-safe", "ldc-artifact", "ldc-replacement.bin",
                CompiledInputArtifactClass.DpFirmware),
        ];
    }
}
