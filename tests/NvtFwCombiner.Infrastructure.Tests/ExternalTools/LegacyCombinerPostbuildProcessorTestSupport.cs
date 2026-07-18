using System.Security.Cryptography;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using SharedTempWorkspace = NvtFwCombiner.TestSupport.TempWorkspace;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildProcessorTests
{
    private static byte[] CreateFirmwareImage()
    {
        byte[] bytes = new byte[0x40000];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }

    private static void MakeNt51927TwoChipSharedStagedBlocksConsistent(byte[] firmware)
    {
        Array.Copy(firmware, 0x16800, firmware, 0x1F800, 16);
        Array.Copy(firmware, 0x1CBD0, firmware, 0x25BD0, 5728);
    }

    private static LegacyCombinerPostbuildProfile CreateProjectionConflictProfile()
    {
        var command = new LegacyCombinerPostbuildCommand(
            "projection-conflict",
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Enable",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "first",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Same.bin",
                    0,
                    new ByteRange(0, 2)),
                new LegacyCombinerBlockArgument(
                    "second",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Same.bin",
                    0,
                    new ByteRange(2, 2)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.projection-conflict-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [command],
            [command],
            "test projection conflict profile");
    }

    private static LegacyCombinerPostbuildProfile CreateStagedReplacementProfile()
    {
        var command = new LegacyCombinerPostbuildCommand(
            "pasteback",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "replacement",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Replacement.bin",
                    0,
                    new ByteRange(1, 2)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.staged-replacement-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [command],
            [command],
            "test staged replacement pasteback profile");
    }

    private static LegacyCombinerPostbuildProfile CreateArtifactSourceProfile()
    {
        var command = new LegacyCombinerPostbuildCommand(
            "artifact-source",
            LegacyCombinerCommandFamily.NtBasedNormalMode,
            "NTTESTBASED_NORMAL_MODE",
            "CRC8",
            [
                new LegacyCombinerBlockArgument(
                    "a-bank",
                    LegacyCombinerBlockSourceKind.StagedArtifact,
                    "ABank.bin",
                    0,
                    new ByteRange(0, 4),
                    "a-bank"),
                new LegacyCombinerBlockArgument(
                    "b-bank",
                    LegacyCombinerBlockSourceKind.StagedArtifact,
                    "BBank.bin",
                    0,
                    new ByteRange(4, 4),
                    "b-bank"),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.artifact-source-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [command],
            [command],
            "test immutable staged artifact profile");
    }

    private static LegacyCombinerPostbuildProfile CreateArtifactSourceThenCrcProfile()
    {
        var first = new LegacyCombinerPostbuildCommand(
            "artifact-source-first",
            LegacyCombinerCommandFamily.NtBasedNormalMode,
            "NTTESTBASED_NORMAL_MODE",
            "CRC8",
            [
                new LegacyCombinerBlockArgument(
                    "a-bank-first",
                    LegacyCombinerBlockSourceKind.StagedArtifact,
                    "ABank.bin",
                    0,
                    new ByteRange(0, 4),
                    "a-bank"),
            ]);
        var second = new LegacyCombinerPostbuildCommand(
            "artifact-source-second",
            LegacyCombinerCommandFamily.NtBasedNormalMode,
            "NTTESTBASED_NORMAL_MODE",
            "CRC8",
            [
                new LegacyCombinerBlockArgument(
                    "b-bank-second",
                    LegacyCombinerBlockSourceKind.StagedArtifact,
                    "BBank.bin",
                    0,
                    new ByteRange(4, 4),
                    "b-bank"),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.artifact-source-then-crc-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [first, second],
            [first, second],
            "test deferred immutable staged artifact profile");
    }

    private static LegacyCombinerPostbuildProfile CreateCopyThenRestoreProfile()
    {
        var copyCommand = new LegacyCombinerPostbuildCommand(
            "copy-window",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "copy",
                    LegacyCombinerBlockSourceKind.FirmwareImage,
                    "firmware",
                    0,
                    new ByteRange(4, 1)),
            ]);
        var restoreCommand = new LegacyCombinerPostbuildCommand(
            "restore-replacement",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "replacement",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Replacement.bin",
                    0,
                    new ByteRange(4, 1)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.copy-then-restore-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "test_fw.bin",
            [copyCommand, restoreCommand],
            [copyCommand, restoreCommand],
            "test copy command followed by staged replacement restore");
    }

    private static LegacyCombinerPostbuildProfile CreateCrcOnlyProfile(
        string processorId,
        string firmwareFileName,
        string toolBindingId = "legacy-combiner-1.13.0")
    {
        var command = new LegacyCombinerPostbuildCommand(
            "crc-only",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        return new LegacyCombinerPostbuildProfile(
            processorId,
            "NTTEST",
            toolBindingId,
            firmwareFileName,
            [command],
            [command],
            "test crc-only profile");
    }

    private static LegacyCombinerPostbuildProfile CreateShortenedOutputProfile()
    {
        var command = new LegacyCombinerPostbuildCommand(
            "shortened-output",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "first-block",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Short.bin",
                    0,
                    new ByteRange(0, 0x20)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.shortened-output-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "shortened_fw.bin",
            [command],
            [command],
            "test shortened output profile");
    }

    private static LegacyCombinerPostbuildProfile CreateRepeatedCrcProfile(int commandCount)
    {
        LegacyCombinerPostbuildCommand[] commands = [
            .. Enumerable.Range(1, commandCount)
                .Select(index => new LegacyCombinerPostbuildCommand(
                    $"crc-only-{index}",
                    LegacyCombinerCommandFamily.CrcOnlyMode,
                    "NT51927BASED_GEN_CRC_MODE",
                    "CRC32",
                    [])),
        ];
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.repeated-crc-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "repeated_crc_fw.bin",
            commands,
            commands,
            "test repeated CRC command profile");
    }

    private static LegacyCombinerPostbuildProfile CreateShortenedOutputAfterCrcProfile()
    {
        var crcCommand = new LegacyCombinerPostbuildCommand(
            "crc-first",
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
        var shortenedCommand = new LegacyCombinerPostbuildCommand(
            "shortened-second",
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            [
                new LegacyCombinerBlockArgument(
                    "shortened-block",
                    LegacyCombinerBlockSourceKind.StagedFile,
                    "Short.bin",
                    0,
                    new ByteRange(0, 0x20)),
            ]);
        return new LegacyCombinerPostbuildProfile(
            "nfc.test.shortened-output-after-crc-v1",
            "NTTEST",
            "legacy-combiner-1.13.0",
            "shortened_after_crc_fw.bin",
            [crcCommand, shortenedCommand],
            [crcCommand, shortenedCommand],
            "test shortened output after accepted CRC state");
    }

    private sealed class FakeProcessRunner : IExternalProcessRunner
    {
        private readonly Func<ExternalProcessStartInfo, ExternalProcessResult> _run;

        internal FakeProcessRunner(Func<ExternalProcessStartInfo, ExternalProcessResult> run)
        {
            _run = run;
        }

        internal int RunCount { get; private set; }

        public ValueTask<ExternalProcessResult> RunAsync(
            ExternalProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return ValueTask.FromResult(_run(startInfo));
        }
    }

    private sealed class CountingFirmwareIo : ILegacyCombinerFirmwareIo
    {
        internal int LengthCheckCount { get; private set; }

        internal int FullReadCount { get; private set; }

        internal int TailReadCount { get; private set; }

        internal int TailAppendCount { get; private set; }

        public long GetLength(string path)
        {
            LengthCheckCount++;
            return PhysicalLegacyCombinerFirmwareIo.Instance.GetLength(path);
        }

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        {
            FullReadCount++;
            return PhysicalLegacyCombinerFirmwareIo.Instance.ReadAllBytesAsync(path, cancellationToken);
        }

        public Task<byte[]> ReadTailAsync(
            string path,
            long start,
            long length,
            CancellationToken cancellationToken)
        {
            TailReadCount++;
            return PhysicalLegacyCombinerFirmwareIo.Instance.ReadTailAsync(path, start, length, cancellationToken);
        }

        public Task AppendTailAsync(
            string path,
            long expectedCurrentLength,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            TailAppendCount++;
            return PhysicalLegacyCombinerFirmwareIo.Instance.AppendTailAsync(
                path,
                expectedCurrentLength,
                bytes,
                cancellationToken);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        private const string ToolId = "legacy-combiner";
        private const string ToolVersion = "1.13.0";
        private const string ExecutableName = "Combiner.exe";
        private readonly SharedTempWorkspace _workspace;

        private TempWorkspace(SharedTempWorkspace workspace)
        {
            _workspace = workspace;
            Root = workspace.Root;
            ToolRoot = Path.Combine(Root, "tools");
            StagingRoot = Path.Combine(Root, "staging");
            _ = Directory.CreateDirectory(ToolRoot);
            _ = Directory.CreateDirectory(StagingRoot);
        }

        internal string Root { get; }

        internal string ToolRoot { get; }

        internal string StagingRoot { get; }

        internal static TempWorkspace Create()
        {
            return new TempWorkspace(SharedTempWorkspace.Create("nfc-legacy-postbuild-tests"));
        }

        internal string CreateToolExecutable()
        {
            string toolDirectory = Path.Combine(ToolRoot, ToolId, ToolVersion);
            _ = Directory.CreateDirectory(toolDirectory);
            string executablePath = Path.Combine(toolDirectory, ExecutableName);
            File.WriteAllBytes(executablePath, [0x4D, 0x5A, 0x13, 0x00]);
            return Sha256(executablePath);
        }

        internal LegacyCombinerPostbuildProcessor CreateProcessor(
            string executableSha256,
            IExternalProcessRunner runner,
            IEnumerable<LegacyCombinerPostbuildProfile>? profiles = null,
            ILegacyCombinerFirmwareIo? firmwareIo = null)
        {
            var registry = new ExternalCombinerToolRegistry([Manifest(executableSha256)]);
            return new LegacyCombinerPostbuildProcessor(
                registry,
                profiles ?? LegacyCombinerPostbuildCatalog.All,
                ToolRoot,
                StagingRoot,
                runner,
                firmwareIo ?? PhysicalLegacyCombinerFirmwareIo.Instance);
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }

        private static ExternalCombinerToolManifest Manifest(string executableSha256)
        {
            return new ExternalCombinerToolManifest(
                "1.0",
                "legacy-combiner-1.13.0",
                ToolId,
                ToolVersion,
                "Legacy Combiner 1.13.0",
                "win-x64",
                ExecutableName,
                executableSha256,
                "legacy-combiner-postbuild-v1",
                "in-place",
                ["{staging.runDir}"],
                "staging-directory",
                5,
                []);
        }

        private static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
