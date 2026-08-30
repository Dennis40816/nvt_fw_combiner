using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Platform.Processes;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal enum BootstrapAdmissionInheritanceOutcome
{
    NotInherited,
    Inherited,
    Invalid,
}

internal enum BootstrapStartGateInheritanceOutcome
{
    NotInherited,
    Inherited,
    Invalid,
}

/// <summary>One-use parent authorization captured before Root Bootstrap may inspect managed state.</summary>
internal sealed partial class BootstrapStartGate : IDisposable
{
    internal const string ContextEnvironment = "NVT_FW_COMBINER_BOOTSTRAP_START_CONTEXT";
    internal const string HandleEnvironment = "NVT_FW_COMBINER_BOOTSTRAP_START_PIPE_HANDLE";
    internal const string ContextVersion = "v1";
    private const uint HandleFlagInherit = 1;
    private SafePipeHandle? _handle;

    private BootstrapStartGate(
        BootstrapStartGateInheritanceOutcome outcome,
        SafePipeHandle? handle)
    {
        Outcome = outcome;
        _handle = handle;
    }

    internal BootstrapStartGateInheritanceOutcome Outcome { get; }

    internal static BootstrapStartGate Capture()
    {
        string? context = TakeEnvironment(ContextEnvironment);
        string? handle = TakeEnvironment(HandleEnvironment);
        if (context is null && handle is null)
        {
            return new(BootstrapStartGateInheritanceOutcome.NotInherited, null);
        }
        if (!long.TryParse(handle, NumberStyles.None, CultureInfo.InvariantCulture, out long rawHandle) ||
            rawHandle is 0 or -1)
        {
            return new(BootstrapStartGateInheritanceOutcome.Invalid, null);
        }
#pragma warning disable CA2000 // Ownership transfers into the returned one-use gate.
        var ownedHandle = new SafePipeHandle(new IntPtr(rawHandle), ownsHandle: true);
#pragma warning restore CA2000
        if (!SetHandleInformation(ownedHandle.DangerousGetHandle(), HandleFlagInherit, flags: 0))
        {
            ownedHandle.Dispose();
            return new(BootstrapStartGateInheritanceOutcome.Invalid, null);
        }
        if (!string.Equals(context, ContextVersion, StringComparison.Ordinal))
        {
            ownedHandle.Dispose();
            return new(BootstrapStartGateInheritanceOutcome.Invalid, null);
        }
        return new(BootstrapStartGateInheritanceOutcome.Inherited, ownedHandle);
    }

    internal async ValueTask<bool> WaitForStartAsync(CancellationToken cancellationToken)
    {
        if (Outcome == BootstrapStartGateInheritanceOutcome.NotInherited)
        {
            return true;
        }
        if (Outcome != BootstrapStartGateInheritanceOutcome.Inherited || _handle is null)
        {
            return false;
        }
        try
        {
            await using var pipe = new AnonymousPipeClientStream(PipeDirection.In, _handle);
            _handle = null;
            string? signal = await BoundedUtf8LineReader.ReadAsync(
                pipe,
                maximumCharacters: 8,
                bufferSize: 32,
                cancellationToken).ConfigureAwait(false);
            return string.Equals(signal, "START", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is
            ArgumentException or DecoderFallbackException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }

    private static string? TakeEnvironment(string key)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, null);
        return value;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
}

/// <summary>Parent-owned one-use START authority. Pending may become Authorized or Aborted once.</summary>
internal sealed class BootstrapStartAuthorization : IDisposable
{
    private readonly AnonymousPipeServerStream _pipe;
    private int _state;

    internal BootstrapStartAuthorization()
    {
        _pipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
    }

    internal string ClientHandle => _pipe.GetClientHandleAsString();

    internal ProcessInheritedHandle InheritedHandle => ProcessInheritedHandle.Parse(
        BootstrapStartGate.HandleEnvironment,
        ClientHandle);

    internal void ApplyInheritedContext(ProcessStartInfo startInfo)
    {
        startInfo.Environment[BootstrapStartGate.ContextEnvironment] = BootstrapStartGate.ContextVersion;
        startInfo.Environment[BootstrapStartGate.HandleEnvironment] = ClientHandle;
    }

    internal bool TryAuthorize()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return false;
        }
        try
        {
            byte[] message = "START\n"u8.ToArray();
            _pipe.Write(message);
            _pipe.Flush();
            _pipe.Dispose();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return false;
        }
    }

    internal bool TryAbort()
    {
        if (Interlocked.CompareExchange(ref _state, 2, 0) != 0)
        {
            return false;
        }
        _pipe.Dispose();
        return true;
    }

    internal void DisposeLocalClientHandle()
    {
        try
        {
            _pipe.DisposeLocalCopyOfClientHandle();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        _ = TryAbort();
        _pipe.Dispose();
    }
}

internal sealed partial class BootstrapAdmissionSignal : IDisposable
{
    private const string AdmittedLine = "ADMITTED\n";
    private const uint HandleFlagInherit = 1;
    private SafePipeHandle? _handle;
    private int _reported;

    private BootstrapAdmissionSignal(
        BootstrapAdmissionInheritanceOutcome outcome,
        SafePipeHandle? handle)
    {
        Outcome = outcome;
        _handle = handle;
    }

    internal BootstrapAdmissionInheritanceOutcome Outcome { get; }

    internal static BootstrapAdmissionSignal Capture()
    {
        string? handle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment);
        Environment.SetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
            null);
        if (handle is null)
        {
            return new(BootstrapAdmissionInheritanceOutcome.NotInherited, null);
        }
        if (string.IsNullOrWhiteSpace(handle) ||
            !long.TryParse(handle, NumberStyles.None, CultureInfo.InvariantCulture, out long rawHandle) ||
            rawHandle is 0 or -1)
        {
            return new(BootstrapAdmissionInheritanceOutcome.Invalid, null);
        }
#pragma warning disable CA2000 // Ownership transfers into the returned one-use admission signal.
        var ownedHandle = new SafePipeHandle(new IntPtr(rawHandle), ownsHandle: true);
#pragma warning restore CA2000
        if (!SetHandleInformation(
                ownedHandle.DangerousGetHandle(),
                HandleFlagInherit,
                flags: 0))
        {
            ownedHandle.Dispose();
            return new(BootstrapAdmissionInheritanceOutcome.Invalid, null);
        }
        return new(BootstrapAdmissionInheritanceOutcome.Inherited, ownedHandle);
    }

    internal async ValueTask<bool> ReportAdmittedAsync(CancellationToken cancellationToken)
    {
        if (Outcome == BootstrapAdmissionInheritanceOutcome.NotInherited)
        {
            return true;
        }
        if (Outcome != BootstrapAdmissionInheritanceOutcome.Inherited)
        {
            return false;
        }
        int prior = Interlocked.CompareExchange(ref _reported, -1, 0);
        if (prior == 1)
        {
            return true;
        }
        if (prior != 0)
        {
            return false;
        }
        try
        {
            await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, _handle!);
            _handle = null;
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(AdmittedLine), cancellationToken)
                .ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _reported, 1);
            return true;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or UnauthorizedAccessException)
        {
            _handle?.Dispose();
            _handle = null;
            Volatile.Write(ref _reported, 2);
            return false;
        }
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
}
