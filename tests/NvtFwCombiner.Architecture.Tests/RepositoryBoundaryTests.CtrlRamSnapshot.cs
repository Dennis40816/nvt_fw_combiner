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

        Assert.Contains("baseBytes = TryReadFirmwareImage(basePath)", context, StringComparison.Ordinal);
        Assert.Contains("FirmwareArtifactPayload referencePayload", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes(context.BasePath!)", v2, StringComparison.Ordinal);
        Assert.Contains("byte[] referenceBytes = context.BaseBytes", runner, StringComparison.Ordinal);
        Assert.Contains("referencePayload.Sha256", runner, StringComparison.Ordinal);
        Assert.Contains("virtualArtifacts: new Dictionary<string, byte[]>(StringComparer.Ordinal)", runner, StringComparison.Ordinal);
        Assert.Contains("[context.BasePath!] = referenceBytes", runner, StringComparison.Ordinal);
    }
}
