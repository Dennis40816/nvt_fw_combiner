using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Avalonia.Headless;
using Avalonia.Threading;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(NvtFwCombiner.UiSmoke.Tests.HeadlessSessionLoopGuard))]
[assembly: NvtFwCombiner.UiSmoke.Tests.HeadlessTestsInFlight]

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>
/// Ends the test process with its cause when the Avalonia headless session loop faults, instead of
/// letting every headless test wait until the hang timeout.
/// </summary>
/// <remarks>
/// Avalonia.Headless 12.0.5 sets up and tears down the application of each headless test outside
/// the <c>try</c> that reports to that test. An exception there ends the session loop without output,
/// and every queued or later <c>[AvaloniaFact]</c>/<c>[AvaloniaTheory]</c> then blocks its xUnit
/// worker without a timeout (BUG-20260927-uismoke-headless-session-stall). The loop task faults
/// only on that path: exceptions from a test body are reported to that test, and disposing the
/// session completes the loop normally. A faulted loop can never run another headless test.
/// </remarks>
public sealed class HeadlessSessionLoopGuard
{
    private const string BugId = "BUG-20260927-uismoke-headless-session-stall";
    private const string LoopFieldName = "_dispatchTask";
    private const string UiThreadFieldName = "s_uiThread";

    /// <summary>Starts or reuses this assembly's headless session and watches its loop.</summary>
    public HeadlessSessionLoopGuard()
    {
        HeadlessUnitTestSession session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessSessionLoopGuard).Assembly);
        _ = FailWhenFaulted(FindLoop(session), Environment.FailFast);
    }

    internal static Task FindLoop(HeadlessUnitTestSession session)
    {
        FieldInfo? field = typeof(HeadlessUnitTestSession).GetField(
            LoopFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(session) as Task
            ?? throw new InvalidOperationException(
                $"Cannot watch the Avalonia headless session loop: {nameof(HeadlessUnitTestSession)}."
                + $"{LoopFieldName} is missing or unset in {typeof(HeadlessUnitTestSession).Assembly.GetName()}. "
                + $"Revisit {nameof(HeadlessSessionLoopGuard)} for this Avalonia version ({BugId}).");
    }

    internal static Task FailWhenFaulted(Task loop, Action<string, Exception?> fail)
    {
        return loop.ContinueWith(
            faulted =>
            {
                Exception cause = faulted.Exception!.InnerExceptions.Count == 1
                    ? faulted.Exception.InnerExceptions[0]
                    : faulted.Exception;
                fail(Describe(cause), cause);
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal static string Describe(Exception cause)
    {
        StringBuilder text = new StringBuilder()
            .Append("The Avalonia headless session loop faulted during headless test setup or teardown; ")
            .Append("every queued or later [AvaloniaFact]/[AvaloniaTheory] would wait forever, ")
            .Append("so the test process stops now (").Append(BugId).AppendLine(").")
            .Append("Setup fails this way when code outside the session, for example a plain [Fact] or [Theory], ")
            .AppendLine("creates an Avalonia object or reads Dispatcher.UIThread while a headless test is being set up.")
            .Append("Observed on thread: ").AppendLine(DescribeThread(Thread.CurrentThread))
            .Append("Avalonia UI dispatcher thread: ").AppendLine(DescribeUiDispatcherThread())
            .AppendLine("Tests in flight (thread where each started):");
        foreach (string test in HeadlessTestsInFlightAttribute.Snapshot())
        {
            _ = text.Append("  ").AppendLine(test);
        }

        // Environment.FailFast prints the full exception after this message.
        return text.Append("Cause: ").Append(cause.GetType().FullName).Append(": ").Append(cause.Message).ToString();
    }

    private static string DescribeUiDispatcherThread()
    {
        // Read the field instead of Dispatcher.UIThread: that getter creates a dispatcher when none exists.
        FieldInfo? field = typeof(Dispatcher).GetField(
            UiThreadFieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        return field is null
            ? "unavailable"
            : field.GetValue(null) is Dispatcher dispatcher ? DescribeThread(dispatcher.Thread) : "none";
    }

    internal static string DescribeThread(Thread thread)
    {
        return $"managed {thread.ManagedThreadId} ({thread.Name ?? "unnamed"}, "
            + (thread.IsThreadPoolThread ? "thread pool)" : "dedicated)");
    }
}

/// <summary>Tracks the tests that have started and not finished, for the headless session diagnostic.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class HeadlessTestsInFlightAttribute : BeforeAfterTestAttribute
{
    private static readonly ConcurrentDictionary<IXunitTest, string> Started = new();

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        Started[test] = HeadlessSessionLoopGuard.DescribeThread(Thread.CurrentThread);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        _ = Started.TryRemove(test, out _);
    }

    internal static IReadOnlyList<string> Snapshot()
    {
        return [.. Started
            .Select(static pair => $"{pair.Key.TestDisplayName} on {pair.Value}")
            .Order(StringComparer.Ordinal)];
    }
}
