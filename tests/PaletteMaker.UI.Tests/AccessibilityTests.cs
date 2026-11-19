using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

/// <summary>Ekran okuyucu adları, klavye ile kullanım ve metin kontrastı.</summary>
public class AccessibilityTests
{
    private static byte[] Png()
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

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<(Window W, ShellView V, MainViewModel Vm)> OpenAsync(bool dark = false)
    {
        var shell = new ShellViewModel(new MainViewModel());
        var vm = shell.Main;
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = 360, Height = 740, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        var png = Png();
        await vm.LoadAsync("ornek.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
        Settle();
        return (window, view, vm);
    }

    /// <summary>Ekran okuyucunun duyacağı ad: açık ad ya da düğme/metin içeriği.</summary>
    private static string? SpokenName(Control c) =>
        AutomationProperties.GetName(c) is { Length: > 0 } n ? n
        : c is ContentControl { Content: string s } ? s
        : c is TextBlock t ? t.Text : null;

    [AvaloniaTheory]
    [InlineData("en")] [InlineData("tr")]
    public async Task Every_interactive_control_has_a_spoken_name(string culture)
    {
        var old = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var (w, v, vm) = await OpenAsync();
            try
            {
                vm.IsExportOpen = true; Settle(); // alt sayfa öğeleri de sayılsın
                var interactive = v.GetVisualDescendants().OfType<Control>()
                    .Where(c => c.IsEffectivelyVisible && c.FindAncestorOfType<ComboBox>() == null && c.FindAncestorOfType<Slider>() == null && (c is Button or Slider or ComboBox || c.Name == "DropZone" || c.Classes.Contains("swatch") || c.Classes.Contains("valueChip")))
                    .ToList();
                Assert.NotEmpty(interactive);
                Assert.True(interactive.Count >= 20, $"beklenenden az öğe bulundu: {interactive.Count}");
                foreach (var c in interactive)
                    Assert.False(string.IsNullOrWhiteSpace(SpokenName(c)), $"{c.GetType().Name} '{c.Name}' ({culture}) için okunacak ad yok");

                var preview = v.GetVisualDescendants().OfType<Image>().First();
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(preview)));

                // Sürgüler hem ad hem yardım metni taşır.
                foreach (var slider in v.GetVisualDescendants().OfType<Slider>())
                {
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(slider)));
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(slider)));
                }

                // Kart adı kartın tüm bilgisini taşır; bildirim ve hata canlı bölgedir.
                var card = v.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("swatch"));
                Assert.Contains("#", AutomationProperties.GetName(card));
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(v.Find<Border>("Toast").Child!));
                Assert.Contains(v.GetVisualDescendants().OfType<TextBlock>(), t => AutomationProperties.GetLiveSetting(t) == AutomationLiveSetting.Assertive);
            }
            finally { w.Close(); }
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [AvaloniaFact]
    public async Task Cards_and_drop_zone_are_keyboard_focusable_and_the_card_copies_on_enter()
    {
        var (w, v, vm) = await OpenAsync();
        try
        {
            Assert.True(v.Find<Border>("DropZone").Focusable);
            var card = v.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("swatch"));
            Assert.True(card.Focusable);

            var clipboard = TopLevel.GetTopLevel(v)?.Clipboard;
            if (clipboard == null) { Assert.Skip("Headless oturum pano sağlamıyor"); return; }
            await clipboard.ClearAsync();

            card.Focus(); Settle();
            card.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = card });
            Settle();
            Assert.Equal(((SwatchViewModel)card.DataContext!).Hex, await clipboard.TryGetTextAsync());

            await clipboard.ClearAsync();
            card.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, Source = card });
            Settle();
            Assert.Equal(((SwatchViewModel)card.DataContext!).Hex, await clipboard.TryGetTextAsync());

            // Başka bir tuş kopyalamaz.
            await clipboard.ClearAsync();
            card.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A, Source = card });
            Settle();
            Assert.True(string.IsNullOrEmpty(await clipboard.TryGetTextAsync()));
        }
        finally { w.Close(); }
    }

    // ---- Kontrast (WCAG 2.x göreli parlaklık) -------------------------------------------------

    private static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    private static double Luminance(Color c) => 0.2126 * Lin(c.R / 255.0) + 0.7152 * Lin(c.G / 255.0) + 0.0722 * Lin(c.B / 255.0);
    private static Color Blend(Color fg, double opacity, Color bg) => Color.FromRgb(
        (byte)Math.Round(fg.R * opacity + bg.R * (1 - opacity)), (byte)Math.Round(fg.G * opacity + bg.G * (1 - opacity)), (byte)Math.Round(fg.B * opacity + bg.B * (1 - opacity)));
    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static Color BrushColor(ShellView v, string key, ThemeVariant variant)
    {
        Assert.True(v.TryFindResource(key, variant, out var res), $"kaynak yok: {key}");
        return ((ISolidColorBrush)res!).Color;
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Text_colors_meet_wcag_aa_contrast(bool dark)
    {
        var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var (w, v, _) = await OpenAsync(dark);
        try
        {
            var page = BrushColor(v, "SystemControlBackgroundAltHighBrush", variant);
            var text = BrushColor(v, "SystemControlForegroundBaseHighBrush", variant);

            // İpucu metinleri yarı saydam: gerçek zemin üzerinde karıştırılarak ölçülür (en düşük opaklık 0,6).
            Assert.True(Contrast(Blend(text, 0.6, page), page) >= 4.5, $"ipucu metni kontrastı ({(dark ? "koyu" : "açık")})");
            Assert.True(Contrast(Blend(text, 0.7, page), page) >= 4.5, "bölüm başlığı kontrastı");

            // Hata metni zemin üzerinde.
            var error = BrushColor(v, "ErrorText", variant);
            Assert.True(Contrast(error, page) >= 4.5, $"hata metni kontrastı {Contrast(error, page):0.00} ({(dark ? "koyu" : "açık")})");

            // Alt sayfa: metin, alt sayfa zemininde.
            var sheet = BrushColor(v, "SheetBackground", variant);
            Assert.True(Contrast(text, sheet) >= 4.5, "alt sayfa metni kontrastı");
            Assert.True(Contrast(Blend(text, 0.7, sheet), sheet) >= 4.5, "alt sayfa bölüm başlığı kontrastı");
        }
        finally { w.Close(); }
    }

    [Fact]
    public void Toast_text_is_white_on_a_dark_pill_with_enough_contrast()
    {
        // Bildirim: beyaz yazı, #E6202020 (≈ %90 opak koyu) zemin; en kötü durumda (beyaz sayfa) karışmış zemin.
        var pill = Blend(Color.FromRgb(0x20, 0x20, 0x20), 0xE6 / 255.0, Colors.White);
        Assert.True(Contrast(Colors.White, pill) >= 4.5, $"{Contrast(Colors.White, pill):0.00}");
    }
}
