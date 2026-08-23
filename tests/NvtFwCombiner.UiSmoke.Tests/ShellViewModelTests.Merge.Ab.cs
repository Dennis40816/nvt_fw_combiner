using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MergeWorkflowTests
{
    /// <summary>Selected AB A-only output is promoted in the same atomic bundle as the primary and sources.</summary>
    [Fact]
    public async Task AbAdditionalDeliveryAndSourcesCommitInsideOneAtomicBundle()
    {
        const int dpLength = 0x80000;
        const int tpLength = 0x40000;
        using var inputs = TempWorkspace.Create("nvt-fw-combiner-ui-ab-bundle-inputs");
        using var destination = TempWorkspace.Create("nvt-fw-combiner-ui-ab-bundle-output");
        byte[] dp = new byte[dpLength];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, tpLength, major: 0x07, minor: 0x08, jira: 0x456);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            inputs.Write("dp-ab.bin", dp),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            inputs.Write("tp-a.bin", CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102)),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            inputs.Write("tp-b.bin", CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C)),
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.CanBuildMerge);

        await viewModel.Merge.RequestBuildOutputDeliveryAsync();
        Assert.True(viewModel.OutputDelivery.OffersAdditionalDelivery);
        viewModel.OutputDelivery.SetBundleEnabled(true);
        viewModel.OutputDelivery.SetAdditionalDeliveryEnabled(true);
        viewModel.OutputDelivery.SetParentDirectory(destination.Root);
        viewModel.OutputDelivery.SetBundleFolderName("ab-build");

        await viewModel.OutputDelivery.ConfirmBundleAsync();

        string bundle = Path.Combine(destination.Root, "ab-build");
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(Directory.Exists(bundle));
        Assert.Empty(Directory.EnumerateFiles(destination.Root, "*.bin"));
        Assert.Equal(5, Directory.EnumerateFiles(bundle, "*.bin").Count());
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement[] artifacts =
        [
            .. report.RootElement.GetProperty("BundleDelivery")
                .GetProperty("Artifacts")
                .EnumerateArray(),
        ];
        JsonElement additional = Assert.Single(artifacts, static artifact =>
            artifact.GetProperty("Role").GetString() == "additional-delivery");
        Assert.Equal(
            CompiledAdditionalDelivery.AbAFlashCodeKind,
            additional.GetProperty("BindingId").GetString());
        string additionalPath = Path.Combine(
            bundle,
            additional.GetProperty("DeliveredFileName").GetString()!);
        Assert.True(File.Exists(additionalPath));
        JsonElement primary = Assert.Single(artifacts, static artifact =>
            artifact.GetProperty("Role").GetString() == "output");
        byte[] primaryBytes = await File.ReadAllBytesAsync(
            Path.Combine(bundle, primary.GetProperty("DeliveredFileName").GetString()!),
            TestContext.Current.CancellationToken);
        byte[] additionalBytes = await File.ReadAllBytesAsync(
            additionalPath,
            TestContext.Current.CancellationToken);
        JsonElement delivery = Assert.Single(
            report.RootElement.GetProperty("DeliveryArtifacts").EnumerateArray());
        Assert.Equal(0, delivery.GetProperty("SourceRange").GetProperty("Start").GetInt64());
        Assert.Equal(additionalBytes.LongLength, delivery.GetProperty("SourceRange").GetProperty("Length").GetInt64());
        Assert.True(primaryBytes.AsSpan(0, additionalBytes.Length).SequenceEqual(additionalBytes));
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(additionalBytes)),
            delivery.GetProperty("Sha256").GetString());
    }

}
