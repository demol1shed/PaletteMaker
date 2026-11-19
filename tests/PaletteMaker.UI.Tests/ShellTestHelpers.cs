using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;

namespace PaletteMaker.UI.Tests;

/// <summary>Kabuk (ShellView) testleri için ortak yardımcılar.</summary>
internal static class ShellTestHelpers
{
    public static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");

    public static byte[] Png()
    {
        using var bmp = new SKBitmap(new SKImageInfo(200, 100, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bmp))
        {
            SKColor[] cs = [new(0xE6, 0x39, 0x46), new(0x2A, 0x9D, 0x8F), new(0x26, 0x46, 0x53), new(0xF4, 0xA2, 0x61)];
            for (int i = 0; i < cs.Length; i++) { using var p = new SKPaint { Color = cs[i] }; canvas.DrawRect(i * 50, 0, 50, 100, p); }
        }
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public static async Task<(Window W, ShellView V, ShellViewModel Vm)> OpenShellAsync(int width, int height, bool dark = false, bool loadImage = true, ShellTab tab = ShellTab.Image)
    {
        var shellVm = new ShellViewModel(new MainViewModel());
        var view = new ShellView { DataContext = shellVm };
        var window = new Window { Width = width, Height = height, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        if (loadImage)
        {
            var png = Png();
            await shellVm.Main.LoadAsync("ornek.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
        }
        shellVm.Tab = tab;
        Settle();
        return (window, view, shellVm);
    }

    public static void Capture(Window window, string name)
    {
        Settle();
        Directory.CreateDirectory(ScreenshotDir);
        using var frame = window.CaptureRenderedFrame();
        frame!.Save(Path.Combine(ScreenshotDir, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    public static Rect BoundsIn(Visual v, Visual root) => new(v.TranslatePoint(new Point(0, 0), root)!.Value, v.Bounds.Size);
}
