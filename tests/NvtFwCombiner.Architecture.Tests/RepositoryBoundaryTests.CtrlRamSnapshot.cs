namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Locks runtime-reference compilation and execution to one per-run base snapshot.</summary>
    [Fact]
    public void CtrlRamRuntimeReferenceRouteReusesItsCompilationBaseSnapshot()
    {
        string context = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.Context.cs");
        string v2 = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.V2.cs");
        string runner = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs");
        string snapshots = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.DiffDlm.cs");

        Assert.Contains("baseBytes = TryReadFirmwareImage(basePath)", context, StringComparison.Ordinal);
        Assert.Contains("FirmwareArtifactPayload referencePayload", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes(context.BasePath!)", v2, StringComparison.Ordinal);
        Assert.Contains("byte[] referenceBytes = context.BaseBytes", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredBaseSha256", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("referencePayload.Sha256", runner, StringComparison.Ordinal);
        Assert.Contains("CreateCtrlRamInputSnapshotsAsync(", runner, StringComparison.Ordinal);
        Assert.Contains("var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)", snapshots, StringComparison.Ordinal);
        Assert.Contains("[context.BasePath!] = context.BaseBytes!", snapshots, StringComparison.Ordinal);
    }
}
