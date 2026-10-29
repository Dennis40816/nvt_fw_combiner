using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace NvtFwCombiner.TestSupport;

// Workaround for xUnit issue 3576, fixed upstream in xunit.v3 4.0.0 (commit 8b317b7523).
// In xunit.v3 3.2.2 the in-process console runner queues a timer when Main returns. If the
// process is still alive one second later, the timer writes "Waiting 10 seconds for foreground
// threads to exit..." to standard output. During the "-assemblyInfo" query that line follows the
// assembly-info JSON, xunit.runner.visualstudio rejects the output as invalid JSON, and VSTest
// discovery lists no tests. The query writes exactly one line, so this guard forwards that line and
// drops all later output, which is what xunit.v3 4.0.0 does in assembly-info mode.
// Delete this file and its Directory.Build.props entry when xunit.v3 is 4.0.0 or later.
[ExcludeFromCodeCoverage]
internal static class XunitAssemblyInfoOutputGuard
{
    internal const string AssemblyInfoSwitch = "-assemblyInfo";

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
        Console.SetOut(new FirstLineOnlyWriter(standardOutput));
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

    internal sealed class FirstLineOnlyWriter(TextWriter inner) : TextWriter
    {
        private bool _firstLineComplete;

        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value)
        {
            if (_firstLineComplete)
            {
                return;
            }

            inner.Write(value);
            if (value == '\n')
            {
                _firstLineComplete = true;
                inner.Flush();
            }
        }

        public override void Flush()
        {
            inner.Flush();
        }
    }
}
