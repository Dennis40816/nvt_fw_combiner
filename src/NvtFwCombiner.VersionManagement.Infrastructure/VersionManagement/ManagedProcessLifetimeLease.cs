using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Inherited child-owned file lease that survives the launching process.</summary>
internal sealed partial class ManagedProcessLifetimeLease : IDisposable
{
    internal const string HandleEnvironment = "NVT_FW_COMBINER_PROCESS_LIFETIME_HANDLE";
    internal const string ApplicationSuffix = ".application-lifetime.v1.lock";
    internal const string LauncherSuffix = ".launcher-lifetime.v1.lock";
    private const uint HandleFlagInherit = 1;

    private readonly FileStream _stream;

    private ManagedProcessLifetimeLease(FileStream stream)
    {
        _stream = stream;
    }

    internal string InheritedHandle => _stream.SafeFileHandle.DangerousGetHandle()
        .ToInt64()
        .ToString(CultureInfo.InvariantCulture);

    internal static ManagedProcessLifetimeLease? TryAcquire(string statePath, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        FileStream? stream = null;
        try
        {
            string path = Path.GetFullPath(statePath) + suffix;
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                throw new ArgumentException("Lifetime lease has no parent directory.", nameof(statePath)));
            stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            _ = SetHandleInformation(
                    stream.SafeFileHandle.DangerousGetHandle(),
                    HandleFlagInherit,
                    HandleFlagInherit)
                ? true
                : throw new Win32Exception(Marshal.GetLastPInvokeError());
            return new(stream);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or Win32Exception)
        {
            stream?.Dispose();
            return null;
        }
    }

    internal static ManagedProcessLifetimeStatus GetStatus(string statePath, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        if (!OperatingSystem.IsWindows())
        {
            return ManagedProcessLifetimeStatus.Unavailable;
        }
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

    internal static IDisposable? CaptureInherited()
    {
        string? value = Environment.GetEnvironmentVariable(HandleEnvironment);
        Environment.SetEnvironmentVariable(HandleEnvironment, null);
        if (value is null)
        {
            return null;
        }
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long rawHandle) ||
            rawHandle is 0 or -1)
        {
            throw new InvalidOperationException("Inherited managed-process lifetime handle is invalid.");
        }
#pragma warning disable CA2000 // Ownership transfers to FileStream; the failure path disposes explicitly.
        var handle = new SafeFileHandle(new IntPtr(rawHandle), ownsHandle: true);
#pragma warning restore CA2000
        try
        {
            _ = SetHandleInformation(
                    handle.DangerousGetHandle(),
                    HandleFlagInherit,
                    flags: 0)
                ? true
                : throw new Win32Exception(Marshal.GetLastPInvokeError());
            return new FileStream(handle, FileAccess.ReadWrite);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(
        IntPtr handle,
        uint mask,
        uint flags);
}

/// <summary>Consumes an inherited child-owned lifetime lease at managed process entry.</summary>
public static class InheritedManagedProcessLifetime
{
    /// <summary>Captures the inherited lease, or returns null for an unmanaged invocation.</summary>
    public static IDisposable? Capture()
    {
        return ManagedProcessLifetimeLease.CaptureInherited();
    }
}
