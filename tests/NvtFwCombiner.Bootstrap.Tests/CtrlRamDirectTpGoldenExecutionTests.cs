using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Executes owner-evidenced CtrlRAM routes from their original immutable TP inputs.
/// </summary>
public sealed class CtrlRamDirectTpGoldenExecutionTests
{
    /// <summary>
    /// Proves each TP route uses the canonical inputs, the exact production route and constrained
    /// processor, and produces the complete declared owner TP view modulo only reviewed ranges.
    /// </summary>
    [Theory]
    [InlineData("nt51923-fw141-single-auto-prj-662-20260717", "NT51923", "single", "tp-input", "nt51923-ctrlram-replace-fw141-single", "nt51923-ctrlram-fw141-single-tp-work-240k", 0x3C000, "d91dd6470fe6084b0d0bbf855e4c443f2911995b028b370ec8dccbef2efc3e78", "6d14818a4b11ed9cebfcad810a3261f75e896e3378986736089541fe0fcc3e28", 16)]
    [InlineData("nt51923-fw141-cascade3-auto-prj-734-20260717", "NT51923", "cascade", "tp-input", "nt51923-ctrlram-replace-fw141-cascade3", "nt51923-ctrlram-fw141-cascade3-tp-work-240k", 0x3C000, "1eb5f8647cf58dacfeea21010abb17c23c62d4adb6b790501236bf4ad1badbc7", "7b7cb25f6c267d2a330b924c2ffc82db0d3afff06e2da35e8614f66225fa418d", 16)]
    [InlineData("nt51926-fw141-single-auto-prj-747-20260717", "NT51926", "single", "tp-input", "nt51926-ctrlram-replace-fw141-runtime-single", "nt51926-ctrlram-fw141-tp-work-240k", 0x3C000, "987274195623ed48d673a733fd4140fbfaadee5fa13168c42a50055e7ab8b1c3", "22888325a4c15a149251427c258efff9854421ce3b94481eba2b356a0b2db893", 16)]
    [InlineData("nt51926-fw141-cascade2-auto-prj-597-20260717", "NT51926", "cascade", "tp-input", "nt51926-ctrlram-replace-fw141-runtime-cascade", "nt51926-ctrlram-fw141-tp-work-240k", 0x3C000, "9c0b7338c2178d5a250960485bea3f9390ab85173f366bae4e2c4a8e29f279ab", "ad6b5cd0b8a3babe7e0363d4a77f257b3170b3a5ca3ba58b96b43fa746064500", 16)]
    [InlineData("nt51926-fw200-single-auto-prj-597-20260718", "NT51926", "single", "tp-input", "nt51926-ctrlram-replace-fw200-runtime-single", "nt51926-ctrlram-fw200-tp-work-240k", 0x3C000, "258c1a51305edf966b8a8fb8fd64f50338385af1178076ea1c0b873db7d6999c", "f1c4a61299741af4837ac656300ebe24da97742e9f0bac5d11617a6cfc277209", 16)]
    [InlineData("nt51926-fw200-cascade3-auto-prj-597-20260718", "NT51926", "cascade", "tp-input", "nt51926-ctrlram-replace-fw200-runtime-cascade", "nt51926-ctrlram-fw200-tp-work-240k", 0x3C000, "5e12cf6484d1d681826192b0c2b8aabead4098285f6a2facdfde732f4d1bc7e2", "2e79b7634219c9e67b8f4311d2dc0252d49e7dbe81ec6a56d610298a19cda50d", 16)]
    [InlineData("nt51927-fw141-single-auto-prj-529-20260717", "NT51927", "single", "tp-input", "nt51927-ctrlram-replace-fw141-single", "nt51927-ctrlram-fw141-single-tp-work-212k", 0x35000, "4569893ba45af44ae28e7401374aa549201b9b2edeb2743e9f878d650ac248ee", "6a1bbb51faaad6fe543f960104e0cd21436df2cbdbbbdb466fedacfaba84db2c", 24)]
    public async Task OriginalTpBaseProducesCompleteDeclaredOwnerViewWithinExactAuthorityAsync(
        string caseId,
        string icId,
        string number,
        string tpArtifactId,
        string expectedProfileId,
        string expectedMapId,
        int tpLength,
        string expectedOwnerViewSha256,
        string expectedActualOutputSha256,
        int expectedDifferenceCount)
    {
        RouteExecution execution = await ExecuteRouteAsync(
            caseId,
            icId,
            number,
            tpArtifactId,
            expectedProfileId,
            expectedMapId,
            tpLength,
            expectedActualOutputSha256);
        if (execution.IsSkipped)
        {
            return;
        }

        OwnerArtifact expected = RequireArtifact(execution.Artifacts, "expected-output");
        Assert.Equal("expected", expected.Role);
        Assert.True(expected.Bytes.Length >= tpLength);

        byte[] expectedOwnerView = expected.Bytes.AsSpan(0, tpLength).ToArray();
        Assert.Equal(expectedOwnerViewSha256, Hash(expectedOwnerView));
        AssertDifferencesMatchOwnerContract(
            execution.GoldenCase,
            expectedOwnerView,
            execution.Actual,
            expectedDifferenceCount);
    }

    /// <summary>
    /// Pins the three supported TP routes whose owner full-FlashCode prefix is not a TP-only golden.
    /// </summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "single", "tp-input", "nt51950-ctrlram-replace-fw200-single", "nt51950-ctrlram-fw200-single-tp-work", 0x37000, "8001bdeadfda988a4da181f2f66991abd238a4d83de29eb24f23e16ac491c885")]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "single", "tp-input", "nt51951-ctrlram-replace-fw200-single", "nt51951-ctrlram-fw200-single-tp-work", 0x37000, "b27e519242e29a2e2f2ad2d50376853375999e38d1d899a7b97122119a74d0b8")]
    [InlineData("nt51951-fw200-cascade2-auto-prj-599-20260731", "NT51951", "cascade", "tp-firmware-input", "nt51951-ctrlram-replace-fw1x-cascade", "nt51951-ctrlram-fw1x-cascade-tp-work", 0x37000, "a239645dd6e2527af34934e20ef7399ecb9d748aca32ce9ed4b829b94b8fc643")]
    public async Task ContractOnlyTpBaseRunsDeterministicallyWithinExactAuthorityAsync(
        string caseId,
        string icId,
        string number,
        string tpArtifactId,
        string expectedProfileId,
        string expectedMapId,
        int tpLength,
        string expectedOutputSha256)
    {
        _ = await ExecuteRouteAsync(
            caseId,
            icId,
            number,
            tpArtifactId,
            expectedProfileId,
            expectedMapId,
            tpLength,
            expectedOutputSha256);
    }

    /// <summary>
    /// Proves 256-KiB TP artifacts use the single capacity-matched full map; absence of a second
    /// map does not mean that only a FlashCode container is accepted.
    /// </summary>
    [Theory]
    [InlineData("nt51929-fw200-single-auto-prj-594-20260717", "NT51919", "single", "tp-input", "nt51919-ctrlram-replace-fw200-single", "nt51929-ctrlram-fw200-single-full-flash", "532598233a484f54791b0c252aacdd42a7f20198c4b86b6252901ecbb6afaae4")]
    [InlineData("nt51929-fw200-single-auto-prj-594-20260717", "NT51929", "single", "tp-input", "nt51929-ctrlram-replace-fw200-single", "nt51929-ctrlram-fw200-single-full-flash", "532598233a484f54791b0c252aacdd42a7f20198c4b86b6252901ecbb6afaae4")]
    [InlineData("nt51932-fw200-cascade3-auto-prj-525-20260718", "NT51932", IcNumberSelectionTokens.CascadeTwoToEight, "tp-input", "nt51932-ctrlram-replace-fw200-cascade", "nt51932-ctrlram-fw200-cascade-full-flash", "635216fa12a527fd53db41956431398bea6291a43b6a45f91505e25d5fa4c71c")]
    public async Task FullLengthTpBaseUsesCapacityMatchedFullMapAsync(
        string caseId,
        string icId,
        string number,
        string tpArtifactId,
        string expectedProfileId,
        string expectedMapId,
        string expectedOutputSha256)
    {
        RouteExecution execution = await ExecuteRouteAsync(
            caseId,
            icId,
            number,
            tpArtifactId,
            expectedProfileId,
            expectedMapId,
            0x40000,
            expectedOutputSha256);
        if (execution.IsSkipped || !StringComparer.Ordinal.Equals(icId, "NT51919"))
        {
            return;
        }

        CanonicalGoldenAlias alias = Assert.Single(
            CanonicalGoldenTestData.LoadWorkflowAliases("ctrlram-replace"),
            static candidate => StringComparer.Ordinal.Equals(
                candidate.CaseId,
                "nt51919-fw200-single-nt51929-alias"));
        Assert.Equal(caseId, alias.SourceCaseId);
        Assert.Equal("NT51929", alias.SourceIc);
    }

    private static async Task<RouteExecution> ExecuteRouteAsync(
        string caseId,
        string icId,
        string number,
        string tpArtifactId,
        string expectedProfileId,
        string expectedMapId,
        int tpLength,
        string expectedOutputSha256)
    {
        if (!OperatingSystem.IsWindows())
        {
            return RouteExecution.Skipped;
        }

        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        OwnerArtifact[] artifacts =
        [
            .. goldenCase.GetProperty("artifacts").EnumerateArray().Select(ReadArtifact),
        ];
        OwnerArtifact tpBase = RequireArtifact(artifacts, tpArtifactId);
        Assert.Equal("input", tpBase.Role);
        Assert.Equal(tpLength, tpBase.Bytes.Length);
        Dictionary<string, string> slotPaths = CreateSlotPaths(artifacts, tpBase.Path, icId, number);

        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            CtrlRamReplaceTestSupport.Prepare(
                BootstrapTestHost.Canonical,
                icId,
                number,
                slotPaths,
                firmwareVersionEdit: null);
        Assert.True(
            snapshot is not null,
            string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(
            snapshot.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection));
        Assert.Equal(
            expectedMapId,
            capability.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);

        var immutableHashes = artifacts
            .Where(static artifact => StringComparer.Ordinal.Equals(artifact.Role, "input"))
            .ToDictionary(static artifact => artifact.Path, static artifact => Hash(artifact.Bytes), StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create($"nfc-{caseId}-{icId}-tp-route");
        string outputPath = workspace.PathFor("tp-output.bin");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunAsync(
            BootstrapTestHost.Canonical,
            icId,
            number,
            ExperienceIds.CtrlRamReplace,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        byte[] actual = File.ReadAllBytes(outputPath);
        Assert.Equal(tpLength, actual.Length);
        string actualSha256 = Hash(actual);
        Assert.True(
            StringComparer.Ordinal.Equals(expectedOutputSha256, actualSha256),
            $"Actual TP output SHA-256: {actualSha256}");
        Assert.Equal(expectedOutputSha256, result.OutputSha256);
        AssertExecutionAuthority(result, expectedProfileId, tpLength, tpBase.Bytes, actual);
        Assert.All(
            immutableHashes,
            pair => Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key))));
        return new RouteExecution(goldenCase, artifacts, actual, false);
    }

    private static Dictionary<string, string> CreateSlotPaths(
        IReadOnlyList<OwnerArtifact> artifacts,
        string tpBasePath,
        string icId,
        string number)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = tpBasePath,
        };
        foreach (OwnerArtifact artifact in artifacts.Where(static artifact => artifact.Role == "input"))
        {
            string? slotId = artifact.FileName switch
            {
                "Normal_Ctrlram.bin" when icId == "NT51927" => "replace-ctrlram-normal-master",
                "Normal_Ctrlram.bin" => "replace-ctrlram-normal",
                "DiffDLM.bin" when number != "single" => "replace-ctrlram-diff",
                "MP_Ctrlram.bin" when icId == "NT51927" => "replace-ctrlram-mp-master",
                "MP_Ctrlram.bin" => "replace-ctrlram-mp",
                "VN_Ctrlram.bin" => "replace-ctrlram-vn",
                "NF_Ctrlram.bin" when icId == "NT51932" => null,
                "NF_Ctrlram.bin" => "replace-ctrlram-nf",
                _ => null,
            };
            if (slotId is not null)
            {
                slots.Add(slotId, artifact.Path);
            }
        }

        return slots;
    }

    private static void AssertDifferencesMatchOwnerContract(
        JsonElement goldenCase,
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        int expectedDifferenceCount)
    {
        CanonicalGoldenTestDisposition disposition = CanonicalGoldenTestData.RequireDisposition(
            goldenCase,
            CanonicalGoldenTestDispositionKind.AllowedByteDifference);
        long[] differenceCounts = new long[disposition.AllowedDifferenceRanges.Count];
        long total = 0;
        var unapproved = new List<int>();
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] == actual[index])
            {
                continue;
            }

            int allowedIndex = -1;
            for (int rangeIndex = 0; rangeIndex < disposition.AllowedDifferenceRanges.Count; rangeIndex++)
            {
                if (disposition.AllowedDifferenceRanges[rangeIndex].Contains(index))
                {
                    allowedIndex = rangeIndex;
                    break;
                }
            }

            if (allowedIndex < 0)
            {
                unapproved.Add(index);
                continue;
            }
            total++;
            differenceCounts[allowedIndex]++;
        }

        Assert.True(
            unapproved.Count == 0,
            $"Unapproved TP output differences: {unapproved.Count}; first offsets: " +
            string.Join(", ", unapproved.Take(16).Select(static offset => $"0x{offset:X}")));
        Assert.Equal(expectedDifferenceCount, total);
        Assert.All(differenceCounts, static count => Assert.Equal(4, count));
    }

    private static void AssertExecutionAuthority(
        CompositionRunResult result,
        string expectedProfileId,
        int tpLength,
        ReadOnlySpan<byte> originalBase,
        ReadOnlySpan<byte> actual)
    {
        Assert.Equal(expectedProfileId, result.Report.ProfileId);
        Assert.All(result.Report.Operations, operation =>
        {
            Assert.Equal(OperationRunStatus.Succeeded, operation.Status);
            Assert.Equal(CompositionAddressSpaceIds.OutputImage, operation.TargetSpaceId);
            Assert.InRange(operation.TargetRange.Start, 0, tpLength);
            Assert.InRange(operation.TargetRange.EndExclusive, 0, tpLength);
        });

        OperationRunSummary processor = Assert.Single(
            result.Report.Operations,
            static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor);
        Assert.Equal([new ByteRange(0, tpLength)], processor.ProcessorAllowedReadRanges);
        Assert.NotEmpty(processor.ProcessorAllowedWriteRanges);
        Assert.All(processor.ProcessorAllowedWriteRanges, range =>
        {
            Assert.InRange(range.Start, 0, tpLength);
            Assert.InRange(range.EndExclusive, 1, tpLength);
        });

        for (int index = 0; index < originalBase.Length; index++)
        {
            if (originalBase[index] != actual[index])
            {
                Assert.Contains(processor.ProcessorAllowedWriteRanges, range => range.Contains(index));
            }
        }
    }

    private static OwnerArtifact ReadArtifact(JsonElement entry)
    {
        string path = RepositoryPaths.ManifestPath(CanonicalGoldenTestData.Root, entry);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return new OwnerArtifact(
            entry.GetProperty("artifactId").GetString()!,
            entry.GetProperty("originalFileName").GetString()!,
            entry.GetProperty("role").GetString()!,
            path,
            bytes);
    }

    private static OwnerArtifact RequireArtifact(
        IEnumerable<OwnerArtifact> artifacts,
        string artifactId)
    {
        return artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.ArtifactId, artifactId));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record OwnerArtifact(
        string ArtifactId,
        string FileName,
        string Role,
        string Path,
        byte[] Bytes);

    private sealed record RouteExecution(
        JsonElement GoldenCase,
        IReadOnlyList<OwnerArtifact> Artifacts,
        byte[] Actual,
        bool IsSkipped)
    {
        internal static RouteExecution Skipped { get; } = new(
            default,
            [],
            [],
            true);
    }
}
