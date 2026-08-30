using System.ComponentModel;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal static class LauncherReadyProtocol
{
    internal static string CreateExpectedPrefix(ManagedLauncherIdentity launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        string admissionDigest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(launcher.OwnerAdmissionIdentity)));
        return string.Join(
            ':',
            "READY-LAUNCHER",
            launcher.ProtocolVersion,
            launcher.OwnerAppVersion,
            admissionDigest,
            launcher.OwnerReleaseManifestSha256,
            launcher.Sha256);
    }

    internal static string Create(
        ManagedLauncherIdentity launcher,
        ManagedVersionAdmission readyAdmission)
    {
        ArgumentNullException.ThrowIfNull(readyAdmission);
        return string.Join(
            ':',
            CreateExpectedPrefix(launcher),
            readyAdmission.Version,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(readyAdmission.AdmissionIdentity)),
            readyAdmission.ReleaseManifestSha256);
    }

    internal static bool TryParse(
        string? value,
        string expectedPrefix,
        out ManagedVersionAdmission? admission)
    {
        admission = null;
        if (value is null || !value.StartsWith(expectedPrefix + ":", StringComparison.Ordinal))
        {
            return false;
        }
        string[] fields = value[(expectedPrefix.Length + 1)..].Split(':');
        if (fields.Length != 3 ||
            !ManagedAppVersion.TryParse(fields[0], out ManagedAppVersion version) ||
            !IsLowerSha256(fields[2]))
        {
            return false;
        }
        try
        {
            string identity = Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
            if (string.IsNullOrWhiteSpace(identity) || identity.Length > 2048)
            {
                return false;
            }
            admission = new(version, identity, fields[2]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static bool IsExpectedPrefix(string? value)
    {
        if (value is null)
        {
            return false;
        }
        string[] fields = value.Split(':');
        return fields.Length == 6 &&
               string.Equals(fields[0], "READY-LAUNCHER", StringComparison.Ordinal) &&
               int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int protocol) &&
               protocol == ManagedLauncherIdentity.SupportedProtocolVersion &&
               ManagedAppVersion.TryParse(fields[2], out _) &&
               IsLowerSha256(fields[3]) &&
               IsLowerSha256(fields[4]) &&
               IsLowerSha256(fields[5]);
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}

/// <summary>Classification of one consumed outer launcher READY inheritance context.</summary>
public enum LauncherReadyInheritanceOutcome
{
    /// <summary>The Launcher was started without immutable-Bootstrap supervision.</summary>
    NotInherited,
    /// <summary>Both inherited values are syntactically valid and may be used once.</summary>
    Inherited,
    /// <summary>The inherited pair was partial, blank, or malformed.</summary>
    InvalidInheritedContext,
}

/// <summary>One consumed outer READY inheritance context with no public handshake material.</summary>
public sealed class LauncherReadyInheritance
{
    private LauncherReadyInheritance(
        LauncherReadyInheritanceOutcome outcome,
        string? handle,
        string? expected)
    {
        Outcome = outcome;
        Handle = handle;
        Expected = expected;
    }

    /// <summary>Gets the complete inherited-context classification.</summary>
    public LauncherReadyInheritanceOutcome Outcome { get; }

    internal string? Handle { get; }
    internal string? Expected { get; }

    internal static LauncherReadyInheritance NotInherited { get; } =
        new(LauncherReadyInheritanceOutcome.NotInherited, null, null);

    internal static LauncherReadyInheritance Invalid { get; } =
        new(LauncherReadyInheritanceOutcome.InvalidInheritedContext, null, null);

    internal static LauncherReadyInheritance CreateInherited(string handle, string expected)
    {
        return new(LauncherReadyInheritanceOutcome.Inherited, handle, expected);
    }
}

/// <summary>Public host seam used only by the immutable Bootstrap executable.</summary>
public static class LauncherBootstrapRuntime
{
    private const string SeedStateFileName = "version-manager.seed.v1.json";

    /// <summary>Launcher exit code preserving an unconfirmed nested-process cleanup journal.</summary>
    public const int UnconfirmedTerminationExitCode = 17;

    /// <summary>Captures and authorizes Bootstrap custody before any managed-state access.</summary>
    public static async ValueTask<int> RunEntryAsync(
        string managedRoot,
        string statePath,
        CancellationToken cancellationToken)
    {
        using LauncherBootstrapStartupContext startup = CaptureStartup(statePath);
        return startup.Outcome == LauncherBootstrapStartupOutcome.InvalidInheritedContext
            ? 22
            : !await startup.WaitForStartAsync(cancellationToken).ConfigureAwait(false)
                ? 23
                : await RunCoreAsync(managedRoot, statePath, startup, cancellationToken)
                    .ConfigureAwait(false);
    }

    private static LauncherBootstrapStartupContext CaptureStartup(string statePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        bool lifetimeAdvertised = InheritedManagedProcessLifetime.IsContextAdvertised();
        IInheritedManagedProcessLifetimeCapture lifetime = InheritedManagedProcessLifetime.Capture(
            statePath,
            ManagedProcessLifetimeKind.Bootstrap,
            lifetimeAdvertised);
        BootstrapStartGate gate = BootstrapStartGate.Capture();
        BootstrapAdmissionSignal admission = BootstrapAdmissionSignal.Capture();
        ManagedImmutableBootstrapIdentity? identity =
            InheritedManagedBootstrapIdentityContext.CaptureAndClear();
        bool legacy = lifetime.Outcome == InheritedManagedProcessLifetimeOutcome.NotInherited &&
            gate.Outcome == BootstrapStartGateInheritanceOutcome.NotInherited &&
            admission.Outcome == BootstrapAdmissionInheritanceOutcome.NotInherited;
        bool inherited = lifetime.Outcome == InheritedManagedProcessLifetimeOutcome.Captured &&
            gate.Outcome == BootstrapStartGateInheritanceOutcome.Inherited &&
            admission.Outcome == BootstrapAdmissionInheritanceOutcome.Inherited;
        return new(
            legacy
                ? LauncherBootstrapStartupOutcome.LegacyDirect
                : inherited
                    ? LauncherBootstrapStartupOutcome.Inherited
                    : LauncherBootstrapStartupOutcome.InvalidInheritedContext,
            lifetime,
            gate,
            admission,
            inherited ? identity : null);
    }

    private static async ValueTask<int> RunCoreAsync(
        string managedRoot,
        string statePath,
        LauncherBootstrapStartupContext startup,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        string root = Path.GetFullPath(managedRoot);
        string state = Path.GetFullPath(statePath);
        var stateStore = new JsonVersionManagerStateStore(state);
        ManagedVersionSeedOutcome seedOutcome = await new ManagedVersionSeedBootstrapper(
                root,
                stateStore,
                new JsonVersionManagerStateStore(Path.Combine(root, SeedStateFileName)),
                new FileSystemManagedVersionRepository())
            .EnsureInitializedAsync(
                LauncherBootstrapCoordinator.StartupWriterLeaseTimeout,
                cancellationToken).ConfigureAwait(false);
        if (seedOutcome is not ManagedVersionSeedOutcome.ExistingState and
            not ManagedVersionSeedOutcome.Seeded)
        {
            return seedOutcome is ManagedVersionSeedOutcome.Busy
                ? 2
                : seedOutcome is ManagedVersionSeedOutcome.StateUnavailable
                    ? 18
                    : seedOutcome is ManagedVersionSeedOutcome.ManagedRootMismatch
                        ? 11
                        : 10;
        }
        var coordinator = new LauncherBootstrapCoordinator(
            root,
            state,
            stateStore,
            new JsonLauncherBootstrapStateStore(state),
            new FileSystemInstalledLauncherRepository(),
            new AnonymousPipeManagedLauncherProcess(
                ManagedProcessTermination.Instance,
                startup.Admission,
                inheritedBootstrapIdentity: startup.Identity));
        LauncherBootstrapResult result = await coordinator.RunAsync(cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            LauncherBootstrapOutcome.Ready => 0,
            LauncherBootstrapOutcome.RolledBack => 1,
            LauncherBootstrapOutcome.Busy => 2,
            LauncherBootstrapOutcome.InvalidState => 10,
            LauncherBootstrapOutcome.ManagedRootMismatch => 11,
            LauncherBootstrapOutcome.AppMutationPending => 12,
            LauncherBootstrapOutcome.DamagedLauncher => 13,
            LauncherBootstrapOutcome.ProtocolMismatch => 14,
            LauncherBootstrapOutcome.StartFailed => 15,
            LauncherBootstrapOutcome.RollbackUnavailable => 16,
            LauncherBootstrapOutcome.StateChanged => 17,
            LauncherBootstrapOutcome.StateUnavailable => 18,
            LauncherBootstrapOutcome.TerminationUnconfirmed => 19,
            _ => 99,
        };
    }

    /// <summary>Consumes and classifies the outer READY environment before a nested process can inherit it.</summary>
    public static LauncherReadyInheritance CaptureNestedReadyContext()
    {
        string? handle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? expected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment, null);
        Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment, null);
        return handle is null && expected is null
            ? LauncherReadyInheritance.NotInherited
            : !string.IsNullOrWhiteSpace(handle) &&
               long.TryParse(handle, NumberStyles.None, CultureInfo.InvariantCulture, out long pipeHandle) &&
               pipeHandle > 0 &&
               LauncherReadyProtocol.IsExpectedPrefix(expected)
                ? LauncherReadyInheritance.CreateInherited(handle, expected!)
                : LauncherReadyInheritance.Invalid;
    }

    /// <summary>Reports nested app readiness only after reloading exact app and launcher identities.</summary>
    public static async ValueTask<bool> ReportNestedReadyAsync(
        LauncherReadyInheritance context,
        string managedRoot,
        string statePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Outcome != LauncherReadyInheritanceOutcome.Inherited)
        {
            return false;
        }
        string handle = context.Handle!;
        string expected = context.Expected!;

        try
        {
            var appStateStore = new JsonVersionManagerStateStore(statePath);
            using VersionManagerWriteLeaseResult lease = await appStateStore.TryAcquireWriteLeaseAsync(
                LauncherBootstrapCoordinator.StartupWriterLeaseTimeout,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return false;
            }

            VersionManagerStateLoadResult app = await appStateStore.LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            LauncherBootstrapStateLoadResult launcher = await new JsonLauncherBootstrapStateStore(statePath)
                .LoadAsync(cancellationToken).ConfigureAwait(false);
            ManagedLauncherIdentity? running = new[]
            {
                launcher.State?.Pending?.Candidate,
                launcher.State?.Pending?.PreviousLastKnownGood,
                launcher.State?.Active,
            }.FirstOrDefault(identity =>
                identity is not null &&
                string.Equals(expected, LauncherReadyProtocol.CreateExpectedPrefix(identity), StringComparison.Ordinal));
            ManagedVersionAdmission? admission = app.State?.Admissions.SingleOrDefault(
                value => value.Version == app.State.ActiveVersion);
            if (!app.IsSuccess || !launcher.IsSuccess ||
                !app.State!.IsBoundToManagedRoot(managedRoot) ||
                !launcher.State!.IsBoundToManagedRoot(managedRoot) ||
                app.State!.PendingActivation is not null || app.State.PendingMutation is not null ||
                running is null || admission is null)
            {
                return false;
            }

            await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
            byte[] message = Encoding.UTF8.GetBytes(LauncherReadyProtocol.Create(running, admission) + "\n");
            await pipe.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or Win32Exception)
        {
            return false;
        }
    }

    private enum LauncherBootstrapStartupOutcome
    {
        LegacyDirect,
        Inherited,
        InvalidInheritedContext,
    }

    private sealed class LauncherBootstrapStartupContext : IDisposable
    {
        private readonly BootstrapStartGate _gate;
        private readonly IInheritedManagedProcessLifetimeCapture _lifetime;

        internal LauncherBootstrapStartupContext(
            LauncherBootstrapStartupOutcome outcome,
            IInheritedManagedProcessLifetimeCapture lifetime,
            BootstrapStartGate gate,
            BootstrapAdmissionSignal admission,
            ManagedImmutableBootstrapIdentity? identity)
        {
            Outcome = outcome;
            _lifetime = lifetime;
            _gate = gate;
            Admission = admission;
            Identity = identity;
        }

        internal LauncherBootstrapStartupOutcome Outcome { get; }
        internal BootstrapAdmissionSignal Admission { get; }
        internal ManagedImmutableBootstrapIdentity? Identity { get; }

        internal ValueTask<bool> WaitForStartAsync(CancellationToken cancellationToken)
        {
            return Outcome switch
            {
                LauncherBootstrapStartupOutcome.LegacyDirect => ValueTask.FromResult(true),
                LauncherBootstrapStartupOutcome.Inherited => _gate.WaitForStartAsync(cancellationToken),
                LauncherBootstrapStartupOutcome.InvalidInheritedContext => ValueTask.FromResult(false),
                _ => throw new InvalidOperationException("Bootstrap startup outcome is undefined."),
            };
        }

        public void Dispose()
        {
            Admission.Dispose();
            _gate.Dispose();
            _lifetime.Dispose();
        }
    }
}
