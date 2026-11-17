using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Monetization;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

public class FakeMonetization : ObservableObject, IMonetization
{
    private bool _showAds = true, _available = true, _privacy = true;
    public bool ShowAds { get => _showAds; set => SetProperty(ref _showAds, value); }
    public bool RemoveAdsAvailable { get => _available; set => SetProperty(ref _available, value); }
    public bool PrivacyOptionsRequired { get => _privacy; set => SetProperty(ref _privacy, value); }
    public string RemoveAdsText => "Reklamları kaldır (₺49,99)";
    public int PurchaseCalls, PrivacyCalls;

    public Task BuyRemoveAdsAsync()
    {
        PurchaseCalls++;
        ShowAds = false;
        RemoveAdsAvailable = false;
        return Task.CompletedTask;
    }

    public void ShowPrivacyOptions() => PrivacyCalls++;
}

/// <summary>Reklam alanı, "Reklamları kaldır" ve gizlilik düğmeleri: görünürlük, yerleşim ve davranış.</summary>
public class MonetizationTests
{
    private const double AdHeight = 60;

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
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<(Window W, MainView V, MainViewModel Vm)> OpenAsync(int width, int height, IMonetization? m, bool dark = false)
    {
        var vm = new MainViewModel();
        if (m != null) vm.Monetization = m;
        var view = new MainView { DataContext = vm };
        var window = new Window { Width = width, Height = height, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        var png = Png();
        await vm.LoadAsync("ornek.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
        Settle();
        return (window, view, vm);
    }

    private static Control FakeAd() => new Border { Height = AdHeight, Background = Brushes.Goldenrod, Child = new TextBlock { Text = "Test reklamı", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } };

    private static Rect BoundsIn(Visual v, Visual root) => new(v.TranslatePoint(new Point(0, 0), root)!.Value, v.Bounds.Size);

    [AvaloniaFact]
    public async Task Without_monetization_nothing_ad_related_is_shown()
    {
        var old = App.AdControlFactory; App.AdControlFactory = FakeAd;
        var (w, v, _) = await OpenAsync(360, 740, null);
        try
        {
            Assert.Null(v.FindControl<ContentControl>("AdSlot")!.Content);
            Assert.False(v.FindControl<Button>("RemoveAdsButton")!.IsVisible);
            Assert.False(v.FindControl<Button>("PrivacyOptionsButton")!.IsVisible);
        }
        finally { w.Close(); App.AdControlFactory = old; }
    }

    [AvaloniaFact]
    public async Task Ads_enabled_without_a_factory_creates_no_slot_content()
    {
        var old = App.AdControlFactory; App.AdControlFactory = null;
        var (w, v, _) = await OpenAsync(360, 740, new FakeMonetization());
        try { Assert.Null(v.FindControl<ContentControl>("AdSlot")!.Content); }
        finally { w.Close(); App.AdControlFactory = old; }
    }

    [AvaloniaTheory]
    [InlineData(360, 740, false)] [InlineData(360, 740, true)] [InlineData(412, 915, false)] [InlineData(915, 412, false)] [InlineData(940, 700, true)]
    public async Task Ad_is_the_last_thing_on_the_page_and_fits(int width, int height, bool dark)
    {
        var old = App.AdControlFactory; App.AdControlFactory = FakeAd;
        var m = new FakeMonetization();
        var (w, v, vm) = await OpenAsync(width, height, m, dark);
        try
        {
            var slot = v.FindControl<ContentControl>("AdSlot")!;
            Assert.NotNull(slot.Content);
            var scroll = v.FindControl<ScrollViewer>("PageScroll")!;
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5, $"yatay taşma {scroll.Extent.Width} > {scroll.Viewport.Width}");

            // Reklam paletin ve alt notun altında, düğmelerden önce; kaydırınca tamamen görünür.
            var list = v.FindControl<ItemsControl>("SwatchList")!;
            Assert.True(BoundsIn(slot, v).Top >= BoundsIn(list, v).Bottom - 0.5, "reklam paletin altında olmalı");
            Assert.Equal(AdHeight, slot.Bounds.Height, 0.5);
            var b = BoundsIn(slot, v);
            Assert.True(b.Left >= -0.5 && b.Right <= width + 0.5);

            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            Settle();
            var after = BoundsIn(slot, v);
            Assert.True(after.Bottom <= height + 0.5, $"kaydırınca reklam görünmeli, alt {after.Bottom} > {height}");

            // Düğmeler görünür ve dar düzende en az 40 px.
            var buy = v.FindControl<Button>("RemoveAdsButton")!;
            var priv = v.FindControl<Button>("PrivacyOptionsButton")!;
            Assert.True(buy.IsVisible && priv.IsVisible);
            if (width < MainView.NarrowBreakpoint)
            {
                Assert.True(buy.Bounds.Height >= 40, $"düğme {buy.Bounds.Height} px");
                Assert.True(priv.Bounds.Height >= 40, $"düğme {priv.Bounds.Height} px");
            }
            Assert.All(new[] { buy, priv }, c => { var r = BoundsIn(c, v); Assert.True(r.Left >= -0.5 && r.Right <= width + 0.5); });

            var dir = Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(dir);
            using var frame = w.CaptureRenderedFrame();
            frame!.Save(Path.Combine(dir, $"reklam-{width}x{height}-{(dark ? "koyu" : "acik")}-alt.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { w.Close(); App.AdControlFactory = old; }
    }

    [AvaloniaFact]
    public async Task Buying_remove_ads_hides_the_ad_and_the_button()
    {
        var old = App.AdControlFactory; App.AdControlFactory = FakeAd;
        var m = new FakeMonetization();
        var (w, v, _) = await OpenAsync(360, 740, m);
        try
        {
            var buy = v.FindControl<Button>("RemoveAdsButton")!;
            Assert.NotNull(v.FindControl<ContentControl>("AdSlot")!.Content);
            Assert.Equal("Reklamları kaldır (₺49,99)", buy.Content);

            buy.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Settle();

            Assert.Equal(1, m.PurchaseCalls);
            Assert.Null(v.FindControl<ContentControl>("AdSlot")!.Content);
            Assert.False(buy.IsVisible);
        }
        finally { w.Close(); App.AdControlFactory = old; }
    }

    [AvaloniaFact]
    public async Task Privacy_options_button_calls_the_consent_form()
    {
        var old = App.AdControlFactory; App.AdControlFactory = FakeAd;
        var m = new FakeMonetization();
        var (w, v, _) = await OpenAsync(360, 740, m);
        try
        {
            v.FindControl<Button>("PrivacyOptionsButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, m.PrivacyCalls);
            m.PrivacyOptionsRequired = false; Settle();
            Assert.False(v.FindControl<Button>("PrivacyOptionsButton")!.IsVisible);
        }
        finally { w.Close(); App.AdControlFactory = old; }
    }

    [AvaloniaFact]
    public async Task Swapping_monetization_at_runtime_updates_the_page()
    {
        var old = App.AdControlFactory; App.AdControlFactory = FakeAd;
        var (w, v, vm) = await OpenAsync(360, 740, null);
        try
        {
            Assert.Null(v.FindControl<ContentControl>("AdSlot")!.Content);
            vm.Monetization = new FakeMonetization(); Settle();
            Assert.NotNull(v.FindControl<ContentControl>("AdSlot")!.Content);
            vm.Monetization = NullMonetization.Instance; Settle();
            Assert.Null(v.FindControl<ContentControl>("AdSlot")!.Content);
        }
        finally { w.Close(); App.AdControlFactory = old; }
    }
}
