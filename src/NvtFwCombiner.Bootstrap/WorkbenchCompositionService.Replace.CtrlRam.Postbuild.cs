using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static string FormatPostbuildCommandBlock(LegacyCombinerPostbuildCommandPlan commandPlan)
    {
        string firmwarePath = Path.Combine("output", commandPlan.Profile.FirmwareFileName);
        const string binDirectory = "BIN";
        return string.Join(
            Environment.NewLine,
            commandPlan.Commands.Select(command =>
                $"Combiner.exe {string.Join(' ', LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(command, firmwarePath, binDirectory))}"));
    }
}
