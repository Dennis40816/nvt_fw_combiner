using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Windows-only smoke tests for the committed legacy Combiner.exe binding.</summary>
public sealed class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>Verifies the real Combiner.exe can run a golden-backed NT51927 CRC-only command through the host adapter.</summary>
    [Fact]
    public async Task RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-real-combiner-smoke-{Guid.NewGuid():N}");

        try
        {
            byte[] goldenBytes = File.ReadAllBytes(FindGoldenExpectedOutput(goldenRoot, "51927"));
            ExternalCombinerToolManifest manifest = LoadManifest(
                Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
            Assert.Equal(
                manifest.Sha256,
                Sha256(Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName)));

            var crcOnlyCommand = new LegacyCombinerPostbuildCommand(
                "nt51927-real-tool-crc-smoke",
                LegacyCombinerCommandFamily.CrcOnlyMode,
                "NT51927BASED_GEN_CRC_MODE",
                "CRC32",
                []);
            var smokeProfile = new LegacyCombinerPostbuildProfile(
                "nfc.nt51927.real-tool-crc-smoke-v1",
                "NT51927",
                "legacy-combiner-1.13.0",
                "nt51927_fw.bin",
                [crcOnlyCommand],
                [crcOnlyCommand],
                "Windows smoke test for the committed Combiner 1.13.0 binding.");
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [smokeProfile],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            var request = new ExternalProcessorRequest(
                "real-tool-nt51927-crc-smoke",
                smokeProfile.ProcessorId,
                "legacy-combiner-1.13.0",
                goldenBytes,
                [],
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

            ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Empty(result.ChangedRanges);
            Assert.Equal(goldenBytes, result.OutputBytes.ToArray());
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    /// <summary>Locks the accepted 16-byte direct-combiner behavior for CtrlRAM self-replacement scope.</summary>
    [Theory]
    [MemberData(nameof(SixteenByteSelfReplacementCases))]
    public async Task DirectRealToolSixteenByteCasesMatchForSingleAndMultipleCtrlRamSelfReplacement(
        string icId,
        string manifestIc,
        IcNumberInputMode mode,
        string selectionToken,
        long[] expectedChangedRangeValues)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestIc);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionToken);
        ArgumentNullException.ThrowIfNull(expectedChangedRangeValues);

        string testName = $"{icId}/{selectionToken}";
        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        string executableSource = Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName);
        Assert.Equal(manifest.Sha256, Sha256(executableSource));

        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out LegacyCombinerPostbuildProfile? profile));
        IcNumberSelection selection = new(mode, [selectionToken]);
        TpFlashMapRegion[] mappedRegions =
        [
            .. TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection)
                .OrderBy(region => region.Range.Start)
                .ThenBy(region => region.RegionId, StringComparer.Ordinal),
        ];
        Assert.True(
            mappedRegions.Length >= 2,
            $"{testName} must expose multiple postbuild-mapped CtrlRAM regions.");

        byte[] baseBytes = File.ReadAllBytes(FindGoldenExpectedOutput(goldenRoot, manifestIc));
        byte[] singleRegionBytes = CreateSelfReplacementBytes(baseBytes, [mappedRegions[0]]);
        byte[] multipleRegionBytes = CreateSelfReplacementBytes(baseBytes, mappedRegions);
        Assert.Equal(baseBytes, singleRegionBytes);
        Assert.Equal(baseBytes, multipleRegionBytes);

        List<ByteRange> expectedChangedRanges = DecodeRanges(expectedChangedRangeValues);
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-direct-combiner-16byte-{Guid.NewGuid():N}");
        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [profile!],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            byte[] singleOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                singleRegionBytes,
                expectedChangedRanges,
                $"{icId}-{selectionToken}-single-region",
                CancellationToken.None);
            byte[] multipleOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                multipleRegionBytes,
                expectedChangedRanges,
                $"{icId}-{selectionToken}-multiple-regions",
                CancellationToken.None);

            Assert.Equal(singleOutput, multipleOutput);

            IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(baseBytes, singleOutput);
            Assert.Equal(expectedChangedRanges, changedRanges);
            Assert.Equal(16, changedRanges.Sum(range => range.Length));
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies pure Combiner pasteback produces the same bytes as the older pre-pasted work image model.</summary>
    [Theory]
    [MemberData(nameof(PureCombinerPastebackEquivalenceCases))]
    public async Task DirectRealToolPureCombinerPastebackMatchesPrePasteFlow(
        string icId,
        string manifestIc,
        IcNumberInputMode mode,
        string selectionToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        string executableSource = Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName);
        Assert.Equal(manifest.Sha256, Sha256(executableSource));

        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out LegacyCombinerPostbuildProfile? profile));
        IcNumberSelection selection = new(mode, [selectionToken]);
        TpFlashMapRegion selectedRegion = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection)
            .OrderBy(region => region.Range.Start)
            .ThenBy(region => region.RegionId, StringComparer.Ordinal)
            .First();
        byte[] baseBytes = File.ReadAllBytes(FindGoldenExpectedOutput(goldenRoot, manifestIc));
        byte[] replacementBytes = baseBytes.AsSpan(
                (int)selectedRegion.Range.Start,
                (int)selectedRegion.Range.Length)
            .ToArray();
        replacementBytes[0] ^= 0x5A;
        byte[] prePastedBytes = [.. baseBytes];
        replacementBytes.AsSpan().CopyTo(prePastedBytes.AsSpan((int)selectedRegion.Range.Start));
        Assert.NotEqual(baseBytes[(int)selectedRegion.Range.Start], prePastedBytes[(int)selectedRegion.Range.Start]);

        List<ByteRange> allowWholeImage = [new ByteRange(0, baseBytes.LongLength)];
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-direct-combiner-pasteback-equivalence-{Guid.NewGuid():N}");
        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [profile!],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            byte[] prePastedOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                prePastedBytes,
                allowWholeImage,
                $"{icId}-{selectionToken}-pre-pasted",
                CancellationToken.None,
                assertChangedRanges: false);
            byte[] purePastebackOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                baseBytes,
                allowWholeImage,
                $"{icId}-{selectionToken}-pure-pasteback",
                CancellationToken.None,
                [new ExternalProcessorStagedSource(selectedRegion.Range, replacementBytes)],
                assertChangedRanges: false);

            Assert.Equal(prePastedOutput, purePastebackOutput);
            Assert.Equal(replacementBytes[0], purePastebackOutput[(int)selectedRegion.Range.Start]);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    /// <summary>Accepted 16-byte direct-combiner CtrlRAM self-replacement matrix.</summary>
    public static TheoryData<string, string, IcNumberInputMode, string, long[]> SixteenByteSelfReplacementCases()
    {
        return new TheoryData<string, string, IcNumberInputMode, string, long[]>
        {
            { "NT51920", "51920", IcNumberInputMode.SingleSelector, "single", Nt51920RangeValues() },
            { "NT51920", "51920", IcNumberInputMode.CascadeSelector, "cascade", Nt51920RangeValues() },
            { "NT51923", "51923", IcNumberInputMode.SingleSelector, "single", Nt51923RangeValues() },
            { "NT51923", "51923", IcNumberInputMode.CascadeSelector, "cascade", Nt51923RangeValues() },
            { "NT51929", "51929", IcNumberInputMode.SingleSelector, "single", Nt51929RangeValues() },
            { "NT51929", "51929", IcNumberInputMode.CascadeSelector, "cascade", Nt51929RangeValues() },
            { "NT51932", "51932", IcNumberInputMode.SingleSelector, "single", Nt51929RangeValues() },
            { "NT51932", "51932", IcNumberInputMode.CascadeSelector, "cascade", Nt51929RangeValues() },
            { "NT51950", "51950", IcNumberInputMode.SingleSelector, "single", Nt51950RangeValues() },
            { "NT51950", "51950", IcNumberInputMode.CascadeSelector, "cascade", Nt51950RangeValues() },
            { "NT51951", "51951", IcNumberInputMode.SingleSelector, "single", Nt51950RangeValues() },
            { "NT51951", "51951", IcNumberInputMode.CascadeSelector, "cascade", Nt51950RangeValues() },
        };
    }

    /// <summary>Representative command-family cases used to compare pre-paste and pure Combiner pasteback.</summary>
    public static TheoryData<string, string, IcNumberInputMode, string> PureCombinerPastebackEquivalenceCases()
    {
        return new TheoryData<string, string, IcNumberInputMode, string>
        {
            { "NT51920", "51920", IcNumberInputMode.SingleSelector, "single" },
            { "NT51923", "51923", IcNumberInputMode.CascadeSelector, "cascade" },
            { "NT51926", "51926", IcNumberInputMode.SingleSelector, "single" },
            { "NT51927", "51927", IcNumberInputMode.SingleSelector, "single" },
            { "NT51950", "51950", IcNumberInputMode.SingleSelector, "single" },
        };
    }

    private static long[] Nt51920RangeValues()
    {
        return
        [
            0x1C, 4,
            0xFC, 4,
            0x2669C, 4,
            0x2677C, 4,
        ];
    }

    private static long[] Nt51923RangeValues()
    {
        return
        [
            0x1C, 4,
            0xFC, 4,
            0x3032C, 4,
            0x3040C, 4,
        ];
    }

    private static long[] Nt51929RangeValues()
    {
        return
        [
            0x7100, 4,
            0x7118, 4,
            0x27FF0, 4,
            0x28008, 4,
        ];
    }

    private static long[] Nt51950RangeValues()
    {
        return
        [
            0xA11C, 4,
            0xA130, 4,
            0x2D428, 4,
            0x2D43C, 4,
        ];
    }

    private static List<ByteRange> DecodeRanges(long[] rangeValues)
    {
        if (rangeValues.Length % 2 != 0)
        {
            throw new ArgumentException("Range values must contain start/length pairs.", nameof(rangeValues));
        }

        List<ByteRange> ranges = [];
        for (int index = 0; index < rangeValues.Length; index += 2)
        {
            ranges.Add(new ByteRange(rangeValues[index], rangeValues[index + 1]));
        }

        return ranges;
    }

    private static byte[] CreateSelfReplacementBytes(
        byte[] baseBytes,
        IReadOnlyList<TpFlashMapRegion> selectedRegions)
    {
        byte[] replacementBytes = [.. baseBytes];
        foreach (TpFlashMapRegion region in selectedRegions)
        {
            if (region.Range.EndExclusive > baseBytes.LongLength)
            {
                throw new InvalidOperationException($"Region '{region.RegionId}' exceeds the golden base image.");
            }

            baseBytes.AsSpan((int)region.Range.Start, (int)region.Range.Length)
                .CopyTo(replacementBytes.AsSpan((int)region.Range.Start, (int)region.Range.Length));
        }

        return replacementBytes;
    }

    private static async Task<byte[]> RunPostbuildProcessorAsync(
        LegacyCombinerPostbuildProcessor processor,
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection selection,
        byte[] postReplacementBytes,
        IReadOnlyList<ByteRange> expectedChangedRanges,
        string runId,
        CancellationToken cancellationToken,
        IReadOnlyList<ExternalProcessorStagedSource>? stagedSources = null,
        bool assertChangedRanges = true)
    {
        var request = new ExternalProcessorRequest(
            runId,
            profile.ProcessorId,
            "legacy-combiner-1.13.0",
            postReplacementBytes,
            expectedChangedRanges,
            selection,
            stagedSources);

        ExternalProcessorResult result = await processor.TransformAsync(request, cancellationToken).ConfigureAwait(false);
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        if (assertChangedRanges)
        {
            Assert.Equal(expectedChangedRanges, result.ChangedRanges);
        }

        return result.OutputBytes.ToArray();
    }

    private static string FindGoldenExpectedOutput(string goldenRoot, string ic)
    {
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == ic);
        string relativePath = goldenCase.GetProperty("expectedOutput").GetProperty("path").GetString()!;
        return Path.Combine(goldenRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static ExternalCombinerToolManifest LoadManifest(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return new ExternalCombinerToolManifest(
            RequiredString(root, "schemaVersion"),
            RequiredString(root, "toolBindingId"),
            RequiredString(root, "toolId"),
            RequiredString(root, "toolVersion"),
            RequiredString(root, "displayName"),
            RequiredString(root, "platform"),
            RequiredString(root, "executableName"),
            RequiredString(root, "sha256"),
            RequiredString(root, "adapterId"),
            RequiredString(root, "inputMode"),
            [.. root.GetProperty("argumentTemplate").EnumerateArray().Select(item => item.GetString()!)],
            RequiredString(root, "workingDirectoryPolicy"),
            root.GetProperty("timeoutSeconds").GetInt32(),
            [.. root.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(item => item.GetString()!)]);
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is null.");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SPEC.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
