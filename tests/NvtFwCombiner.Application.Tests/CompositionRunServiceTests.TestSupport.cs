using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
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

    private static IReadOnlyList<InputArtifactBinding> DefaultBindings()
    {
        return
        [
            new InputArtifactBinding(
                "dp-input",
                "dp-input",
                "dp-artifact",
                "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-input",
                "tp-input",
                "tp-artifact",
                "tp-input.bin",
                CompiledInputArtifactClass.TpFirmware),
        ];
    }

    private static (CompositionRunService Service, CompositionRunRequest Request, FakeOutputWriter Writer)
        CreateTpMaximumRun(int sourceLength)
    {
        var source = new AddressSpace(
            "tp-input",
            4,
            AddressSpaceMutability.Immutable,
            inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0xFF),
            [source, new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable)],
            [CompositionOperation.CopyRange(
                "copy-tp",
                100,
                source.AddressSpaceId,
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.Reject,
                "Copy the accepted TP source view.")]);
        CompiledComposition composition = CreateCompiledComposition(
            plan,
            new TestCompiledCompositionIdentity(
                "synthetic-tp-maximum",
                "1.0.0",
                "NT-SYNTHETIC",
                ExperienceIds.StandardMerge,
                ExperienceIds.StandardMerge,
                CompositionKind.Merge),
            "synthetic-tp-maximum.bin",
            inputLengthRequirement: new CompiledSourceViewCoverageInputLengthRequirement(
                maximumBytes: InputLengthPolicyLimits.MaximumTpFirmwareBytes));
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["tp-artifact"] = new byte[sourceLength],
        });
        var writer = new FakeOutputWriter();
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var request = new CompositionRunRequest(
            "run-tp-maximum",
            composition,
            [new InputArtifactBinding(
                source.AddressSpaceId,
                "tp-input",
                "tp-artifact",
                "tp.bin",
                CompiledInputArtifactClass.TpFirmware)],
            "synthetic-tp-maximum.bin");
        return (service, request, writer);
    }

    private static CompiledComposition CreateCompiledComposition(
        CompositionPlan plan,
        TestCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        IcNumberInputMode? icNumberInputMode = null,
        IReadOnlyList<CompiledValidationRequirement>? validationRequirements = null,
        bool allowOutputOverride = false,
        CompiledInputLengthRequirement? inputLengthRequirement = null)
    {
        return CompiledCompositionTestFactory.Create(
            plan,
            identity,
            defaultOutputFileName,
            icNumberInputMode,
            validationRequirements,
            allowOutputOverride: allowOutputOverride,
            inputLengthRequirement: inputLengthRequirement);
    }

    private sealed class FakeOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        internal string? FileName { get; private set; }

        internal byte[] OutputBytes { get; private set; } = [];

        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            FileName = fileName;
            OutputBytes = outputBytes.ToArray();
            return ValueTask.FromResult(CompositionOutputCommitReceipt.CreateLoose(
                $"committed:{fileName}", fileName, outputBytes.Span));
        }
    }

    private sealed class CountingArtifactReader(byte[]? bytes) : IArtifactReader
    {
        internal int ReadCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return bytes is null
                ? ValueTask.FromException<ReadOnlyMemory<byte>>(new FileNotFoundException(artifactId))
                : ValueTask.FromResult<ReadOnlyMemory<byte>>(bytes);
        }
    }
}
