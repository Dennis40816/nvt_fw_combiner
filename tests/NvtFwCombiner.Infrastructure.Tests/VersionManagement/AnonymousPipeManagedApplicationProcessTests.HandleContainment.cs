using System.ComponentModel;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedApplicationProcessTests
{
    /// <summary>A real READY handle is closed when its expected version is missing or blank.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ApplicationReadyCaptureClosesHandleWhenExpectedVersionIsInvalid(
        string? expectedVersion)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateApplicationReadyClientHandle(pipe);
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                inheritedDuplicate);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                expectedVersion);

            using var signal = new InheritedPipeApplicationReadySignal();
            ApplicationReadySignalOutcome outcome = await signal.ReportReadyAsync(
                ManagedAppVersion.Parse("0.10.6"),
                TestContext.Current.CancellationToken);
            pipe.DisposeLocalCopyOfClientHandle();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var reader = new StreamReader(pipe);

            Assert.Equal(ApplicationReadySignalOutcome.InvalidInheritedContext, outcome);
            Assert.Empty(await reader.ReadToEndAsync(timeout.Token));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                priorVersion);
        }
    }

    /// <summary>Desktop capture clears inheritance before any later child can start.</summary>
    [Fact]
    public async Task ApplicationReadyHandleIsNonInheritableImmediatelyAfterCapture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string handle = DuplicateApplicationReadyClientHandle(pipe);
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                handle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                "0.10.6");

            using var signal = new InheritedPipeApplicationReadySignal();
            Assert.True(long.TryParse(
                handle,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long rawHandle));
            if (!GetApplicationReadyHandleInformation(new IntPtr(rawHandle), out uint flags))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            Assert.Equal(0u, flags & 1u);
            Assert.Equal(
                ApplicationReadySignalOutcome.Reported,
                await signal.ReportReadyAsync(
                    ManagedAppVersion.Parse("0.10.6"),
                    TestContext.Current.CancellationToken));
            using var reader = new StreamReader(pipe);
            Assert.Equal(
                "READY:0.10.6",
                await reader.ReadLineAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
                priorVersion);
        }
    }

    private static string DuplicateApplicationReadyClientHandle(AnonymousPipeServerStream pipe)
    {
        IntPtr process = GetApplicationReadyCurrentProcess();
        IntPtr source = new(long.Parse(
            pipe.GetClientHandleAsString(),
            NumberStyles.None,
            CultureInfo.InvariantCulture));
        if (!DuplicateApplicationReadyHandle(
                process,
                source,
                process,
                out SafeFileHandle duplicate,
                desiredAccess: 0,
                inheritHandle: true,
                options: 2))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
#pragma warning disable CA2000 // Ownership transfers to the ready-signal capture through the raw handle.
        string value = duplicate.DangerousGetHandle().ToInt64().ToString(CultureInfo.InvariantCulture);
        duplicate.SetHandleAsInvalid();
        duplicate.Dispose();
#pragma warning restore CA2000
        return value;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetApplicationReadyHandleInformation(IntPtr handle, out uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "DuplicateHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DuplicateApplicationReadyHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out SafeFileHandle targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
    private static partial IntPtr GetApplicationReadyCurrentProcess();
}
