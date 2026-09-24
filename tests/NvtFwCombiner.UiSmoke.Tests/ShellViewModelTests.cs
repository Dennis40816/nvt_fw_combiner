using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Owns one isolated Bootstrap graph for one concrete shell test group.</summary>
public sealed class ShellViewModelTestHostFixture
{
    internal CompositionHostServices Services { get; } = CompositionHostServices.Create(
        NvtFwCombiner.Infrastructure.Capabilities.BuiltInCanonicalCapabilityPolicy.Load);
}

/// <summary>Shared smoke-test support; each concrete group owns an isolated host fixture.</summary>
public abstract partial class ShellViewModelTestBase
{
    private protected ShellViewModelTestBase(ShellViewModelTestHostFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        TestHost = fixture.Services;
    }

    private protected CompositionHostServices TestHost { get; }

    private protected CanonicalCapabilityExperience TestProjection =>
        (CanonicalCapabilityExperience)TestHost.CompositionCapabilityExperience;
    internal static byte[] ReadCtrlRamReference()
    {
        System.Text.Json.JsonElement golden = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717");
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            golden.GetProperty("artifacts").EnumerateArray().Single(artifact =>
                artifact.GetProperty("role").GetString() == "expected")));
    }

    internal static byte[] ReadCtrlRamNormalSource()
    {
        System.Text.Json.JsonElement golden = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717");
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            golden.GetProperty("artifacts").EnumerateArray().Single(artifact =>
                artifact.GetProperty("role").GetString() == "input" &&
                artifact.GetProperty("originalFileName").GetString() == "Normal_Ctrlram.bin")));
    }
}
