using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests CtrlRAM report metadata authority with materialized built-in profiles.</summary>
public sealed class CtrlRamReportMetadataPlanTests
{
    /// <summary>TP-work and full-flash bases need no Standard Merge map when report classification is undeclared.</summary>
    [Theory]
    [InlineData("NT51950", 0x37000)]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x37000)]
    [InlineData("NT51951", 0x80000)]
    public void UndeclaredReportClassificationUsesEmptyPlan(
        string icId,
        long referenceCapacity)
    {
        MetadataPlanDefinition plan =
            BuiltInCtrlRamAuthoringAdapter.CreateCtrlRamReportMetadataPlan(
                icId,
                referenceCapacity);

        Assert.Same(MetadataPlanDefinition.Empty, plan);
        Assert.Empty(plan.Entries);
        Assert.Empty(plan.ReportProjections);
        Assert.Null(plan.SourceIdentity);
    }

    /// <summary>A readable base one byte outside every declared map becomes a typed input issue.</summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "tp-input", -1)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "tp-input", 1)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "expected-output", -1)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "expected-output", 1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "tp-input", -1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "tp-input", 1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "expected-output", -1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "expected-output", 1)]
    public void NonMapReferenceCapacityReturnsTypedLengthIssue(
        string caseId,
        string icId,
        string baseArtifactId,
        int lengthDelta)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            caseId);
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == baseArtifactId);
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        byte[] source = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        byte[] invalid = lengthDelta > 0
            ? [.. source, 0x00]
            : source[..^1];
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ctrlram-invalid-capacity");
        string basePath = workspace.Write("reference.bin", invalid);
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-nf"] = CanonicalGoldenTestData.ArtifactPath(replacementArtifact),
        };
        Dictionary<string, byte[]> inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        CtrlRamAuthoringSessionPreparation? preparation = null;

        Exception? exception = Record.Exception(() =>
            preparation = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.CtrlRamReplace),
                icId,
                "single",
                slotPaths,
                inputBytes));

        Assert.Null(exception);
        Assert.NotNull(preparation);
        Assert.Null(preparation.AcceptedSession);
        CompositionIssue issue = Assert.Single(
            preparation.Issues,
            issue => issue.Code == CompositionIssueCodes.InputAddressSpaceLengthMismatch &&
                issue.OperationId == CompositionSlotIds.ReplaceBase);
        Assert.Contains("length", issue.Message, StringComparison.OrdinalIgnoreCase);
    }
}
