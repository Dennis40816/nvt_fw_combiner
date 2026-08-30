using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Behavioral coverage for the single public Launcher composition graph.</summary>
public sealed class ManagedDistributionLauncherHostServicesTests
{
    /// <summary>Development builds without release payloads stop before local routing.</summary>
    [Fact]
    public async Task MissingDevelopmentPayloadFailsClosedBeforeEntryRouting()
    {
        using var workspace = TempWorkspace.Create();
        using ManagedDistributionLauncherHostServices host =
            ManagedDistributionLauncherHostServices.Create(
                workspace.PathFor("NvtFwCombiner.DistributionLauncher.exe"),
                "1.0.3",
                _ => null,
                _ => null,
                workspace.PathFor("state/version-manager.v1.json"));

        ManagedDistributionLauncherHostResult result = await host.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(result.Entry);
        Assert.Null(result.Setup);
        Assert.Equal(ManagedDistributionPayloadIssue.Unavailable, result.PayloadIssue);
    }

    /// <summary>Only genuine absence returns the exact shared Setup experience.</summary>
    [Fact]
    public async Task GenuineFirstInstallExposesOnlyTheSharedSetupExperience()
    {
        using var workspace = TempWorkspace.Create();
        byte[] bootstrap = "immutable-bootstrap"u8.ToArray();
        Dictionary<string, byte[]> resources = new(StringComparer.Ordinal)
        {
            [ManagedDistributionLauncherHostServices.PayloadAdmissionResourceName] =
                PayloadDescriptor(bootstrap),
            [ManagedDistributionLauncherHostServices.BootstrapResourceName] = bootstrap,
        };
        string launcher = workspace.PathFor("delivery/NvtFwCombiner.DistributionLauncher.exe");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(launcher)!);
        var bootstrapReads = new BootstrapReadGuard(bootstrap);
        using ManagedDistributionLauncherHostServices host =
            ManagedDistributionLauncherHostServices.Create(
                launcher,
                "1.0.3",
                name => name == ManagedDistributionLauncherHostServices.BootstrapResourceName
                    ? bootstrapReads.Open()
                    : OpenResource(resources, name),
                _ => null,
                workspace.PathFor("state/version-manager.v1.json"));

        ManagedDistributionLauncherHostResult result = await host.RunAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            workspace.PathFor("delivery/NvtFwCombiner"),
            host.ManagedRoot);
        Assert.Equal(ManagedLauncherEntryOutcome.SetupRequired, result.Entry?.Outcome);
        Assert.NotNull(result.Setup);
        Assert.False(bootstrapReads.ReadAttempted);
    }

    /// <summary>The host consumes the canonical locator resolver rather than adding defaults.</summary>
    [Fact]
    public void FactoryUsesTheExternalOverrideWithoutAddingAnotherRegistryDefault()
    {
        using var workspace = TempWorkspace.Create();
        byte[] bootstrap = "immutable-bootstrap"u8.ToArray();
        Dictionary<string, byte[]> resources = new(StringComparer.Ordinal)
        {
            [ManagedDistributionLauncherHostServices.PayloadAdmissionResourceName] =
                PayloadDescriptor(bootstrap),
            [ManagedDistributionLauncherHostServices.BootstrapResourceName] = bootstrap,
        };
        string configuredRegistry = workspace.PathFor("registry/update-source-registry.json");
        string? observedEnvironmentName = null;
        using ManagedDistributionLauncherHostServices host =
            ManagedDistributionLauncherHostServices.Create(
                workspace.PathFor("NvtFwCombiner.DistributionLauncher.exe"),
                "1.0.3",
                name => OpenResource(resources, name),
                name =>
                {
                    observedEnvironmentName = name;
                    return configuredRegistry;
                },
                workspace.PathFor("state/version-manager.v1.json"));

        Assert.Equal("NFC_UPDATE_SOURCE_REGISTRY_PATH", observedEnvironmentName);
    }

    /// <summary>Each incomplete or invalid closed resource shape fails before entry routing.</summary>
    [Theory]
    [InlineData("descriptor-only", ManagedDistributionPayloadIssue.Unavailable)]
    [InlineData("bootstrap-only", ManagedDistributionPayloadIssue.Unavailable)]
    [InlineData("malformed-descriptor", ManagedDistributionPayloadIssue.Invalid)]
    [InlineData("oversized-descriptor", ManagedDistributionPayloadIssue.Invalid)]
    [InlineData("launcher-version-mismatch", ManagedDistributionPayloadIssue.Invalid)]
    public async Task ClosedResourcesAreBoundedAndVersionBoundBeforeEntryRouting(
        string shape,
        ManagedDistributionPayloadIssue expectedIssue)
    {
        using var workspace = TempWorkspace.Create();
        byte[] bootstrap = "immutable-bootstrap"u8.ToArray();
        byte[]? descriptor = shape switch
        {
            "bootstrap-only" => null,
            "malformed-descriptor" => "{}"u8.ToArray(),
            "oversized-descriptor" => new byte[(64 * 1024) + 1],
            "launcher-version-mismatch" => PayloadDescriptor(bootstrap, "1.0.4"),
            _ => PayloadDescriptor(bootstrap),
        };
        byte[]? embeddedBootstrap = shape == "descriptor-only" ? null : bootstrap;
        var requestedResources = new List<string>();
        using ManagedDistributionLauncherHostServices host =
            ManagedDistributionLauncherHostServices.Create(
                workspace.PathFor("NvtFwCombiner.DistributionLauncher.exe"),
                "1.0.3",
                name =>
                {
                    requestedResources.Add(name);
                    return name switch
                    {
                        ManagedDistributionLauncherHostServices.PayloadAdmissionResourceName =>
                            OpenResource(descriptor),
                        ManagedDistributionLauncherHostServices.BootstrapResourceName =>
                            OpenResource(embeddedBootstrap),
                        _ => throw new InvalidOperationException("Unexpected resource request."),
                    };
                },
                _ => null,
                workspace.PathFor("state/version-manager.v1.json"));

        Assert.Empty(requestedResources);
        ManagedDistributionLauncherHostResult result = await host.RunAsync(
            TestContext.Current.CancellationToken);

        string[] expectedResources = shape is "descriptor-only" or "launcher-version-mismatch"
            ?
            [
                ManagedDistributionLauncherHostServices.PayloadAdmissionResourceName,
                ManagedDistributionLauncherHostServices.BootstrapResourceName,
            ]
            : [ManagedDistributionLauncherHostServices.PayloadAdmissionResourceName];
        Assert.Equal(expectedResources, requestedResources);
        Assert.Null(result.Entry);
        Assert.Null(result.Setup);
        Assert.Equal(expectedIssue, result.PayloadIssue);
    }

    /// <summary>Every declared host-result tuple is classified against one exact admitted set.</summary>
    [Fact]
    public void ExitCodeMapperExhaustivelyClassifiesEveryDeclaredTuple()
    {
        var valid = new Dictionary<
            (ManagedDistributionPayloadIssue, ManagedLauncherEntryOutcome?, bool),
            NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode>
        {
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.LaunchInstalled, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.LaunchInstalled,
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.SetupRequired, true)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.SetupRequired,
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.RecoveryRequired, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.RecoveryRequired,
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.Busy, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.Busy,
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.HealthUnavailable, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.HealthUnavailable,
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.LaunchFailed, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.LaunchFailed,
            [(ManagedDistributionPayloadIssue.Unavailable, null, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.PayloadUnavailable,
            [(ManagedDistributionPayloadIssue.Invalid, null, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.PayloadInvalid,
            [(ManagedDistributionPayloadIssue.None, ManagedLauncherEntryOutcome.TerminationUnconfirmed, false)] =
                NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.TerminationUnconfirmed,
        };
        ManagedDistributionPayloadIssue[] payloadIssues =
            Enum.GetValues<ManagedDistributionPayloadIssue>();
        ManagedLauncherEntryOutcome?[] entryOutcomes =
        [
            null,
            .. Enum.GetValues<ManagedLauncherEntryOutcome>()
                .Select(static outcome => (ManagedLauncherEntryOutcome?)outcome),
        ];
        bool[] setupStates = [false, true];
        int observed = 0;
        int invalid = 0;

        foreach (ManagedDistributionPayloadIssue payloadIssue in payloadIssues)
        {
            foreach (ManagedLauncherEntryOutcome? entryOutcome in entryOutcomes)
            {
                foreach (bool hasSetup in setupStates)
                {
                    (ManagedDistributionPayloadIssue, ManagedLauncherEntryOutcome?, bool) key =
                        (payloadIssue, entryOutcome, hasSetup);
                    bool isValid = valid.TryGetValue(
                        key,
                        out NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode expected);
                    if (!isValid)
                    {
                        expected = NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode
                            .HostUnavailable;
                        invalid++;
                    }

                    NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode actual =
                        NvtFwCombiner.DistributionLauncher.Program.MapExitCode(
                            payloadIssue,
                            entryOutcome,
                            hasSetup);

                    Assert.True(
                        actual == expected,
                        $"Unexpected mapping for ({payloadIssue}, {entryOutcome?.ToString() ?? "null"}, {hasSetup}).");
                    observed++;
                }
            }
        }

        Assert.Equal(payloadIssues.Length * entryOutcomes.Length * 2, observed);
        Assert.Equal(valid.Count, observed - invalid);
    }

    /// <summary>Undefined payload and entry enum values remain outside every admitted tuple.</summary>
    [Theory]
    [InlineData(999, null, false)]
    [InlineData((int)ManagedDistributionPayloadIssue.None, 999, false)]
    [InlineData(999, 999, true)]
    public void ExitCodeMapperRejectsUndefinedEnumSentinels(
        int payloadIssue,
        int? entryOutcome,
        bool hasSetup)
    {
        ManagedLauncherEntryOutcome? outcome = entryOutcome.HasValue
            ? (ManagedLauncherEntryOutcome)entryOutcome.Value
            : null;

        NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode actual =
            NvtFwCombiner.DistributionLauncher.Program.MapExitCode(
                (ManagedDistributionPayloadIssue)payloadIssue,
                outcome,
                hasSetup);

        Assert.Equal(
            NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode.HostUnavailable,
            actual);
    }

    /// <summary>Every declared process exit code has one unique numeric identity.</summary>
    [Fact]
    public void DistributionLauncherExitCodeNumericValuesAreUnique()
    {
        NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode[] values =
            Enum.GetValues<NvtFwCombiner.DistributionLauncher.DistributionLauncherExitCode>();

        Assert.Equal(values.Length, values.Select(static value => (int)value).Distinct().Count());
    }

    private static byte[] PayloadDescriptor(byte[] bootstrap, string launcherVersion = "1.0.3")
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "1.0",
            product = "NVT FW Combiner",
            payloadKind = "distribution-launcher-bootstrap",
            launcherSetupProtocolVersion = 1,
            launcherVersion,
            runtimeIdentifier = "win-x64",
            sourceCommit = new string('c', 40),
            bootstrap = new
            {
                resourceName = ManagedDistributionLauncherHostServices.BootstrapResourceName,
                installedFileName = "NvtFwCombiner.Bootstrap.exe",
                size = bootstrap.LongLength,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(bootstrap)),
                versionManagementProtocolVersion = 1,
                sourceCommit = new string('c', 40),
            },
        });
    }

    private static MemoryStream? OpenResource(
        Dictionary<string, byte[]> resources,
        string name)
    {
        return resources.TryGetValue(name, out byte[]? bytes) ? OpenResource(bytes) : null;
    }

    private static MemoryStream? OpenResource(byte[]? bytes)
    {
        return bytes is null ? null : new MemoryStream(bytes, writable: false);
    }

    private sealed class BootstrapReadGuard(byte[] bytes)
    {
        internal bool ReadAttempted { get; private set; }

        internal MemoryStream Open()
        {
            return new GuardedMemoryStream(bytes, this);
        }

        private sealed class GuardedMemoryStream(byte[] bytes, BootstrapReadGuard owner)
            : MemoryStream(bytes, writable: false)
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                owner.ReadAttempted = true;
                throw new InvalidOperationException("Bootstrap content must remain lazy during entry.");
            }

            public override int Read(Span<byte> buffer)
            {
                owner.ReadAttempted = true;
                throw new InvalidOperationException("Bootstrap content must remain lazy during entry.");
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                owner.ReadAttempted = true;
                throw new InvalidOperationException("Bootstrap content must remain lazy during entry.");
            }
        }
    }
}
