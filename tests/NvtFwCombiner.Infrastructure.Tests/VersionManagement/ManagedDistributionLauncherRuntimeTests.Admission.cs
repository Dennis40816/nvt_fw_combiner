using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class ManagedDistributionLauncherRuntimeTests
{
    /// <summary>A non-seekable descriptor is stopped at the 64 KiB ceiling plus one probe.</summary>
    [Fact]
    public async Task OversizedNonSeekableDescriptorStopsAtMaximumPlusOne()
    {
        var descriptor = new TrackingResource(new byte[(64 * 1024) + 4096])
        {
            CanSeek = false,
        };
        var bootstrap = new TrackingResource("bootstrap"u8.ToArray());
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor.Open,
            bootstrap.Open);

        ManagedDistributionPayloadEntryAdmissionResult result = await source.AdmitEntryAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedDistributionPayloadIssue.Invalid, result.Issue);
        Assert.Equal((64 * 1024) + 1, descriptor.BytesRead);
        Assert.Equal(0, bootstrap.OpenCount);
    }

    /// <summary>An exact 64 KiB valid descriptor remains admitted at the inclusive boundary.</summary>
    [Fact]
    public async Task ExactMaximumDescriptorIsAdmittedWithoutBootstrapContentRead()
    {
        byte[] bootstrapBytes = "bootstrap"u8.ToArray();
        byte[] canonical = PayloadDescriptor(bootstrapBytes);
        var exact = new byte[64 * 1024];
        canonical.CopyTo(exact, 0);
        exact.AsSpan(canonical.Length).Fill((byte)' ');
        var descriptor = new TrackingResource(exact)
        {
            CanSeek = false,
        };
        var bootstrap = new TrackingResource(bootstrapBytes);
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor.Open,
            bootstrap.Open);

        ManagedDistributionPayloadEntryAdmissionResult result = await source.AdmitEntryAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(64 * 1024, descriptor.BytesRead);
        Assert.Equal(1, bootstrap.OpenCount);
        Assert.Equal(1, bootstrap.LengthReadCount);
        Assert.Equal(0, bootstrap.BytesRead);
    }

    /// <summary>The canonical 200 MB Bootstrap ceiling is admitted from metadata without content reads.</summary>
    [Fact]
    public async Task CanonicalMaximumBootstrapIsAdmittedWithoutReadingContent()
    {
        var descriptor = new TrackingResource(PayloadDescriptor(
            ManagedImmutableBootstrapIdentity.MaximumExecutableBytes,
            HashA));
        var bootstrap = new TrackingResource([0x01])
        {
            ReportedLength = ManagedImmutableBootstrapIdentity.MaximumExecutableBytes,
        };
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor.Open,
            bootstrap.Open);

        ManagedDistributionPayloadEntryAdmissionResult result = await source.AdmitEntryAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ManagedImmutableBootstrapIdentity.MaximumExecutableBytes, result.Bootstrap?.Length);
        Assert.Equal(0, bootstrap.BytesRead);
    }

    /// <summary>An oversized declared Bootstrap fails before the Bootstrap resource is opened.</summary>
    [Fact]
    public async Task BootstrapAboveCanonicalMaximumFailsBeforeResourceOpen()
    {
        var descriptor = new TrackingResource(PayloadDescriptor(
            ManagedImmutableBootstrapIdentity.MaximumExecutableBytes + 1,
            HashA));
        var bootstrap = new TrackingResource([0x01]);
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor.Open,
            bootstrap.Open);

        ManagedDistributionPayloadEntryAdmissionResult result = await source.AdmitEntryAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedDistributionPayloadIssue.Invalid, result.Issue);
        Assert.Equal(0, bootstrap.OpenCount);
        Assert.Equal(0, bootstrap.BytesRead);
    }

    /// <summary>Declared and embedded Bootstrap lengths must match before entry routing.</summary>
    [Fact]
    public async Task BootstrapLengthMismatchFailsWithoutReadingContent()
    {
        byte[] declared = "declared-bootstrap"u8.ToArray();
        var bootstrap = new TrackingResource(declared)
        {
            ReportedLength = declared.LongLength - 1,
        };
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            new TrackingResource(PayloadDescriptor(declared)).Open,
            bootstrap.Open);

        ManagedDistributionPayloadEntryAdmissionResult result = await source.AdmitEntryAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedDistributionPayloadIssue.Invalid, result.Issue);
        Assert.Equal(0, bootstrap.BytesRead);
    }

    /// <summary>Setup inspection rejects a stream that reports the expected length but ends early.</summary>
    [Fact]
    public async Task SetupInspectionRejectsTruncatedBootstrap()
    {
        byte[] declared = "expected-bootstrap"u8.ToArray();
        var bootstrap = new TrackingResource(declared[..^1])
        {
            ReportedLength = declared.LongLength,
        };
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            new TrackingResource(PayloadDescriptor(declared)).Open,
            bootstrap.Open);

        ManagedDistributionPayloadInspectionResult result = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedDistributionPayloadIssue.Invalid, result.Issue);
        Assert.Equal(declared.Length - 1, bootstrap.BytesRead);
    }

    /// <summary>Setup inspection rejects exact-length Bootstrap content with the wrong digest.</summary>
    [Fact]
    public async Task SetupInspectionRejectsBootstrapHashMismatch()
    {
        byte[] declared = "expected-bootstrap"u8.ToArray();
        byte[] changed = [.. declared];
        changed[0] ^= 0xFF;
        var bootstrap = new TrackingResource(changed);
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            new TrackingResource(PayloadDescriptor(declared)).Open,
            bootstrap.Open);

        ManagedDistributionPayloadInspectionResult result = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedDistributionPayloadIssue.Invalid, result.Issue);
        Assert.Equal(changed.Length, bootstrap.BytesRead);
    }

    /// <summary>Setup inspection probes past the declared length and rejects hidden extra bytes.</summary>
    [Fact]
    public async Task SetupInspectionRejectsBootstrapBytesBeyondDeclaredLength()
    {
        byte[] declared = "expected-bootstrap"u8.ToArray();
        byte[] extra = [.. declared, 0xA5];
        var bootstrap = new TrackingResource(extra)
        {
            ReportedLength = declared.LongLength,
        };
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            new TrackingResource(PayloadDescriptor(declared)).Open,
            bootstrap.Open);

        ManagedDistributionPayloadInspectionResult result = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManagedDistributionPayloadIssue.Invalid, result.Issue);
        Assert.Equal(extra.Length, bootstrap.BytesRead);
    }

    /// <summary>Bootstrap drift after Setup review is Changed, never a mixed exact capture.</summary>
    [Fact]
    public async Task CaptureRejectsBootstrapChangedAfterInspection()
    {
        await using TemporaryRoot temporary = new();
        string probe = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string launcher = Path.Combine(temporary.Path, "distribution.exe");
        File.Copy(probe, launcher);
        byte[] original = "expected-bootstrap"u8.ToArray();
        byte[] changed = [.. original];
        changed[0] ^= 0xFF;
        var originalResource = new TrackingResource(original);
        var changedResource = new TrackingResource(changed);
        int bootstrapOpenCount = 0;
        var source = new EmbeddedManagedDistributionPayloadSource(
            launcher,
            new TrackingResource(PayloadDescriptor(original)).Open,
            () => ++bootstrapOpenCount <= 2
                ? originalResource.Open()
                : changedResource.Open());
        ManagedDistributionPayloadInspectionResult inspected = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        ManagedDistributionPayloadCaptureResult captured = await source.CaptureExactAsync(
            Assert.IsType<ManagedDistributionPayloadIdentity>(inspected.Identity),
            TestContext.Current.CancellationToken);

        Assert.True(inspected.IsSuccess);
        Assert.Null(captured.Capture);
        Assert.Equal(ManagedDistributionPayloadIssue.Changed, captured.Issue);
        Assert.Equal(4, bootstrapOpenCount);
    }

    /// <summary>Descriptor drift after Setup review is Changed before payload capture.</summary>
    [Fact]
    public async Task CaptureRejectsDescriptorChangedAfterInspection()
    {
        await using TemporaryRoot temporary = new();
        string probe = Path.Combine(
            AppContext.BaseDirectory,
            "ready-probe",
            "NvtFwCombiner.ReadyProbe.exe");
        string launcher = Path.Combine(temporary.Path, "distribution.exe");
        File.Copy(probe, launcher);
        byte[] bootstrap = "expected-bootstrap"u8.ToArray();
        var originalDescriptor = new TrackingResource(PayloadDescriptor(bootstrap));
        var changedDescriptor = new TrackingResource(PayloadDescriptor(
            bootstrap.LongLength,
            Hash(bootstrap),
            new string('d', 40)));
        int descriptorOpenCount = 0;
        var source = new EmbeddedManagedDistributionPayloadSource(
            launcher,
            () => ++descriptorOpenCount == 1
                ? originalDescriptor.Open()
                : changedDescriptor.Open(),
            new TrackingResource(bootstrap).Open);
        ManagedDistributionPayloadInspectionResult inspected = await source.InspectAsync(
            TestContext.Current.CancellationToken);

        ManagedDistributionPayloadCaptureResult captured = await source.CaptureExactAsync(
            Assert.IsType<ManagedDistributionPayloadIdentity>(inspected.Identity),
            TestContext.Current.CancellationToken);

        Assert.True(inspected.IsSuccess);
        Assert.Null(captured.Capture);
        Assert.Equal(ManagedDistributionPayloadIssue.Changed, captured.Issue);
        Assert.Equal(2, descriptorOpenCount);
    }

    /// <summary>Resource access failures remain typed unavailable instead of escaping.</summary>
    [Theory]
    [InlineData("descriptor-open")]
    [InlineData("bootstrap-length")]
    [InlineData("bootstrap-read")]
    public async Task ResourceAccessFailuresRemainTypedUnavailable(string failure)
    {
        byte[] declared = "declared-bootstrap"u8.ToArray();
        var descriptor = new TrackingResource(PayloadDescriptor(declared))
        {
            OpenException = failure == "descriptor-open" ? static () => new IOException("open") : null,
        };
        var bootstrap = new TrackingResource(declared)
        {
            LengthException = failure == "bootstrap-length"
                ? static () => new UnauthorizedAccessException("length")
                : null,
            ReadException = failure == "bootstrap-read" ? static () => new IOException("read") : null,
        };
        var source = new EmbeddedManagedDistributionPayloadSource(
            Path.Combine(Path.GetTempPath(), "missing-distribution-launcher.exe"),
            descriptor.Open,
            bootstrap.Open);

        ManagedDistributionPayloadIssue issue = failure == "bootstrap-read"
            ? (await source.InspectAsync(TestContext.Current.CancellationToken)).Issue
            : (await source.AdmitEntryAsync(TestContext.Current.CancellationToken)).Issue;

        Assert.Equal(ManagedDistributionPayloadIssue.Unavailable, issue);
    }

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
