using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using PaletteMaker.Core;
using PaletteMaker.Localization;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

/// <summary>Strings.resx (İngilizce, nötr) ve Strings.tr.resx tutarlılığı; iki dilde çalışma zamanı ve yerleşim.</summary>
public class LocalizationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PaletteMaker.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static Dictionary<string, string> ReadResx(string file)
    {
        var path = Path.Combine(RepoRoot(), "src", "PaletteMaker.UI", "Localization", file);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
    }

    private static int Placeholders(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().Count();

    [Fact]
    public void Turkish_and_neutral_resources_have_identical_keys()
    {
        var en = ReadResx("Strings.resx");
        var tr = ReadResx("Strings.tr.resx");
        Assert.Empty(en.Keys.Except(tr.Keys));
        Assert.Empty(tr.Keys.Except(en.Keys));
    }

    [Fact]
    public void No_value_is_empty_and_placeholders_match()
    {
        var en = ReadResx("Strings.resx");
        var tr = ReadResx("Strings.tr.resx");
        foreach (var (key, value) in en)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"EN '{key}' boş");
            Assert.False(string.IsNullOrWhiteSpace(tr[key]), $"TR '{key}' boş");
            Assert.True(Placeholders(value) == Placeholders(tr[key]), $"'{key}': yer tutucu sayısı EN/TR farklı");
        }
    }

    [Fact]
    public void Every_key_used_in_code_or_xaml_exists()
    {
        var keys = ReadResx("Strings.resx").Keys.ToHashSet();
        var used = new HashSet<string>();
        foreach (var dir in new[] { "src/PaletteMaker.UI", "src/PaletteMaker.Android" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), dir), "*.*", SearchOption.AllDirectories)
                         .Where(f => (f.EndsWith(".axaml") || f.EndsWith(".cs")) && !f.Contains("/obj/") && !f.Contains("/bin/")))
            {
                // Yorum satırlarındaki örnekler sayılmaz.
                var text = string.Join("\n", File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//")));
                foreach (Match m in Regex.Matches(text, @"\{loc:T (\w+)\}")) used.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(text, @"Loc\.(?:Get|Format)\(""([\w.]+)""")) used.Add(m.Groups[1].Value);
            }
        // MainViewModel.ErrorText anahtarları bir switch içinde (Loc.Get(kind switch …)) geçer.
        foreach (var e in Enum.GetValues<ImageLoadError>()) used.Add(Enum.GetName(e) switch
        {
            "UnsupportedType" => "ErrUnsupportedType", "TooLarge" => "ErrTooLarge", "ResolutionTooHigh" => "ErrResolutionTooHigh",
            "NotAnImage" => "ErrNotAnImage", "Unreadable" => "ErrUnreadable", "DecodeFailed" => "ErrDecodeFailed", _ => "ErrResizeFailed",
        });
        Assert.NotEmpty(used);
        Assert.Empty(used.Where(k => !keys.Contains(k) && !keys.Contains(k + ".Android")));
        Assert.Empty(keys.Where(k => !k.EndsWith(".Android") && !used.Contains(k) && k != "Err" ).Except(used)); // kullanılmayan anahtar kalmasın
    }

    [Fact]
    public void Every_load_error_has_a_localized_message_in_both_languages()
    {
        var old = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "en", "tr" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                foreach (var kind in Enum.GetValues<ImageLoadError>())
                {
                    var text = MainViewModel.ErrorText(kind);
                    Assert.False(text.StartsWith('!'), $"{culture}/{kind}: eksik anahtar {text}");
                    Assert.False(string.IsNullOrWhiteSpace(text));
                }
            }
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    // SwatchViewModel bir Avalonia fırçası yarattığı için headless oturumun UI iş parçacığında çalışmalı.
    [AvaloniaTheory]
    [InlineData("tr", "Tümünü kopyala", "%25")]
    [InlineData("tr-TR", "Tümünü kopyala", "%25")]
    [InlineData("en", "Copy all", "25%")]
    [InlineData("en-US", "Copy all", "25%")]
    [InlineData("de-DE", "Copy all", "25%")] // desteklenmeyen dil → İngilizce
    public void Runtime_language_follows_the_ui_culture(string culture, string copyAll, string share)
    {
        var old = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            Assert.Equal(copyAll, Loc.Get("CopyAll"));
            Assert.Equal(share, new SwatchViewModel(new PaletteColor(1, 2, 3, 0.25), ValueFormat.Rgb).ShareText);
            Assert.Equal(culture.StartsWith("tr") ? "Reklamları kaldır (₺49,99)" : "Remove ads (₺49,99)", Loc.Format("RemoveAdsPrice", "₺49,99"));
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [AvaloniaFact]
    public void Share_text_is_culture_independent_for_numbers()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); // ondalık ayırıcı virgül
            Assert.Equal("12.3%", new SwatchViewModel(new PaletteColor(1, 2, 3, 0.123), ValueFormat.Rgb).ShareText);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

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

    [AvaloniaTheory]
    [InlineData("tr", 360, 740)] [InlineData("en", 360, 740)]
    [InlineData("tr", 320, 640)] [InlineData("en", 320, 640)]
    [InlineData("tr", 915, 412)] [InlineData("en", 915, 412)]
    [InlineData("tr", 940, 700)] [InlineData("en", 940, 700)]
    public async Task Layout_fits_in_both_languages(string culture, int width, int height)
    {
        var old = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var vm = new MainViewModel();
            var view = new MainView { DataContext = vm };
            var window = new Window { Width = width, Height = height, Content = view, RequestedThemeVariant = ThemeVariant.Light };
            window.Show();
            try
            {
                var png = Png();
                await vm.LoadAsync("ornek.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();

                var scroll = view.FindControl<ScrollViewer>("PageScroll")!;
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5, $"{culture}: yatay taşma {scroll.Extent.Width} > {scroll.Viewport.Width}");
                Assert.Equal(culture == "tr" ? "Palet Çıkarıcı" : "Palette Extractor", view.FindControl<TextBlock>("TitleText")!.Text);
                Assert.Equal(culture == "tr" ? "Tümünü kopyala" : "Copy all", view.FindControl<Button>("CopyAllButton")!.Content);
                if (width < MainView.NarrowBreakpoint) Assert.True(view.FindControl<Button>("CopyAllButton")!.Bounds.Height >= 40);

                var dir = Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(dir);
                using var frame = window.CaptureRenderedFrame();
                frame!.Save(Path.Combine(dir, $"dil-{culture}-{width}x{height}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            finally { window.Close(); }
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }
}
