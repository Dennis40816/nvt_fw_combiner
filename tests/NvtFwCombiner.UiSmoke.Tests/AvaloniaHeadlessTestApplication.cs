using Avalonia;
using Avalonia.Headless;
using NvtFwCombiner.Presentation.Avalonia;

[assembly: AvaloniaTestApplication(
    typeof(NvtFwCombiner.UiSmoke.Tests.AvaloniaHeadlessTestApplication))]

namespace NvtFwCombiner.UiSmoke.Tests;

internal static class AvaloniaHeadlessTestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
    }
}
