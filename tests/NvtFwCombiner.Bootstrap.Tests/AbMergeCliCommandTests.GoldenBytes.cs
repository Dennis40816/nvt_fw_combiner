using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeCliCommandTests
{
    /// <summary>
    /// Dummy retains Normal's complete TP bytes and fills every other byte with FF at its own compiled image length.
    /// NT51950 cascade and NT51951 mix TP/DP cases and compare against the candidate's Normal build only, without a Golden output anchor.
    /// </summary>
    [Theory]
    [InlineData("NT51929", null, "ab-merge", "nt51929-ab-t05-d06", "tp-a-input", "nt51929-ab-t05-d06")]
    [InlineData("NT51919", null, "ab-merge", "nt51929-ab-t05-d06", "tp-a-input", "nt51929-ab-t05-d06")]
    [InlineData("NT51932", null, "ab-merge", "nt51929-ab-t05-d06", "tp-a-input", "nt51929-ab-t05-d06")]
    [InlineData("NT51950", "single", "ab-merge", "nt51950-ab-boe-d82t80", "tp-a-input", "nt51950-ab-boe-d82t80")]
    [InlineData("NT51950", "single", "ab-merge", "nt51950-ab-hiway-d82t80", "tp-a-input", "nt51950-ab-hiway-d82t80")]
    [InlineData("NT51950", "single", "ab-merge", "nt51950-ab-osd-d03t02-20260924", "tp-a-input", "nt51950-ab-osd-d03t02-20260924")]
    // These three rows mix TP inputs from CtrlRAM cases with DP from a different AB case.
    // Their TP comparison is candidate Normal versus Dummy; no independent Golden output is asserted.
    [InlineData("NT51950", "cascade", "ctrlram-replace", "nt51951-fw200-cascade2-auto-prj-599-20260731", "tp-firmware-input", "nt51950-ab-osd-d03t02-20260924")]
    [InlineData("NT51951", null, "ctrlram-replace", "nt51951-fw200-single-auto-prj-695-20260718", "tp-input", "nt51950-ab-osd-d03t02-20260924")]
    [InlineData("NT51951", null, "ctrlram-replace", "nt51951-fw200-cascade2-auto-prj-599-20260731", "tp-firmware-input", "nt51950-ab-osd-d03t02-20260924")]
    public async Task DummyBuildKeepsTheNormalBuildsTpBytesAndFillsEverythingElseAsync(
        string profile, string? topology, string tpWorkflow, string tpCaseId, string tpArtifactId, string dpCaseId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tpWorkflow);
        ArgumentNullException.ThrowIfNull(tpCaseId);
        ArgumentNullException.ThrowIfNull(tpArtifactId);
        ArgumentNullException.ThrowIfNull(dpCaseId);
        if (!OperatingSystem.IsWindows() && profile is "NT51950" or "NT51951")
        {
            Assert.Skip("Real NT51950/NT51951 Golden Build requires the packaged Windows processor.");
        }

        JsonElement tpCase = CanonicalGoldenTestData.LoadDirectCase(tpWorkflow, tpCaseId);
        JsonElement dpCase = CanonicalGoldenTestData.LoadDirectCase("ab-merge", dpCaseId);
        string tpA = CliGoldenByteEvidence.ArtifactPath(tpCase, tpArtifactId);
        string tpB = tpWorkflow == "ab-merge" ? CliGoldenByteEvidence.ArtifactPath(tpCase, "tp-b-input") : tpA;
        string dp = CliGoldenByteEvidence.ArtifactPath(dpCase, "dp-ab-input");
        string[] selection = topology is null ? [] : ["--ab-topology", topology];
        string[] inputs = ["--profile", profile, .. selection, "--tp-a", tpA, "--tp-b", tpB];
        using var workspace = TempWorkspace.Create("ab-cli-golden-dummy");
        CliGoldenByteEvidence.BuildEvidence normal = await CliGoldenByteEvidence.BuildAsync(workspace, "normal",
            ["ab-merge", "build", .. inputs, "--dp-ab", dp]);
        CliGoldenByteEvidence.BuildEvidence dummy = await CliGoldenByteEvidence.BuildAsync(workspace, "dummy",
            ["ab-merge", "build", .. inputs, "--dp-mode", "dummy", "--acknowledge-non-tp-ff"]);

        CompiledAuthoringSelectedInput[] tpInputs =
        [
            new(CompositionAddressSpaceIds.TpAInput, tpA, File.ReadAllBytes(tpA)),
            new(CompositionAddressSpaceIds.TpBInput, tpB, File.ReadAllBytes(tpB)),
        ];
        CompiledComposition normalComposition = await PrepareReportedAbCompositionAsync(profile, topology,
            [.. tpInputs, new(CompositionAddressSpaceIds.DpAbInput, dp, File.ReadAllBytes(dp))], AbMergeDpMode.Normal, normal.Report);
        CompiledComposition dummyComposition = await PrepareReportedAbCompositionAsync(profile, topology,
            tpInputs, AbMergeDpMode.Dummy, dummy.Report);
        Assert.Equal(normalComposition.Plan.OutputInitialization.Capacity, normal.Bytes.LongLength);
        Assert.Equal(dummyComposition.Plan.OutputInitialization.Capacity, dummy.Bytes.LongLength);
        Assert.Equal(normal.Report.GetProperty("Output").GetProperty("Size").GetInt64(), normal.Bytes.LongLength);
        Assert.Equal(dummy.Report.GetProperty("Output").GetProperty("Size").GetInt64(), dummy.Bytes.LongLength);
        if (normalComposition.V2Details.Provenance.Context is ResolvedMapV2CompilationContext { SourceEnvelope: { } envelope } &&
            envelope.ActualOutputLength > envelope.LayoutTemplateCapacity)
        {
            // Normal preserves the DP tail; Dummy has no DP and uses the declared layout capacity.
            Assert.Equal(envelope.ActualOutputLength, normal.Bytes.LongLength);
            Assert.Equal(envelope.LayoutTemplateCapacity, dummy.Bytes.LongLength);
            Assert.True(normal.Bytes.LongLength > dummy.Bytes.LongLength);
        }
        else
        {
            Assert.Equal(normal.Bytes.LongLength, dummy.Bytes.LongLength);
        }

        JsonElement[] copies = [.. dummy.Report.GetProperty("Operations").EnumerateArray().Where(static operation =>
            operation.GetProperty("OperationId").GetString() is "copy-tpa" or "copy-tpb" or
                "overlay-tpa-into-output" or "overlay-tpb-into-output")];
        Assert.Equal(2, copies.Length);
        ByteRange[] tpRanges = [.. copies.Select(static operation => CliGoldenByteEvidence.Range(operation.GetProperty("TargetRange")))];
        foreach (JsonElement copy in copies)
        {
            Assert.Equal("output-image", copy.GetProperty("TargetSpaceId").GetString());
            string id = copy.GetProperty("OperationId").GetString()!;
            ByteRange range = CliGoldenByteEvidence.TargetRange(dummy.Report, id);
            Assert.InRange(range.Start, 0, dummy.Bytes.LongLength - 1);
            Assert.InRange(range.EndExclusive, range.Start + 1, dummy.Bytes.LongLength);
            Assert.Equal(range, CliGoldenByteEvidence.TargetRange(normal.Report, id));
            CliGoldenByteEvidence.EqualRange(normal.Bytes, dummy.Bytes, range);
        }

        Assert.True(tpRanges.Sum(static range => range.Length) < dummy.Bytes.LongLength);
        for (int offset = 0; offset < dummy.Bytes.Length; offset++)
        {
            if (!tpRanges[0].Contains(offset) && !tpRanges[1].Contains(offset) && dummy.Bytes[offset] != 0xFF)
            {
                Assert.Fail($"Non-TP output-image offset 0x{offset:X} is not FF.");
            }
        }

        if (tpWorkflow == "ab-merge")
        {
            // The NT51919/NT51932 rows are fact-scoped alias checks, not direct product Goldens.
            CliGoldenByteEvidence.EqualGolden(dpCase, normal.Bytes);
            byte[] goldenBytes = File.ReadAllBytes(CliGoldenByteEvidence.ArtifactPath(dpCase, "expected-output"));
            foreach (ByteRange range in tpRanges)
            {
                CliGoldenByteEvidence.EqualRange(goldenBytes, dummy.Bytes, range);
            }
        }
    }

    private static async Task<CompiledComposition> PrepareReportedAbCompositionAsync(
        string profile, string? topology, IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs,
        AbMergeDpMode dpMode, JsonElement report)
    {
        CompiledAuthoringSessionPreparation prepared = await BootstrapTestHost.ProductServices.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), profile, topology, inputs, dpMode,
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        CompiledComposition composition = prepared.Snapshot!.ExactCapability!.CompiledComposition;
        Assert.Equal(report.GetProperty("CompilationFingerprint").GetString(), composition.CompilationFingerprint);
        return composition;
    }
}
