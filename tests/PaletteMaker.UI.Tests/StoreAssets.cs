using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

/// <summary>
/// Google Play mağaza görsellerini üretir: 512×512 simge, 1024×500 öne çıkan grafik ve 1080×2160 telefon ekran
/// görüntüleri (gerçek MainView, 3× ölçekli). Yalnızca <c>PALETTE_STORE_ASSETS=1</c> iken çalışır, çıktı
/// <c>store/graphics/</c> altına yazılır:
/// <c>PALETTE_STORE_ASSETS=1 dotnet test tests/PaletteMaker.UI.Tests --filter StoreAssets</c>
/// </summary>
public class StoreAssets
{
    private static readonly SKColor Navy = new(0x1D, 0x35, 0x57);
    private static readonly SKColor[] Tiles = [new(0xE6, 0x39, 0x46), new(0xF4, 0xA2, 0x61), new(0x2A, 0x9D, 0x8F), new(0xE9, 0xC4, 0x6A)];

    private static string OutDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PaletteMaker.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "store", "graphics");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Save(SKBitmap bmp, string path)
    {
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static void RoundRect(SKCanvas c, float x, float y, float w, float h, float r, SKColor color)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true };
        c.DrawRoundRect(x, y, w, h, r, r, p);
    }

    /// <summary>Dört palet karosu: uygulama simgesiyle aynı çizim (108 birimlik tuvalde 32,5–75,5 arası).</summary>
    private static void DrawTiles(SKCanvas c, float originX, float originY, float scale)
    {
        float[] xs = [32.5f, 55.5f, 32.5f, 55.5f], ys = [32.5f, 32.5f, 55.5f, 55.5f];
        for (int i = 0; i < 4; i++)
            RoundRect(c, originX + xs[i] * scale, originY + ys[i] * scale, 20 * scale, 20 * scale, 5 * scale, Tiles[i]);
    }

    private static byte[] SunsetPng()
    {
        const int W = 800, H = 500;
        using var bmp = new SKBitmap(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var c = new SKCanvas(bmp);
        using (var sky = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 330),
            [new SKColor(0x1B, 0x26, 0x5C), new SKColor(0x6A, 0x3D, 0x9A), new SKColor(0xF0, 0x6A, 0x5B), new SKColor(0xFF, 0xC8, 0x6B)], [0f, 0.4f, 0.75f, 1f], SKShaderTileMode.Clamp) })
            c.DrawRect(0, 0, W, 330, sky);
        using (var sun = new SKPaint { Color = new SKColor(0xFF, 0xE9, 0xA8), IsAntialias = true }) c.DrawCircle(520, 300, 70, sun);
        using (var far = new SKPaint { Color = new SKColor(0x3B, 0x2A, 0x5E), IsAntialias = true })
        { using var path = new SKPath(); path.MoveTo(0, 330); path.LineTo(120, 250); path.LineTo(230, 310); path.LineTo(360, 230); path.LineTo(470, 330); path.Close(); c.DrawPath(path, far); }
        using (var near = new SKPaint { Color = new SKColor(0x1E, 0x1B, 0x3A), IsAntialias = true })
        { using var path = new SKPath(); path.MoveTo(520, 330); path.LineTo(640, 270); path.LineTo(800, 320); path.LineTo(800, 330); path.Close(); c.DrawPath(path, near); }
        using (var sea = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 330), new SKPoint(0, H),
            [new SKColor(0x2A, 0x9D, 0x8F), new SKColor(0x12, 0x4E, 0x5C), new SKColor(0x0B, 0x25, 0x3A)], [0f, 0.5f, 1f], SKShaderTileMode.Clamp) })
            c.DrawRect(0, 330, W, H - 330, sea);
        using (var glint = new SKPaint { Color = new SKColor(0xFF, 0xD9, 0x8A, 150), IsAntialias = true })
            for (int i = 0; i < 7; i++) c.DrawRoundRect(500 - i * 6, 345 + i * 18, 40 + i * 12, 5, 2, 2, glint);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [AvaloniaFact]
    public async Task Generate()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PALETTE_STORE_ASSETS") == "1", "PALETTE_STORE_ASSETS=1 ile çalıştırın.");
        var dir = OutDir();

        // 512×512 simge: tam kanama (Play köşeleri kendisi yuvarlar).
        using (var icon = new SKBitmap(new SKImageInfo(512, 512, SKColorType.Rgba8888, SKAlphaType.Premul)))
        using (var c = new SKCanvas(icon))
        {
            c.Clear(Navy);
            DrawTiles(c, 0, 0, 512f / 108f);
            Save(icon, Path.Combine(dir, "icon-512.png"));
        }

        // 1024×500 öne çıkan grafik (alfa yok).
        using (var fg = new SKBitmap(new SKImageInfo(1024, 500, SKColorType.Rgba8888, SKAlphaType.Opaque)))
        using (var c = new SKCanvas(fg))
        {
            using (var bg = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(1024, 500), [Navy, new SKColor(0x2A, 0x3F, 0x6E)], null, SKShaderTileMode.Clamp) })
                c.DrawRect(0, 0, 1024, 500, bg);
            DrawTiles(c, 590, 20, 5.2f);
            using var title = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 64, Typeface = SKTypeface.FromFamilyName("Inter", SKFontStyle.Bold) ?? SKTypeface.Default };
            using var sub = new SKPaint { Color = new SKColor(0xD6, 0xE2, 0xF2), IsAntialias = true, TextSize = 30, Typeface = SKTypeface.FromFamilyName("Inter") ?? SKTypeface.Default };
            c.DrawText("Palet Çıkarıcı", 70, 230, title);
            c.DrawText("Görselden renk paleti", 70, 285, sub);
            c.DrawText("RGB · CMYK · HEX", 70, 330, sub);
            Save(fg, Path.Combine(dir, "feature-1024x500.png"));
        }

        // 1080×2160 telefon ekran görüntüleri.
        var png = SunsetPng();
        async Task Shot(string name, bool dark, bool cmyk, double scrollFraction)
        {
            var vm = new MainViewModel { UseCmyk = cmyk };
            var view = new MainView { DataContext = vm };
            var window = new Window { Width = 360, Height = 720, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            window.SetRenderScaling(3);
            await vm.LoadAsync("gun-batimi.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
            Dispatcher.UIThread.RunJobs();
            var scroll = view.FindControl<ScrollViewer>("PageScroll")!;
            scroll.Offset = new Vector(0, Math.Max(0, (scroll.Extent.Height - scroll.Viewport.Height) * scrollFraction));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Assert.Equal(1080, frame!.PixelSize.Width);
            frame.Save(Path.Combine(dir, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            window.Close();
        }
        await Shot("ekran-1-palet", dark: false, cmyk: false, 0);
        await Shot("ekran-2-kartlar", dark: false, cmyk: false, 0.6);
        await Shot("ekran-3-cmyk-koyu", dark: true, cmyk: true, 0.6);
        await Shot("ekran-4-koyu", dark: true, cmyk: false, 0);
    }
}
