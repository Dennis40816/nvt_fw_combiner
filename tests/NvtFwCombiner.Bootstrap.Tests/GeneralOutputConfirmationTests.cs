using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>General confirmation retains accepted source and reference identities.</summary>
public sealed class GeneralOutputConfirmationTests
{
    /// <summary>General inputs remain visible without compiled slot-inspection statuses.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, true, true)]
    public async Task AcceptedGeneralFilesAppearInConfirmation(bool replace, bool extraMappings, bool unsupportedInline = false)
    {
        using TempWorkspace workspace = TempWorkspace.Create("general-confirmation");
        CompositionHostServices host = CompositionHostServices.Create();
        byte[] sourceBytes = [0xA5, 0x5A];
        byte[] referenceBytes = new byte[0x40000];
        string source = workspace.Write("source.bin", sourceBytes);
        string reference = workspace.Write("base.bin", referenceBytes);
        List<GeneralMappingDraftRow> rows = [
            new GeneralMappingDraftRow("copy-source",
                replace ? ExplicitMappingOperationKind.ReplaceRange : ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(source), new ByteRange(0, 2), CompositionAddressSpaceIds.OutputImage,
                new ByteRange(replace ? 0x3E010 : 0, 2), OverlapPolicy.Reject, 1, "confirmation regression")];
        if (extraMappings)
        {
            rows.Add(new GeneralMappingDraftRow("same-source-again",
                replace ? ExplicitMappingOperationKind.ReplaceRange : ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(source), new ByteRange(0, 2), CompositionAddressSpaceIds.OutputImage,
                new ByteRange(replace ? 0x3E020 : 4, 2), OverlapPolicy.Reject, 1, "shared-file regression"));
            if (unsupportedInline)
            {
                rows.Add(new GeneralMappingDraftRow("inline-patch", ExplicitMappingOperationKind.ReplaceRange,
                    GeneralMappingSource.HexOverwrite("1122"), new ByteRange(0, 2), CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0x3E030, 2), OverlapPolicy.Reject, 1, "virtual source regression"));
            }
        }
        var mappings = new GeneralMappingDraftState(rows);
        GeneralAuthoringSessionPreparation prepared = replace
            ? await host.GeneralAuthoring.PrepareReplaceSessionAsync(new AuthoringSessionState(ExperienceIds.GeneralReplace),
                "NT51926", "single", reference, mappings, TestContext.Current.CancellationToken)
            : await host.GeneralAuthoring.PrepareMergeSessionAsync(new AuthoringSessionState(ExperienceIds.GeneralMerge),
                "NT51926", new GeneralMergeDraftState(new GeneralMergeOutputInitializer(16), mappings), TestContext.Current.CancellationToken);
        if (unsupportedInline)
        {
            Assert.False(prepared.Succeeded);
            Assert.Contains(prepared.Issues, issue => issue.Code == "replace.workflow.not-supported");
            return;
        }
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        Assert.Empty(prepared.AcceptedSession!.InputSlotStatuses);
        // Preparation must project accepted identities, never reread changed disk content.
        await File.WriteAllBytesAsync(source, [0xFF], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(reference, [0xEE], TestContext.Current.CancellationToken);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(
            prepared.AcceptedSession, TestContext.Current.CancellationToken);
        CompositionOutputConfirmationSummary summary = Assert.IsType<CompositionOutputConfirmationSummary>(proposal.Confirmation);
        if (replace)
        {
            MapBoundV2CompilationContext physical = Assert.IsType<MapBoundV2CompilationContext>(
                prepared.AcceptedSession.ExactCapability!.CompiledComposition.V2Details.Provenance.Context, exactMatch: false);
            Assert.Equal(new CompositionOutputFlashMapSummary(physical.ResolvedMap.ImageMap.MapId, physical.ResolvedMap.DisplayName), summary.FlashMap);
        }
        else
        {
            _ = Assert.IsType<LogicalOutputV2CompilationContext>(prepared.AcceptedSession.ExactCapability!.CompiledComposition.V2Details.Provenance.Context);
            Assert.Null(summary.FlashMap);
        }
        Assert.All(summary.Inputs, input => Assert.Equal(
            prepared.AcceptedSession.ExactCapability!.CompiledComposition.V2Details.InputContract.SpaceBindings
                .Single(binding => binding.AddressSpaceId == input.BindingId).SlotId, input.SlotId));
        Assert.Equal((replace ? 2 : 1) + (extraMappings ? 1 : 0), summary.Inputs.Count);
        CompositionOutputInputSummary[] fileInputs = [.. summary.Inputs.Where(item => item.SourceFileName == "source.bin")];
        Assert.Equal(extraMappings ? 2 : 1, fileInputs.Length);
        Assert.All(fileInputs, input =>
        {
            Assert.Equal(2, input.SizeBytes);
            Assert.Equal(FileStamp.FromBytes(sourceBytes).Sha256, input.Sha256);
            Assert.Null(input.Inspection);
            Assert.Null(input.InspectionLifecycle);
            Assert.Empty(input.ExpectedLengths);
        });
        Assert.False(summary.HasGeneratedInputs);
        Assert.Equal(replace ? 2 : 1, proposal.Sources.Count);
        Assert.Equal(FileStamp.FromBytes(sourceBytes).Sha256,
            Assert.Single(proposal.Sources, item => item.OriginalFileName == "source.bin").Sha256);
        if (replace)
        {
            CompositionOutputInputSummary baseline = Assert.Single(summary.Inputs, item => item.SourceFileName == "base.bin");
            Assert.Equal(referenceBytes.Length, baseline.SizeBytes);
            Assert.Equal(FileStamp.FromBytes(referenceBytes).Sha256, baseline.Sha256);
        }
    }
}
