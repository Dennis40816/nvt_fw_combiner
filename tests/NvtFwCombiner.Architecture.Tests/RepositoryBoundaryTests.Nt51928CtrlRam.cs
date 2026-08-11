namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class CanonicalCompositionBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Locks all owner-approved NT51928 non-NB CtrlRAM V2 plans without a public profile registration.</summary>
    [Fact]
    public void Nt51928CtrlRamCandidateStaysExactAndPrivate()
    {
        string packageTrustIndex = ReadText("profiles/built-in/package-trust-index.json");
        string registrations = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
        string cli = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs");
        string routes = ReadText("src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs");
        string profile = ReadText(
            "profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw132-twochip.json");
        string singleProfile = ReadText(
            "profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw141-single.json");
        string threeChipProfile = ReadText(
            "profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw140-threechip.json");
        string family = ReadText(
            "profiles/built-in/nt51928-ctrlram-replace-candidate/families/nt51928-ctrlram-replace.json");

        Assert.Contains("nt51928-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51928-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51928-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.Contains("\"icId\": \"NT51928\"", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("\"postbuildBranch\": \"two-chip\"", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("\"postbuildBranch\": \"single-chip\"", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("\"postbuildBranch\": \"three-chip\"", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("ParseBranch", routes, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-partial-family-route\"", profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51928.ctrlram-postbuild-v1", profile, StringComparison.Ordinal);
        Assert.Contains("nt51928-ctrlram-fw132-twochip-full-flash", profile, StringComparison.Ordinal);
        Assert.Contains("nt51928-ctrlram-fw141-single-full-flash", singleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51928-ctrlram-fw140-threechip-full-flash", threeChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"memberId\": \"NT51928\"", family, StringComparison.Ordinal);
        Assert.Contains("\"capacityBytes\": 524288", family, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", family, StringComparison.Ordinal);
        Assert.Contains("ctrlram-replace-partial-family-51928-non-nb-to-51927", family, StringComparison.Ordinal);
    }
}
