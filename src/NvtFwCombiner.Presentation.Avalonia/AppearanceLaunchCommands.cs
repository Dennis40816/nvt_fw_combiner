using Avalonia.Threading;
using Nvt.Core.RuntimeQuery;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One definition of each appearance option for Core startup parsing and runtime routing.</summary>
internal static class AppearanceLaunchCommands
{
    internal static IReadOnlyList<RuntimeQueryCommand> Create(LaunchAppearanceSession? session = null)
    {
        return Array.AsReadOnly<RuntimeQueryCommand>(
        [
            Define("theme", "light|dark|system", static value => value switch
            {
                "light" => new("Light", null, null),
                "dark" => new("Dark", null, null),
                "system" => new("System", null, null),
                _ => null,
            }, session),
            Define("language", "en|zh-Hant", static value => value switch
            {
                "en" => new(null, "English", null),
                "zh-hant" => new(null, "Traditional Chinese", null),
                _ => null,
            }, session),
            Define("motion", "full|reduced", static value => value switch
            {
                "full" => new(null, null, false),
                "reduced" => new(null, null, true),
                _ => null,
            }, session),
        ]);
    }

    internal static RuntimeQueryStartupParseResult Parse(IReadOnlyList<string> arguments)
    {
        return new RuntimeQueryCommandRouter(Create(), requireConfirmation: false).ParseStartupArguments(arguments);
    }

    private static RuntimeQueryCommand Define(string name, string syntax,
        Func<string, AppearanceValues?> parse, LaunchAppearanceSession? session)
    {
        return new(name, RuntimeQueryCommandRisk.ChangesState, arguments =>
        {
            Dispatcher.UIThread.VerifyAccess();
            RuntimeQueryResponseEnvelope? error = Read(arguments, name, syntax, parse, out AppearanceValues? values);
            if (error is not null) { return Task.FromResult(error); }
            if (session is null || !session.ApplyCommand.CanExecute(values))
            {
                return Task.FromResult(RuntimeQueryResponseEnvelope.Failure("UNAVAILABLE", "The appearance owner is unavailable."));
            }
            session.ApplyCommand.Execute(values);
            return Task.FromResult(RuntimeQueryResponseEnvelope.Success(true));
        }, RuntimeQueryStartupPhase.AfterStartup, "value",
            arguments => Read(arguments, name, syntax, parse, out _));
    }

    private static RuntimeQueryResponseEnvelope? Read(IReadOnlyDictionary<string, string>? arguments,
        string name, string syntax, Func<string, AppearanceValues?> parse, out AppearanceValues? values)
    {
        values = null;
        if (arguments is not { Count: 1 } || !arguments.TryGetValue("value", out string? value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return RuntimeQueryResponseEnvelope.Failure("INVALID_ARGUMENTS", $"--{name} requires one value ({syntax}).");
        }
        values = parse(value.ToLowerInvariant());
        return values is not null ? null : RuntimeQueryResponseEnvelope.Failure("INVALID_ARGUMENTS",
            $"Unsupported --{name} value '{value}'. Use {syntax}.");
    }
}

/// <summary>Only the explicitly overridden preference fields; absent fields retain their current owner values.</summary>
internal sealed record AppearanceValues(string? Theme, string? Language, bool? ReducedMotion);
