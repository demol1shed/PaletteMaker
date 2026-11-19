using Avalonia;

namespace PaletteMaker;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.SettingsFilePath = PaletteMaker.Settings.SettingsService.DefaultPath();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
