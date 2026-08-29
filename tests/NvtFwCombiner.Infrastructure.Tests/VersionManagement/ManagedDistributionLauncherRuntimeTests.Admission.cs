using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    /// <summary>Missing descriptor bytes are unavailable; present invalid bytes fail strict admission.</summary>
    [Theory]
    [InlineData("missing", ManagedDistributionPayloadIssue.Unavailable)]
    [InlineData("empty", ManagedDistributionPayloadIssue.Invalid)]
    [InlineData("oversized", ManagedDistributionPayloadIssue.Invalid)]
    [InlineData("malformed", ManagedDistributionPayloadIssue.Invalid)]
    public async Task EmbeddedPayloadSourceClassifiesDescriptorPresence(
        string descriptorKind,
        ManagedDistributionPayloadIssue expected)
    {
        ReadOnlyMemory<byte>? descriptor = descriptorKind switch
        {
            "missing" => (ReadOnlyMemory<byte>?)null,
            "empty" => ReadOnlyMemory<byte>.Empty,
            "oversized" => new byte[(64 * 1024) + 1],
            "malformed" => "{}"u8.ToArray(),
            _ => throw new InvalidOperationException("Undefined descriptor fixture."),
        };
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor,
            "bootstrap"u8.ToArray());

        ManagedDistributionPayloadInspectionResult result = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(result.Identity);
        Assert.Equal(expected, result.Issue);
    }

    /// <summary>Missing Bootstrap bytes are unavailable; a present empty resource is invalid.</summary>
    [Theory]
    [InlineData(true, ManagedDistributionPayloadIssue.Unavailable)]
    [InlineData(false, ManagedDistributionPayloadIssue.Invalid)]
    public async Task EmbeddedPayloadSourceClassifiesBootstrapPresence(
        bool missing,
        ManagedDistributionPayloadIssue expected)
    {
        byte[] declaredBootstrap = "declared-bootstrap"u8.ToArray();
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            PayloadDescriptor(declaredBootstrap),
            missing ? (ReadOnlyMemory<byte>?)null : ReadOnlyMemory<byte>.Empty);

        ManagedDistributionPayloadInspectionResult result = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(result.Identity);
        Assert.Equal(expected, result.Issue);
    }

    /// <summary>Root-admission failures retain their typed user-facing recovery direction.</summary>
    [Theory]
    [InlineData(
        ManagedInstallationRootStatus.InvalidDestination,
        ManagedFirstInstallationMaterializationIssue.InvalidDestination)]
    [InlineData(
        ManagedInstallationRootStatus.PermissionDenied,
        ManagedFirstInstallationMaterializationIssue.PermissionDenied)]
    [InlineData(
        ManagedInstallationRootStatus.Unavailable,
        ManagedFirstInstallationMaterializationIssue.StateUnavailable)]
    [InlineData(
        ManagedInstallationRootStatus.Present,
        ManagedFirstInstallationMaterializationIssue.RecoveryRequired)]
    [InlineData(
        ManagedInstallationRootStatus.Residue,
        ManagedFirstInstallationMaterializationIssue.RecoveryRequired)]
    public void MaterializerRetainsRootAdmissionIssue(
        ManagedInstallationRootStatus status,
        ManagedFirstInstallationMaterializationIssue expected)
    {
        Assert.Equal(
            expected,
            FileSystemManagedFirstInstallationRootMaterializer.MapObservedRoot(status));
    }

    /// <summary>Internal state and payload failures never masquerade as a destination problem.</summary>
    [Theory]
    [InlineData("state", ManagedFirstInstallationMaterializationIssue.StateUnavailable)]
    [InlineData("opaque", ManagedFirstInstallationMaterializationIssue.SourceChanged)]
    [InlineData("bootstrap-name", ManagedFirstInstallationMaterializationIssue.SourceChanged)]
    public async Task MaterializerRetainsInternalAdmissionIssue(
        string failureKind,
        ManagedFirstInstallationMaterializationIssue expected)
    {
        await using TemporaryRoot temporary = new();
        string root = Path.Combine(temporary.Path, "NvtFwCombiner");
        FreshInstallationCandidate candidate = Candidate(temporary.Path);
        ManagedVersionAdmission admission = Admission(candidate);
        var repository = new MaterializingRepository(admission);
        var materializer = new FileSystemManagedFirstInstallationRootMaterializer(repository);
        using IManagedDistributionPayloadCapture payload = failureKind switch
        {
            "state" => new TestPayloadCapture(PayloadIdentity()),
            "opaque" => new OpaquePayloadCapture(PayloadIdentity()),
            "bootstrap-name" => new TestPayloadCapture(PayloadIdentity("Unexpected.Bootstrap.exe")),
            _ => throw new InvalidOperationException("Undefined admission failure fixture."),
        };
        string statePath = failureKind == "state"
            ? "relative-state.json"
            : Path.Combine(temporary.Path, "state", "version-manager.v1.json");

        ManagedFirstInstallationMaterializationResult result = await materializer.MaterializeAsync(
            root,
            statePath,
            payload,
            candidate,
            ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(admission),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Installation);
        Assert.Equal(expected, result.Issue);
        Assert.Equal(0, repository.InstallCount);
        Assert.False(Directory.Exists(root));
    }
}
