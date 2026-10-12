using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AcceptedSessionFileIdentityTests
{
    /// <summary>One accepted immutable container path may supply CtrlRAM Base and one replacement binding.</summary>
    [Fact]
    public async Task CtrlRamReplaceAcceptedSessionSharesOnePathAcrossLogicalSlots()
    {
        ReloadCatalog();
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement[] artifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        string sharedPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "expected-output"));
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = sharedPath,
            ["replace-ctrlram-normal"] = sharedPath,
        };
        (ActiveSessionSnapshot? snapshot, _) = CtrlRamReplaceTestSupport.Prepare(
            _host.Canonical,
            "NT51926",
            "single",
            paths,
            firmwareVersionEdit: null);
        ActiveSessionSnapshot accepted = Assert.IsType<ActiveSessionSnapshot>(snapshot);

        CompositionRunResult result = await CtrlRamReplaceTestSupport.ExecuteAcceptedWithProcessorAsync(
            _host.Canonical,
            accepted,
            paths,
            build: false,
            outputPath: null,
            new PassThroughProcessor(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"Composition result: status={result.Status}, succeeded={result.Succeeded}.");
        Assert.Equal(2, result.Report.Inputs.Count);
        Assert.Equal(2, result.Report.Inputs.Select(static input => input.AddressSpaceId)
            .Distinct(StringComparer.Ordinal).Count());
        _ = Assert.Single(result.Report.Inputs.Select(static input => input.Sha256)
            .Distinct(StringComparer.Ordinal));
        Assert.All(result.Report.Inputs, static input =>
            Assert.Equal(input.AddressSpaceId, input.ArtifactId));
        string replacementAddressSpaceId = result.Report.Inputs.Single(input =>
            input.AddressSpaceId != CompositionAddressSpaceIds.ReferenceBase).AddressSpaceId;
        Assert.Contains(result.Report.Operations, operation =>
            operation.SourceSpaceId == replacementAddressSpaceId);
        AssertOperationProjection(accepted, result);
    }

    /// <summary>CtrlRAM Build and bundle delivery retain the exact accepted bytes after the source is changed or removed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CtrlRamReplaceAcceptedSessionBuildsBundleAfterSourceMutation(
        bool deleteSource)
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ctrlram-accepted-content");
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement[] artifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        string basePath = workspace.Write(
            "accepted-base.bin",
            GoldenFileIo.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
                artifact.GetProperty("artifactId").GetString() == "expected-output"))));
        string replacementPath = workspace.Write(
            "accepted-normal-ctrlram.bin",
            GoldenFileIo.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
                artifact.GetProperty("artifactId").GetString() == "normal-ctrlram-input"))));
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };
        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            CtrlRamReplaceTestSupport.Prepare(
                _host.Canonical,
                "NT51926",
                "single",
                paths,
                firmwareVersionEdit: null);
        ActiveSessionSnapshot accepted = Assert.IsType<ActiveSessionSnapshot>(snapshot);
        Assert.Empty(issues);
        CompositionRunResult baseline = await CtrlRamReplaceTestSupport
            .ExecuteAcceptedWithProcessorAsync(
                _host.Canonical,
                accepted,
                paths,
                build: false,
                outputPath: null,
                new PassThroughProcessor(),
                TestContext.Current.CancellationToken);
        CompositionOutputBundleProposal proposal = _host.Services.CompositionOutputNaming
            .ResolveAcceptedBundleProposal(accepted);
        Assert.Equal(2, proposal.Sources.Count);
        Assert.Contains(proposal.Sources, static source =>
            source.SlotId == CompositionAddressSpaceIds.ReferenceBase);
        Assert.Contains(proposal.Sources, static source =>
            source.SlotId == "replace-ctrlram-normal");

        if (deleteSource)
        {
            File.Delete(replacementPath);
        }
        else
        {
            MutateFirstByte(replacementPath);
        }

        CompositionRunResult built = await CtrlRamReplaceTestSupport
            .ExecuteAcceptedWithProcessorAsync(
                _host.Canonical,
                accepted,
                paths,
                build: true,
                outputPath: null,
                new PassThroughProcessor(),
                TestContext.Current.CancellationToken,
                proposal.CreateIntent(
                    workspace.Root,
                    deleteSource ? "ctrlram_deleted_source" : "ctrlram_changed_source"));

        Assert.True(baseline.Succeeded, $"Composition result: status={baseline.Status}, succeeded={baseline.Succeeded}.");
        Assert.True(built.Succeeded, $"Composition result: status={built.Status}, succeeded={built.Succeeded}.");
        GoldenAssert.AssertHashEqualsGolden(baseline.OutputSha256, built.OutputSha256, "SHA-256");
        GoldenAssert.AssertEqualsGolden(baseline.OutputBytes.ToArray(), built.OutputBytes.ToArray(), "firmware image");
        CompositionOutputBundleDeliverySummary delivery = Assert.IsType<CompositionOutputBundleDeliverySummary>(
            built.Report.BundleDelivery);
        Assert.Equal(1 + proposal.Sources.Count, delivery.Artifacts.Count);
        CompositionOutputBundleDeliveredArtifactSummary output = Assert.Single(
            delivery.Artifacts,
            static artifact => artifact.Role == "output");
        GoldenAssert.AssertEqualsGolden(built.OutputBytes.ToArray(),
            GoldenFileIo.ReadAllBytes(Path.Combine(delivery.ResolvedDirectory, output.DeliveredFileName)), "firmware image");
        foreach (CompositionOutputBundleSourceSummary source in proposal.Sources)
        {
            CompositionOutputBundleDeliveredArtifactSummary delivered = Assert.Single(
                delivery.Artifacts,
                artifact => artifact.Role == "source" && artifact.BindingId == source.BindingId);
            AuthoringInputSlotStatus status = accepted.InputSlotStatuses.Single(candidate =>
                candidate.SlotId == source.SlotId);
            byte[] deliveredBytes = GoldenFileIo.ReadAllBytes(
                Path.Combine(delivery.ResolvedDirectory, delivered.DeliveredFileName));
            Assert.Equal(source.OriginalFileName, delivered.DeliveredFileName);
            Assert.Equal(source.Size, delivered.Size);
            GoldenAssert.AssertHashEqualsGolden(source.Sha256, delivered.Sha256, "SHA-256");
            GoldenAssert.AssertEqualsGolden(status.AcceptedBytes!.Value.ToArray(), deliveredBytes, "firmware image");
            Assert.Equal(status.FileStamp!.Value, FileStamp.FromBytes(deliveredBytes));
        }
        AssertOperationProjection(accepted, built);
    }
}
