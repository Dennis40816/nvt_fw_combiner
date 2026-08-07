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

    private static CompiledComposition CreateCompiledComposition(
        CompositionPlan plan,
        TestCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        IcNumberInputMode? icNumberInputMode = null,
        IReadOnlyList<CompiledValidationRequirement>? validationRequirements = null,
        bool allowOutputOverride = false)
    {
        return CompiledCompositionTestFactory.Create(
            plan,
            identity,
            defaultOutputFileName,
            icNumberInputMode,
            validationRequirements,
            allowOutputOverride: allowOutputOverride);
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
