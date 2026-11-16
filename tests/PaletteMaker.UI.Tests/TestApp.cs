using Avalonia;
using Avalonia.Headless;
using PaletteMaker.UI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace PaletteMaker.UI.Tests;

public static class TestApp
{
    // UseHeadlessDrawing = false: gerçek Skia ile çizilir, böylece ekran görüntüsü alınabilir.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
