using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench evidence for pre-run General Replace POSTBUILD readiness.</summary>
public sealed class GeneralReplacePostbuildReadinessTests
{
    /// <summary>An uncompiled POSTBUILD target fails before runtime readiness can claim Build identity.</summary>
    [Fact]
    public async Task MissingRuntimeToolCreatesDiagnosticWithoutEngineOutputOrMutation()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-diagnostic");
        byte[] referenceBytes = CreatePostbuildReference(0x31);
        byte[] sourceBytes = [0xA5, 0x5A];
        string referencePath = workspace.Write("reference.bin", referenceBytes);
        string sourcePath = workspace.Write("source.bin", sourceBytes);
        GeneralMappingDraftState draft = Draft(sourcePath);
        var progress = new CompositionRunProgressFeed();

        GeneralAuthoringSessionPreparation prepared = await PrepareAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CompositionSlotIds.ReplaceBase] = referencePath,
                },
                draft,
                TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        Assert.Equal(referenceBytes, await File.ReadAllBytesAsync(
            referencePath,
            TestContext.Current.CancellationToken));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(
            sourcePath,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported,
            Assert.Single(prepared.Issues).Code);
        Assert.False(progress.IsAttached);
    }

    /// <summary>Build also fails before a runtime probe when no exact compilation exists.</summary>
    [Fact]
    public async Task MissingRuntimeToolDisablesBuildWithoutCreatingRunReport()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-build");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePostbuildReference(0x32));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        var progress = new CompositionRunProgressFeed();

        GeneralAuthoringSessionPreparation prepared = await PrepareAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CompositionSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        Assert.False(File.Exists(outputPath));
        Assert.False(progress.IsAttached);
    }

    /// <summary>An unsupported target never starts a refresh whose generation could become stale.</summary>
    [Fact]
    public async Task StaleRuntimeGenerationCannotReachEngineExecution()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-stale");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePostbuildReference(0x33));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        var progress = new CompositionRunProgressFeed();

        GeneralAuthoringSessionPreparation prepared = await PrepareAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CompositionSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        Assert.Equal(
            CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported,
            Assert.Single(prepared.Issues).Code);
        Assert.False(progress.IsAttached);
    }

    private static ValueTask<GeneralAuthoringSessionPreparation> PrepareAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        CancellationToken cancellationToken)
    {
        return GeneralWorkflowTestSupport.PrepareGeneralReplaceAsync(
            BootstrapTestHost.Canonical,
            icId,
            number,
            slotPaths,
            mappingDraft,
            savedRulePolicy: null,
            cancellationToken);
    }

    private static byte[] CreatePostbuildReference(byte seed)
    {
        byte[] image = CreatePattern(0x40000, seed);
        const int backupStart = 0x3F000;
        const int markerStart = 0x3FFFC;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = 0x20;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = 0xDF;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 2;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 0;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = 0;
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }

    private static GeneralMappingDraftState Draft(string sourcePath)
    {
        return new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "tp-map",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(sourcePath),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0x22800, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                "Runtime readiness regression."),
        ]);
    }

}
