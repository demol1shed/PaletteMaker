using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

/// <summary>
/// MainView'i telefon, tablet ve masaüstü boyutlarında açıp yerleşimi denetler ve her boyutun ekran
/// görüntüsünü <c>bin/…/screenshots</c> altına yazar (Android cihazı olmadan arayüzü görmek için).
/// </summary>
public class MainViewLayoutTests
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");

    /// <summary>Sekiz belirgin renk şeridinden oluşan örnek PNG.</summary>
    private static byte[] SamplePng()
    {
        SKColor[] colors =
        [
            new(0xE6, 0x39, 0x46), new(0xF4, 0xA2, 0x61), new(0xE9, 0xC4, 0x6A), new(0x2A, 0x9D, 0x8F),
            new(0x26, 0x46, 0x53), new(0x45, 0x7B, 0x9D), new(0x8E, 0x5B, 0xA8), new(0xF1, 0xFA, 0xEE),
        ];
        using var bmp = new SKBitmap(new SKImageInfo(400, 240, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bmp))
        {
            for (int i = 0; i < colors.Length; i++)
            {
                using var paint = new SKPaint { Color = colors[i] };
                canvas.DrawRect(i * 50, 0, 50, 240, paint);
            }
        }
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>8×6 = 48 birbirinden uzak renk karosu (400×300 PNG).</summary>
    private static byte[] FortyEightTilesPng()
    {
        using var bmp = new SKBitmap(new SKImageInfo(400, 300, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bmp))
        {
            int i = 0;
            foreach (var r in new byte[] { 30, 100, 170, 240 })
                foreach (var g in new byte[] { 30, 100, 170, 240 })
                    foreach (var b in new byte[] { 40, 140, 230 })
                    {
                        using var paint = new SKPaint { Color = new SKColor(r, g, b) };
                        canvas.DrawRect((i % 8) * 50, (i / 8) * 50, 50, 50, paint);
                        i++;
                    }
        }
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<(Window Window, MainView View, MainViewModel Vm)> OpenAsync(
        int width, int height, bool dark, bool loadImage = true, bool cmyk = false)
    {
        var vm = new MainViewModel { ValueFormat = cmyk ? ValueFormat.Cmyk : ValueFormat.Rgb };
        var view = new MainView { DataContext = vm };
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        if (loadImage)
        {
            var png = SamplePng();
            await vm.LoadAsync("ornek-gorsel.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
        }
        Settle();
        return (window, view, vm);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Window window, string name)
    {
        Settle();
        Directory.CreateDirectory(ScreenshotDir);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(ScreenshotDir, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static Rect BoundsIn(Visual v, Visual root)
    {
        var topLeft = v.TranslatePoint(new Point(0, 0), root);
        Assert.NotNull(topLeft);
        return new Rect(topLeft.Value, v.Bounds.Size);
    }

    public static TheoryData<string, int, int> Sizes => new()
    {
        { "telefon-kucuk", 360, 740 },
        { "telefon", 412, 915 },
        { "telefon-yatay", 915, 412 },
        { "tablet", 800, 1280 },
        { "masaustu", 940, 700 },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Sizes))]
    public async Task Load_error_shows_a_card_inside_the_image_area(string name, int width, int height)
    {
        foreach (bool dark in new[] { false, true })
        {
            var (window, view, vm) = await OpenAsync(width, height, dark, loadImage: false);
            try
            {
                var junk = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                await vm.LoadAsync("bozuk.png", () => Task.FromResult<Stream>(new MemoryStream(junk)));
                Settle();

                Assert.NotNull(vm.ErrorMessage);
                var card = view.FindControl<Border>("ErrorCard")!;
                var zone = view.FindControl<Border>("DropZone")!;
                Assert.True(card.IsEffectivelyVisible, "hata kartı görünmeli");
                var cb = BoundsIn(card, view);
                var zb = BoundsIn(zone, view);
                Assert.True(cb.Left >= zb.Left - 0.5 && cb.Right <= zb.Right + 0.5 && cb.Top >= zb.Top - 0.5 && cb.Bottom <= zb.Bottom + 0.5,
                    $"hata kartı görsel alanının dışında ({name}, koyu={dark}): kart {cb}, alan {zb}");
                Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == vm.ErrorMessage);
                // "Görsel seç" ipucu kartın altında çakışmasın.
                Assert.False(vm.ShowDropHint);
                Capture(window, $"hata-karti-{name}-{(dark ? "koyu" : "acik")}");

                // Yeni (geçerli) görsel seçilince kart kalkar.
                var png = SamplePng();
                await vm.LoadAsync("ornek-gorsel.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
                Settle();
                Assert.False(card.IsEffectivelyVisible);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Sizes))]
    public async Task Layout_fits_and_adapts(string name, int width, int height)
    {
        foreach (bool dark in new[] { false, true })
        {
            var (window, view, vm) = await OpenAsync(width, height, dark);
            try
            {
                Assert.Null(vm.ErrorMessage);
                Assert.True(vm.Swatches.Count >= 6, $"beklenen ≥6 renk, bulunan {vm.Swatches.Count}");

                // Düzen kırılma noktasına göre seçilir.
                Assert.Equal(width < MainView.NarrowBreakpoint, view.IsNarrow);
                Assert.Equal(MainView.ColumnsFor(width), view.SwatchColumns);

                // Yatay taşma / yatay kaydırma yok.
                var scroll = view.FindControl<ScrollViewer>("PageScroll")!;
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5,
                    $"yatay taşma: içerik {scroll.Extent.Width} > görünür alan {scroll.Viewport.Width}");

                var page = view.FindControl<StackPanel>("Page")!;
                // ComboBox şablonunun iç parçaları (açılır liste görünüm kutusu) ekran dışında ölçülür; ComboBox'ın kendisi denetlenir.
                foreach (var v in page.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.FindAncestorOfType<ComboBox>() == null))
                {
                    var b = BoundsIn(v, view);
                    Assert.True(b.Left >= -0.5 && b.Right <= width + 0.5,
                        $"{v.GetType().Name} '{v.Name}' yatayda dışarı taşıyor: {b.Left:0.#}–{b.Right:0.#} (genişlik {width})");
                }

                // Kartlar eşit genişlikte ve sütun sayısı kadar yan yana.
                var cards = view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("swatch")).ToList();
                Assert.Equal(vm.Swatches.Count, cards.Count);
                Assert.All(cards, c => Assert.Equal(cards[0].Bounds.Width, c.Bounds.Width, 0.5));
                Assert.True(cards[0].Bounds.Width >= 150, $"kart çok dar: {cards[0].Bounds.Width}");
                int firstRow = cards.Count(c => Math.Abs(BoundsIn(c, view).Top - BoundsIn(cards[0], view).Top) < 0.5);
                Assert.Equal(Math.Min(view.SwatchColumns, cards.Count), firstRow);

                // Ayarlar: genişte yan yana, darda alt alta.
                var settings = view.FindControl<UniformGrid>("Settings")!;
                var blocks = settings.Children.OfType<Control>().ToList();
                if (view.IsNarrow) Assert.True(BoundsIn(blocks[1], view).Top > BoundsIn(blocks[0], view).Bottom - 0.5);
                else Assert.Equal(BoundsIn(blocks[0], view).Top, BoundsIn(blocks[1], view).Top, 0.5);

                // Dar düzenin stil değerleri gerçekten uygulanıyor (yerel değer stili ezmiyor).
                Assert.Equal(view.IsNarrow ? 16 : 20, page.Margin.Left);
                double zone = height < MainView.ShortBreakpoint ? 150 : view.IsNarrow ? 200 : 230;
                Assert.Equal(zone, view.FindControl<Border>("DropZone")!.Bounds.Height);
                Assert.Equal(view.IsNarrow ? 20 : 24, view.FindControl<TextBlock>("TitleText")!.FontSize);

                if (view.IsNarrow)
                {
                    // Dokunma hedefleri en az 40 px.
                    var chips = view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("valueChip"));
                    Assert.All(chips, c => Assert.True(c.Bounds.Height >= 40, $"değer çipi {c.Bounds.Height} px"));
                    Assert.True(view.FindControl<Button>("CopyAllButton")!.Bounds.Height >= 40);
                    Assert.True(view.FindControl<Button>("ShareButton")!.Bounds.Height >= 40, "Paylaş düğmesi");
                    Assert.True(view.FindControl<ComboBox>("FormatBox")!.Bounds.Height >= 40, $"biçim menüsü {view.FindControl<ComboBox>("FormatBox")!.Bounds.Height} px");
                }

                string suffix = dark ? "koyu" : "acik";
                Capture(window, $"{name}-{width}x{height}-{suffix}");
                if (scroll.Extent.Height > scroll.Viewport.Height + 1)
                {
                    scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
                    Capture(window, $"{name}-{width}x{height}-{suffix}-alt");
                }
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public async Task Empty_state_and_cmyk_toast_render_on_phone()
    {
        var (empty, emptyView, emptyVm) = await OpenAsync(360, 740, dark: false, loadImage: false);
        try
        {
            Assert.False(emptyVm.HasImage);
            Assert.False(emptyView.FindControl<Button>("CopyAllButton")!.IsEnabled);
            Capture(empty, "telefon-kucuk-360x740-bos");
        }
        finally { empty.Close(); }

        // CMYK metni en uzun değer satırıdır; kopyalama bildirimi de görünür olmalı.
        var (window, view, vm) = await OpenAsync(360, 740, dark: true, cmyk: true);
        try
        {
            vm.ShowToast("#E63946 kopyalandı");
            Settle();
            var toast = view.FindControl<Border>("Toast")!;
            Assert.True(toast.IsEffectivelyVisible);
            var b = BoundsIn(toast, view);
            Assert.True(b.Left >= 0 && b.Right <= 360 && b.Bottom <= 740);
            Assert.All(vm.Swatches, s => Assert.StartsWith("cmyk(", s.ValueText));
            Capture(window, "telefon-kucuk-360x740-koyu-cmyk-bildirim");
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(320, 2)] [InlineData(360, 2)] [InlineData(412, 2)] [InlineData(599, 3)]
    [InlineData(600, 3)] [InlineData(915, 5)] [InlineData(940, 5)] [InlineData(1400, 6)] [InlineData(4000, 6)]
    public void Column_count_follows_width(double width, int expected) =>
        Assert.Equal(expected, MainView.ColumnsFor(width));

    [AvaloniaTheory]
    [InlineData(360, 740)] [InlineData(940, 700)]
    public async Task Forty_eight_colors_fit_without_horizontal_overflow(int width, int height)
    {
        var vm = new MainViewModel { MaxColors = 48, MergeStrength = 0 };
        var view = new MainView { DataContext = vm };
        var window = new Window { Width = width, Height = height, Content = view, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            var png = FortyEightTilesPng();
            await vm.LoadAsync("karolar.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
            Settle();

            Assert.Null(vm.ErrorMessage);
            Assert.Equal(48, vm.Swatches.Count);
            var cards = view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("swatch")).ToList();
            Assert.Equal(48, cards.Count);

            var scroll = view.FindControl<ScrollViewer>("PageScroll")!;
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5,
                $"yatay taşma: içerik {scroll.Extent.Width} > görünür alan {scroll.Viewport.Width}");
            Assert.All(cards, c =>
            {
                var b = BoundsIn(c, view);
                Assert.True(b.Left >= -0.5 && b.Right <= width + 0.5, $"kart dışarı taşıyor: {b.Left:0.#}–{b.Right:0.#}");
            });
            Capture(window, $"48-renk-{width}x{height}");
            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            Capture(window, $"48-renk-{width}x{height}-alt");
        }
        finally { window.Close(); }
    }
}
