using System.ComponentModel;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedLauncherProcessTests
{
    /// <summary>A real outer READY handle is closed when expected identity is missing or blank.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task OuterReadyCaptureClosesHandleWhenExpectedIdentityIsInvalid(string? expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(pipe);
        string? priorHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? priorExpected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                inheritedDuplicate);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                expected);

            using LauncherReadyInheritance context =
                LauncherBootstrapRuntime.CaptureNestedReadyContext();
            pipe.DisposeLocalCopyOfClientHandle();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var reader = new StreamReader(pipe);

            Assert.Equal(
                LauncherReadyInheritanceOutcome.InvalidInheritedContext,
                context.Outcome);
            Assert.Empty(await reader.ReadToEndAsync(timeout.Token));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                priorHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                priorExpected);
        }
    }

    /// <summary>Launcher entry clears the outer READY handle before it can launch Desktop.</summary>
    [Fact]
    public void OuterReadyHandleIsNonInheritableImmediatelyAfterCapture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        using var pipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string handle = DuplicateInheritableClientHandle(pipe);

        WithOuterReadyEnvironment(handle, LauncherReadyProtocol.CreateExpectedPrefix(identity), () =>
        {
            using LauncherReadyInheritance context =
                LauncherBootstrapRuntime.CaptureNestedReadyContext();
            Assert.True(long.TryParse(
                handle,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long rawHandle));
            if (!GetHandleInformation(new IntPtr(rawHandle), out uint flags))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            Assert.Equal(LauncherReadyInheritanceOutcome.Inherited, context.Outcome);
            Assert.Equal(0u, flags & 1u);
        });
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetHandleInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetHandleInformation(IntPtr handle, out uint flags);
}
