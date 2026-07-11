using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
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

    private static CompositionRunRequest CreateScratchRequest(
        IReadOnlyList<InputArtifactBinding>? bindings = null)
    {
        AddressSpace[] addressSpaces =
        [
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
            bindings ?? [],
            "scratch.bin");
    }

    private static CompositionRunRequest CreateInitializerFingerprintRequest(
        byte scratchFillByte,
        string outputSpaceId)
    {
        AddressSpace[] addressSpaces =
        [
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
        return new CompositionRunRequest(
            "run-initializer-fingerprint",
            new CompositionRunProfile(
                "initializer-fingerprint-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "fingerprint",
                "general-merge",
                CompositionKind.Merge),
            plan,
            [],
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
        return new CompositionRunRequest(
            "run-multi-reference-replace",
            new CompositionRunProfile(
                "multi-reference-replace-profile",
                "1.0.0",
                "NT-SYNTHETIC",
                "multi-reference",
                "general-replace",
                CompositionKind.Replace,
                IcNumberInputMode.SingleSelector),
            plan,
            [
                new InputArtifactBinding(
                    "output-reference",
                    "output-reference",
                    "output-reference-artifact"),
                new InputArtifactBinding(
                    "scratch-reference",
                    "scratch-reference",
                    "scratch-reference-artifact"),
            ],
            "multi-reference.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
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

    private static IReadOnlyList<InputArtifactBinding> CreateDpReplaceBindings()
    {
        return
        [
            new InputArtifactBinding("reference-base", "reference-safe", "reference-artifact"),
            new InputArtifactBinding("dp-replacement", "dp-safe", "dp-artifact"),
            new InputArtifactBinding("ld-replacement", "ld-safe", "ld-artifact"),
        ];
    }
}
