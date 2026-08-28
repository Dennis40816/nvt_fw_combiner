using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Parent-acquired start lease plus named Windows job for one managed process tree.</summary>
internal sealed partial class ManagedProcessLifetimeLease : IDisposable
{
    internal const string ContextEnvironment = "NVT_FW_COMBINER_PROCESS_LIFETIME_CONTEXT";
    internal const string HandleEnvironment = "NVT_FW_COMBINER_PROCESS_LIFETIME_HANDLE";
    internal const string JobEnvironment = "NVT_FW_COMBINER_PROCESS_LIFETIME_JOB";
    internal const string StatePathEnvironment = "NVT_FW_COMBINER_PROCESS_LIFETIME_STATE_PATH";
    internal const string KindEnvironment = "NVT_FW_COMBINER_PROCESS_LIFETIME_KIND";
    internal const string ApplicationSuffix = ".application-lifetime.v1.lock";
    internal const string LauncherSuffix = ".launcher-lifetime.v1.lock";
    private const string ContextVersion = "v1";
    private const uint HandleFlagInherit = 1;

    private readonly SafeFileHandle _job;
    private readonly FileStream _stream;
    private readonly string _statePath;
    private readonly ManagedProcessLifetimeKind _kind;

    private ManagedProcessLifetimeLease(
        FileStream stream,
        SafeFileHandle job,
        string jobName,
        string statePath,
        ManagedProcessLifetimeKind kind)
    {
        _stream = stream;
        _job = job;
        JobName = jobName;
        _statePath = statePath;
        _kind = kind;
    }

    internal string InheritedHandle => _stream.SafeFileHandle.DangerousGetHandle()
        .ToInt64()
        .ToString(CultureInfo.InvariantCulture);
    internal string JobName { get; }

    internal void ApplyInheritedContext(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        startInfo.Environment[ContextEnvironment] = ContextVersion;
        startInfo.Environment[HandleEnvironment] = InheritedHandle;
        startInfo.Environment[JobEnvironment] = JobName;
        startInfo.Environment[StatePathEnvironment] = _statePath;
        startInfo.Environment[KindEnvironment] = _kind.ToString();
    }

    internal static ManagedProcessLifetimeLease? TryAcquire(
        string statePath,
        ManagedProcessLifetimeKind kind)
    {
        return TryAcquire(statePath, GetSuffix(kind));
    }

    internal static ManagedProcessLifetimeLease? TryAcquire(string statePath, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        FileStream? stream = null;
        SafeFileHandle? job = null;
        try
        {
            string normalizedStatePath = Path.GetFullPath(statePath);
            string path = normalizedStatePath + suffix;
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                throw new ArgumentException("Lifetime lease has no parent directory.", nameof(statePath)));
            stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            _ = SetHandleInformation(
                    stream.SafeFileHandle.DangerousGetHandle(),
                    HandleFlagInherit,
                    HandleFlagInherit)
                ? true
                : throw new Win32Exception(Marshal.GetLastPInvokeError());
            string jobName = GetJobName(statePath, suffix);
            job = OpenOrCreateJob(jobName);
            if (job is null)
            {
                return null;
            }
            var result = new ManagedProcessLifetimeLease(
                stream,
                job,
                jobName,
                normalizedStatePath,
                GetKind(suffix));
            stream = null;
            job = null;
            return result;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or Win32Exception)
        {
            return null;
        }
        finally
        {
            stream?.Dispose();
            job?.Dispose();
        }
    }

    internal static ManagedProcessLifetimeStatus GetStatus(string statePath, string suffix)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ManagedProcessLifetimeStatus.Unavailable;
        }
        ManagedProcessLifetimeStatus lease = GetLeaseStatus(statePath, suffix);
        ManagedProcessLifetimeStatus tree = GetTreeStatus(GetJobName(statePath, suffix));
        return lease == ManagedProcessLifetimeStatus.Active || tree == ManagedProcessLifetimeStatus.Active
            ? ManagedProcessLifetimeStatus.Active
            : lease == ManagedProcessLifetimeStatus.Unavailable || tree == ManagedProcessLifetimeStatus.Unavailable
                ? ManagedProcessLifetimeStatus.Unavailable
                : ManagedProcessLifetimeStatus.Exited;
    }

    internal static ManagedProcessLifetimeStatus GetStatus(
        string statePath,
        ManagedProcessLifetimeKind kind)
    {
        return GetStatus(statePath, GetSuffix(kind));
    }

    internal bool TerminateTreeAndConfirmEmpty(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (!TerminateJobObject(_job.DangerousGetHandle(), exitCode: 1))
        {
            return false;
        }
        long deadline = Environment.TickCount64 + checked((long)Math.Ceiling(timeout.TotalMilliseconds));
        while (Environment.TickCount64 <= deadline)
        {
            if (!TryGetActiveProcessCount(_job, out uint active))
            {
                return false;
            }
            if (active == 0)
            {
                return true;
            }
            Thread.Sleep(25);
        }
        return false;
    }

    /// <summary>Stops closing the accepted READY job from terminating its process tree.</summary>
    internal bool TryReleaseAcceptedTree()
    {
        var limits = new JobExtendedLimitInformation();
        return SetInformationJobObject(
            _job.DangerousGetHandle(),
            JobObjectExtendedLimitInformation,
            in limits,
            Marshal.SizeOf<JobExtendedLimitInformation>());
    }

    internal static InheritedManagedProcessLifetimeCapture CaptureInherited(
        string? statePath,
        ManagedProcessLifetimeKind kind,
        bool managedContextAdvertised)
    {
        string? context = TakeEnvironment(ContextEnvironment);
        string? value = TakeEnvironment(HandleEnvironment);
        string? jobName = TakeEnvironment(JobEnvironment);
        string? advertisedStatePath = TakeEnvironment(StatePathEnvironment);
        string? advertisedKind = TakeEnvironment(KindEnvironment);
        if (context is null && value is null && jobName is null &&
            advertisedStatePath is null && advertisedKind is null)
        {
            return managedContextAdvertised
                ? InheritedManagedProcessLifetimeCapture.Invalid
                : InheritedManagedProcessLifetimeCapture.NotInherited;
        }
        string? normalizedStatePath = TryNormalizePath(statePath);
        if (!string.Equals(context, ContextVersion, StringComparison.Ordinal) ||
            normalizedStatePath is null ||
            !string.Equals(TryNormalizePath(advertisedStatePath), normalizedStatePath, PathComparison) ||
            !string.Equals(advertisedKind, kind.ToString(), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(jobName) ||
            !string.Equals(jobName, GetJobName(normalizedStatePath, GetSuffix(kind)), StringComparison.Ordinal) ||
            !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long rawHandle) ||
            rawHandle is 0 or -1 ||
            !OperatingSystem.IsWindows())
        {
            return InheritedManagedProcessLifetimeCapture.Invalid;
        }

#pragma warning disable CA2000 // Ownership transfers to the typed capture or the failure path.
        var handle = new SafeFileHandle(new IntPtr(rawHandle), ownsHandle: true);
#pragma warning restore CA2000
        SafeFileHandle? job = null;
        FileStream? stream = null;
        try
        {
            _ = SetHandleInformation(handle.DangerousGetHandle(), HandleFlagInherit, flags: 0)
                ? true
                : throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (!IsExactLeaseHandle(handle, normalizedStatePath + GetSuffix(kind)))
            {
                throw new InvalidOperationException("Inherited lifetime lease does not match the managed state path.");
            }
            job = OpenJob(jobName, JobObjectAssignProcess | JobObjectQuery);
            if (job is null || !AssignProcessToJobObject(job.DangerousGetHandle(), GetCurrentProcess()))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            stream = new FileStream(handle, FileAccess.ReadWrite);
            return InheritedManagedProcessLifetimeCapture.Create(stream, job);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or Win32Exception)
        {
            stream?.Dispose();
            job?.Dispose();
            if (stream is null)
            {
                handle.Dispose();
            }
            return InheritedManagedProcessLifetimeCapture.Invalid;
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _job.Dispose();
    }

    private static ManagedProcessLifetimeStatus GetLeaseStatus(string statePath, string suffix)
    {
        try
        {
            using var stream = new FileStream(
                Path.GetFullPath(statePath) + suffix,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return ManagedProcessLifetimeStatus.Exited;
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33)
        {
            return ManagedProcessLifetimeStatus.Active;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ManagedProcessLifetimeStatus.Unavailable;
        }
    }

    private static ManagedProcessLifetimeStatus GetTreeStatus(string jobName)
    {
        using SafeFileHandle? job = OpenJob(jobName, JobObjectQuery);
        return job is null
            ? Marshal.GetLastPInvokeError() == ErrorFileNotFound
                ? ManagedProcessLifetimeStatus.Exited
                : ManagedProcessLifetimeStatus.Unavailable
            : TryGetActiveProcessCount(job, out uint active)
                ? active == 0
                    ? ManagedProcessLifetimeStatus.Exited
                    : ManagedProcessLifetimeStatus.Active
                : ManagedProcessLifetimeStatus.Unavailable;
    }

    private static bool TryGetActiveProcessCount(SafeFileHandle job, out uint active)
    {
        bool success = QueryInformationJobObject(
            job.DangerousGetHandle(),
            JobObjectBasicAccountingInformation,
            out JobBasicAccountingInformation information,
            Marshal.SizeOf<JobBasicAccountingInformation>(),
            out _);
        active = success ? information.ActiveProcesses : 0;
        return success;
    }

    private static SafeFileHandle? OpenOrCreateJob(string jobName)
    {
        IntPtr raw = CreateJobObject(IntPtr.Zero, jobName);
        if (raw is 0 or -1)
        {
            return null;
        }
#pragma warning disable CA2000 // Ownership transfers to the caller or the explicit failure path.
        var job = new SafeFileHandle(raw, ownsHandle: true);
#pragma warning restore CA2000
        var limits = new JobExtendedLimitInformation
        {
            BasicLimitInformation = new JobBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose,
            },
        };
        if (SetInformationJobObject(
                job.DangerousGetHandle(),
                JobObjectExtendedLimitInformation,
                in limits,
                Marshal.SizeOf<JobExtendedLimitInformation>()))
        {
            return job;
        }
        job.Dispose();
        return null;
    }

    private static SafeFileHandle? OpenJob(string jobName, uint access)
    {
        IntPtr raw = OpenJobObject(access, inheritHandle: false, jobName);
        return raw is 0 or -1 ? null : new SafeFileHandle(raw, ownsHandle: true);
    }

    private static string GetJobName(string statePath, string suffix)
    {
        string identity = FileSystemVersionManagerWriteLease.GetLockPath(statePath) + suffix;
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return $"Local\\NvtFwCombiner.ManagedTree.{hash[..32]}";
    }

    private static ManagedProcessLifetimeKind GetKind(string suffix)
    {
        return suffix switch
        {
            ApplicationSuffix => ManagedProcessLifetimeKind.Application,
            LauncherSuffix => ManagedProcessLifetimeKind.Launcher,
            _ => throw new ArgumentOutOfRangeException(nameof(suffix)),
        };
    }

    private static string GetSuffix(ManagedProcessLifetimeKind kind)
    {
        return kind switch
        {
            ManagedProcessLifetimeKind.Application => ApplicationSuffix,
            ManagedProcessLifetimeKind.Launcher => LauncherSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string? TryNormalizePath(string? path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static unsafe bool IsExactLeaseHandle(SafeFileHandle handle, string expectedPath)
    {
        const int maximumPathLength = 32_768;
        char* path = stackalloc char[maximumPathLength];
        uint length = GetFinalPathNameByHandle(handle, path, maximumPathLength, 0);
        if (length is 0 or >= maximumPathLength)
        {
            return false;
        }
        const string extendedPrefix = @"\\?\";
        string actual = new(path, 0, checked((int)length));
        if (actual.StartsWith(extendedPrefix, StringComparison.Ordinal))
        {
            actual = actual[extendedPrefix.Length..];
        }
        return string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expectedPath), PathComparison);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static string? TakeEnvironment(string key)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, null);
        return value;
    }

    private const int ErrorFileNotFound = 2;
    private const int JobObjectBasicAccountingInformation = 1;
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint JobObjectAssignProcess = 0x0001;
    private const uint JobObjectQuery = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicAccountingInformation
    {
        internal long TotalUserTime;
        internal long TotalKernelTime;
        internal long ThisPeriodTotalUserTime;
        internal long ThisPeriodTotalKernelTime;
        internal uint TotalPageFaultCount;
        internal uint TotalProcesses;
        internal uint ActiveProcesses;
        internal uint TotalTerminatedProcesses;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimitInformation
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize;
        internal UIntPtr MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobExtendedLimitInformation
    {
        internal JobBasicLimitInformation BasicLimitInformation;
        internal IoCounters IoInfo;
        internal UIntPtr ProcessMemoryLimit;
        internal UIntPtr JobMemoryLimit;
        internal UIntPtr PeakProcessMemoryUsed;
        internal UIntPtr PeakJobMemoryUsed;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateJobObject(IntPtr securityAttributes, string name);

    [LibraryImport("kernel32.dll", EntryPoint = "OpenJobObjectW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenJobObject(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        string name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryInformationJobObject(
        IntPtr job,
        int informationClass,
        out JobBasicAccountingInformation information,
        int informationLength,
        out int returnLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
        IntPtr job,
        int informationClass,
        in JobExtendedLimitInformation information,
        int informationLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateJobObject(IntPtr job, uint exitCode);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    private static unsafe partial uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        char* path,
        uint pathLength,
        uint flags);
}

/// <summary>Typed inherited lifetime capture held until the managed process exits.</summary>
internal sealed class InheritedManagedProcessLifetimeCapture : IInheritedManagedProcessLifetimeCapture
{
    private readonly IDisposable? _job;
    private readonly IDisposable? _lease;

    private InheritedManagedProcessLifetimeCapture(
        InheritedManagedProcessLifetimeOutcome outcome,
        IDisposable? lease,
        IDisposable? job)
    {
        Outcome = outcome;
        _lease = lease;
        _job = job;
    }

    /// <summary>Gets the exact inherited-context classification.</summary>
    public InheritedManagedProcessLifetimeOutcome Outcome { get; }

    internal static InheritedManagedProcessLifetimeCapture NotInherited { get; } =
        new(InheritedManagedProcessLifetimeOutcome.NotInherited, null, null);
    internal static InheritedManagedProcessLifetimeCapture Invalid { get; } =
        new(InheritedManagedProcessLifetimeOutcome.InvalidInheritedContext, null, null);

    internal static InheritedManagedProcessLifetimeCapture Create(IDisposable lease, IDisposable job)
    {
        return new(InheritedManagedProcessLifetimeOutcome.Captured, lease, job);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lease?.Dispose();
        _job?.Dispose();
    }
}

/// <summary>Consumes inherited managed-tree lifetime context at process entry.</summary>
public static class InheritedManagedProcessLifetime
{
    /// <summary>Reports whether the Desktop READY channel advertises managed startup.</summary>
    public static bool IsApplicationReadyContextAdvertised()
    {
        return Environment.GetEnvironmentVariable(
                   AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment) is not null ||
               Environment.GetEnvironmentVariable(
                   AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment) is not null;
    }

    /// <summary>Captures and classifies the inherited lifetime context.</summary>
    public static IInheritedManagedProcessLifetimeCapture Capture(
        string? statePath,
        ManagedProcessLifetimeKind kind,
        bool managedContextAdvertised)
    {
        return ManagedProcessLifetimeLease.CaptureInherited(statePath, kind, managedContextAdvertised);
    }
}
