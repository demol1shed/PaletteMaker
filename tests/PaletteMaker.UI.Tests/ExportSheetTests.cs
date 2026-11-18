using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaletteMaker.Core;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

/// <summary>Değer biçimi menüsü ve "Paylaş ve dışa aktar" alt sayfası.</summary>
public class ExportSheetTests
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

    private static async Task<(Window W, MainView V, MainViewModel Vm)> OpenAsync(int width, int height, bool loadImage = true, bool dark = false, string fileName = "ornek.png")
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };
        var window = new Window { Width = width, Height = height, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        if (loadImage)
        {
            var png = Png();
            await vm.LoadAsync(fileName, () => Task.FromResult<Stream>(new MemoryStream(png)));
        }
        Settle();
        return (window, view, vm);
    }

    private static Rect BoundsIn(Visual v, Visual root) => new(v.TranslatePoint(new Point(0, 0), root)!.Value, v.Bounds.Size);
    private static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle(); }

    private static IEnumerable<Button> SheetButtons(MainView v) =>
        // Kaydırma çubuğunun ok/iz düğmeleri (RepeatButton) sayfa öğesi değildir; sayfa kaydırılırken ortaya çıkarlar.
        v.FindControl<Border>("ExportSheet")!.GetVisualDescendants().OfType<Button>().Where(b => b is not RepeatButton);

    private static Button SheetButton(MainView v, string tag) => SheetButtons(v).First(b => (b.Tag as string) == tag);

    [AvaloniaFact]
    public async Task Share_button_is_disabled_until_there_is_a_palette_and_opens_the_sheet()
    {
        var (w, v, vm) = await OpenAsync(360, 740, loadImage: false);
        try
        {
            var share = v.FindControl<Button>("ShareButton")!;
            Assert.False(share.IsEnabled);
            Click(share);
            Assert.False(vm.IsExportOpen);
            Assert.False(v.FindControl<Grid>("ExportSheetHost")!.IsVisible);

            var png = Png();
            await vm.LoadAsync("ornek.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
            Settle();
            Assert.True(share.IsEnabled);
            Click(share);
            Assert.True(vm.IsExportOpen);
            Assert.True(v.FindControl<Grid>("ExportSheetHost")!.IsVisible);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Sheet_closes_with_close_button_scrim_escape_and_back()
    {
        var (w, v, vm) = await OpenAsync(360, 740);
        try
        {
            void Open() { vm.IsExportOpen = true; Settle(); Assert.True(v.FindControl<Grid>("ExportSheetHost")!.IsVisible); }

            Open(); Click(v.FindControl<Button>("ExportCloseButton")!); Assert.False(vm.IsExportOpen);

            // Gerçek fare tıklaması: sayfanın üstündeki perde alanına (alt sayfa en az 48 px aşağıda başlar).
            Open();
            w.MouseDown(new Point(180, 20), MouseButton.Left); w.MouseUp(new Point(180, 20), MouseButton.Left); Settle();
            Assert.False(vm.IsExportOpen);

            Open(); v.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Settle();
            Assert.False(vm.IsExportOpen);

            // Android Geri tuşu: olay platformdan gelir, burada işleyiciyi doğrudan çağırıyoruz.
            Open();
            var back = typeof(MainView).GetMethod("OnBackRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var args = new RoutedEventArgs();
            back.Invoke(v, [null, args]);
            Settle();
            Assert.False(vm.IsExportOpen);
            Assert.True(args.Handled);

            // Alt sayfa kapalıyken Geri tuşu yutulmaz (uygulamadan çıkabilmeli).
            var args2 = new RoutedEventArgs();
            back.Invoke(v, [null, args2]);
            Assert.False(args2.Handled);
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("HexList", "#392E6E")] // yalnızca biçim denetimi aşağıda; ilk renk görsele göre değişir
    public async Task Copy_items_put_the_expected_text_on_the_clipboard(string tag, string _)
    {
        var (w, v, vm) = await OpenAsync(360, 740);
        try
        {
            var clipboard = TopLevel.GetTopLevel(v)?.Clipboard;
            if (clipboard == null) { Assert.Skip("Headless oturum pano sağlamıyor"); return; }

            foreach (var (kind, check) in new (string, Func<string, bool>)[]
            {
                ("HexList", t => t.Split('\n').All(l => l.Length == 7 && l[0] == '#') && t.Split('\n').Length == vm.Palette.Count),
                ("Css", t => t.StartsWith(":root {") && t.Contains("--color-1: #")),
                ("Json", t => t.StartsWith("[") && t.Contains("\"hex\"") && t.Contains("\"oklch\"")),
                ("AndroidColorsXml", t => t.Contains("<resources>") && t.Contains("name=\"palette_1\"")),
            })
            {
                vm.IsExportOpen = true; Settle();
                Click(SheetButton(v, kind));
                Assert.False(vm.IsExportOpen, $"{kind}: alt sayfa kapanmalı");
                var text = await clipboard.TryGetTextAsync();
                Assert.True(text != null && check(text), $"{kind}: beklenmeyen pano içeriği: {text}");
                Assert.False(string.IsNullOrEmpty(vm.ToastMessage));
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0, "rgb(")] [InlineData(1, "cmyk(")] [InlineData(2, "hsl(")] [InlineData(3, "oklch(")]
    public async Task Format_menu_changes_card_values(int index, string prefix)
    {
        var (w, v, vm) = await OpenAsync(360, 740);
        try
        {
            var box = v.FindControl<ComboBox>("FormatBox")!;
            Assert.Equal(4, box.Items.Count);
            box.SelectedIndex = index; Settle();
            Assert.Equal((ValueFormat)index, vm.ValueFormat);
            Assert.All(vm.Swatches, s => Assert.StartsWith(prefix, s.ValueText));
            // Ekran okuyucu adı kartın tüm bilgisini taşır.
            Assert.All(vm.Swatches, s => { Assert.Contains(s.Hex, s.AccessibleName); Assert.Contains(prefix, s.AccessibleName); });

            // Yeni palet hesaplanınca da seçili biçim korunur.
            vm.MaxColors = 3; await Task.Delay(400); Settle();
            Assert.All(vm.Swatches, s => Assert.StartsWith(prefix, s.ValueText));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Export_file_names_are_safe_and_derived_from_the_image_name()
    {
        var (w, _, vm) = await OpenAsync(360, 740, fileName: "gün batımı: v2?.png");
        try
        {
            Assert.Equal("gün batımı_ v2_", vm.BaseFileName);
            Assert.Equal("gün batımı_ v2_-palette.gpl", vm.ExportFileName("gpl"));
            Assert.Equal("gün batımı_ v2_-palette.ase", vm.ExportFileName("ase"));
            Assert.Equal("gün batımı_ v2_-palette.aco", vm.ExportFileName("aco"));
            Assert.Equal("ASEF", System.Text.Encoding.ASCII.GetString(vm.ExportAse(), 0, 4));
            Assert.Equal(vm.Palette.Count, vm.ExportAco()[3]); // v1 bölümünün renk sayısı (ushort'un düşük baytı)
            Assert.Contains("Name: gün batımı_ v2_\n", vm.ExportText(ExportKind.GimpPalette));
            vm.FileName = "klasör/alt/resim.jpg"; // yol benzeri adlarda yalnızca son parça kullanılır
            Assert.Equal("resim", vm.BaseFileName);
            vm.FileName = null;
            Assert.Equal("palette", vm.BaseFileName);
            Assert.Equal("palette-palette.png", vm.ExportFileName("png"));
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("en", 360, 740, false)] [InlineData("tr", 360, 740, true)]
    [InlineData("en", 412, 915, false)] [InlineData("tr", 915, 412, false)] [InlineData("en", 915, 412, true)]
    [InlineData("tr", 940, 700, false)] [InlineData("en", 320, 640, false)]
    public async Task Sheet_fits_the_window_in_both_languages(string culture, int width, int height, bool dark)
    {
        var old = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var (w, v, vm) = await OpenAsync(width, height, dark: dark);
            try
            {
                vm.IsExportOpen = true; Settle();
                var sheet = v.FindControl<Border>("ExportSheet")!;
                var b = BoundsIn(sheet, v);
                Assert.True(b.Left >= -0.5 && b.Right <= width + 0.5, $"{culture}: alt sayfa yatayda taşıyor {b.Left:0.#}–{b.Right:0.#}");
                Assert.True(b.Top >= 47.5 && b.Bottom <= height + 0.5, $"{culture}: alt sayfa dikeyde sığmıyor {b.Top:0.#}–{b.Bottom:0.#} / {height}");
                Assert.True(b.Width <= 460.5);

                // Alçak pencerede içerik sığmazsa sayfa içinde kaydırılabilir; en az başlık ve kapat düğmesi erişilebilir kalır.
                var close = v.FindControl<Button>("ExportCloseButton")!;
                var scroll = sheet.GetVisualDescendants().OfType<ScrollViewer>().First();
                scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)); Settle();
                var cb = BoundsIn(close, v);
                Assert.True(cb.Bottom <= height + 0.5, $"kapat düğmesi pencerenin dışında: {cb.Bottom:0.#} > {height}");
                if (width < MainView.NarrowBreakpoint)
                    Assert.All(SheetButtons(v), btn => Assert.True(btn.Bounds.Height >= 44, $"{btn.Content}: {btn.Bounds.Height:0.#} px"));

                var dir = Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(dir);
                scroll.Offset = default; Settle();
                using var frame = w.CaptureRenderedFrame();
                frame!.Save(Path.Combine(dir, $"sayfa-{culture}-{width}x{height}-{(dark ? "koyu" : "acik")}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            finally { w.Close(); }
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [AvaloniaFact]
    public async Task Sheet_with_48_colors_still_fits()
    {
        var (w, v, vm) = await OpenAsync(360, 740);
        try
        {
            vm.MaxColors = 48; await Task.Delay(400); Settle();
            vm.IsExportOpen = true; Settle();
            var b = BoundsIn(v.FindControl<Border>("ExportSheet")!, v);
            Assert.True(b.Bottom <= 740.5 && b.Right <= 360.5);
        }
        finally { w.Close(); }
    }
}
