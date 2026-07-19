namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Locks the exact NT51928 non-NB CtrlRAM V2 route without exposing it as a public registration.</summary>
    [Fact]
    public void Nt51928CtrlRamCandidateStaysExactAndPrivate()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string cli = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.cs");
        string runtime = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs");
        string profile = ReadText(
            "profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw132-twochip.json");
        string family = ReadText(
            "profiles/built-in/nt51928-ctrlram-replace-candidate/families/nt51928-ctrlram-replace.json");

        Assert.Contains("nt51928-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51928-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51928-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.Contains(
            "(\"NT51928\", \"nfc.nt51928.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, \"1.3.2\", 2, 0xF206)",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "5064b3134031adbd7ae292c9038d728da116d5a013a2463ae809694a07f87e0e",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-partial-family-route\"", profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51928.ctrlram-postbuild-v1", profile, StringComparison.Ordinal);
        Assert.Contains("nt51928-ctrlram-fw132-twochip-full-flash", profile, StringComparison.Ordinal);
        Assert.Contains("\"memberId\": \"NT51928\"", family, StringComparison.Ordinal);
        Assert.Contains("\"capacityBytes\": 524288", family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [\n              61958", family, StringComparison.Ordinal);
        Assert.Contains("ctrlram-replace-partial-family-51928-non-nb-to-51927", family, StringComparison.Ordinal);
    }
}
