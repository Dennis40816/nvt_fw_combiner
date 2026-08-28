using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the physical inherited lifetime-handle identity boundary.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed partial class ManagedProcessLifetimeHandleIdentityTests
{
    /// <summary>A readable arbitrary-file handle cannot impersonate the exact managed lifetime lease.</summary>
    [Fact]
    public async Task ManagedEntryRejectsDifferentReadableFileHandleWithoutReady()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        string arbitraryPath = workspace.PathFor("unrelated/readable.txt");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(arbitraryPath)!);
        await File.WriteAllTextAsync(
            arbitraryPath,
            "not a lifetime lease",
            TestContext.Current.CancellationToken);
        using var arbitrary = new FileStream(
            arbitraryPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using ManagedProcessLifetimeLease? lease = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            ManagedProcessLifetimeKind.Application);
        Assert.NotNull(lease);
        using var readyPipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                AppContext.BaseDirectory,
                "ready-probe",
                "NvtFwCombiner.ReadyProbe.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(Path.GetFullPath(statePath));
        startInfo.Environment[AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment] =
            readyPipe.GetClientHandleAsString();
        startInfo.Environment[AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment] = "0.10.6";
        lease.ApplyInheritedContext(startInfo);
        MakeInheritable(arbitrary.SafeFileHandle);
        startInfo.Environment[ManagedProcessLifetimeLease.HandleEnvironment] =
            arbitrary.SafeFileHandle.DangerousGetHandle().ToInt64().ToString(CultureInfo.InvariantCulture);

        try
        {
            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Probe did not start.");
            readyPipe.DisposeLocalCopyOfClientHandle();
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            using var reader = new StreamReader(readyPipe);
            string? ready = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

            Assert.Equal(24, process.ExitCode);
            Assert.Null(ready);
        }
        finally
        {
            ClearInheritable(arbitrary.SafeFileHandle);
        }
    }

    private static void MakeInheritable(SafeFileHandle handle)
    {
        if (!SetHandleInformation(handle, HandleFlagInherit, HandleFlagInherit))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private static void ClearInheritable(SafeFileHandle handle)
    {
        if (!handle.IsClosed && !SetHandleInformation(handle, HandleFlagInherit, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private const uint HandleFlagInherit = 0x00000001;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
}
