using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace NvtFwCombiner.TestSupport;

// Workaround for xUnit issue 3576, fixed upstream in xunit.v3 4.0.0 (commit 8b317b7523).
// In xunit.v3 3.2.2 the in-process console runner queues a timer when Main returns. If the process is still
// alive one second later, the timer writes ShutdownMessage to standard output. During the "-assemblyInfo"
// query that line follows the assembly-info JSON, xunit.runner.visualstudio rejects the output as invalid
// JSON, and VSTest discovery lists no tests. In that query only, this guard removes exactly that line and
// forwards every other line unchanged, so any other unexpected output still fails the adapter's validation.
// xunit.v3 4.0.0 suppresses the same message in assembly-info mode.
// Delete this file, XunitAssemblyInfoOutputGuardTests and the Directory.Build.props entry when xunit.v3 is
// 4.0.0 or later.
[ExcludeFromCodeCoverage]
internal static class XunitAssemblyInfoOutputGuard
{
    internal const string AssemblyInfoSwitch = "-assemblyInfo";

    // The exact text of xunit.v3 3.2.2 ConsoleRunner.Run when automated mode is off.
    internal const string ShutdownMessage = "Waiting 10 seconds for foreground threads to exit...";

    [ModuleInitializer]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Console.Out owns both writers until the process exits.")]
    internal static void Install()
    {
        if (!IsAssemblyInfoQuery(Environment.GetCommandLineArgs()[1..], File.Exists, File.ReadAllLines))
        {
            return;
        }

        var standardOutput = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
        Console.SetOut(new ShutdownMessageFilter(standardOutput));
    }

    // The out-of-process launcher passes "@@ <file>" and writes the real arguments one per line, which
    // xunit.v3 3.2.2 CommandLineParserBase.GetArguments expands the same way.
    internal static bool IsAssemblyInfoQuery(
        string[] arguments,
        Func<string, bool> fileExists,
        Func<string, string[]> readLines)
    {
        IEnumerable<string> expanded = arguments;
        if (arguments is ["@@", string responseFile])
        {
            try
            {
                expanded = fileExists(responseFile) ? readLines(responseFile) : [];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        return expanded.Any(argument =>
            string.Equals(argument.Trim(), AssemblyInfoSwitch, StringComparison.OrdinalIgnoreCase));
    }

    // Holds each line until its line feed, then forwards it unless it is exactly ShutdownMessage.
    // Flush and Dispose forward an unfinished line.
    internal sealed class ShutdownMessageFilter(TextWriter inner) : TextWriter
    {
        private readonly StringBuilder _line = new();

        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value)
        {
            _ = _line.Append(value);
            if (value != '\n')
            {
                return;
            }

            string line = _line.ToString();
            _ = _line.Clear();
            if (!string.Equals(line.TrimEnd('\n').TrimEnd('\r'), ShutdownMessage, StringComparison.Ordinal))
            {
                inner.Write(line);
                inner.Flush();
            }
        }

        public override void Flush()
        {
            ForwardUnfinishedLine();
            inner.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ForwardUnfinishedLine();
                inner.Flush();
            }

            base.Dispose(disposing);
        }

        private void ForwardUnfinishedLine()
        {
            if (_line.Length == 0)
            {
                return;
            }

            inner.Write(_line.ToString());
            _ = _line.Clear();
        }
    }
}
