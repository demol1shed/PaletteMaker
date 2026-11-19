using System.Globalization;
using PaletteMaker.Core;
using Xunit;

namespace PaletteMaker.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "palettemaker-settings-" + Guid.NewGuid().ToString("N"));
    private string PathIn(string name) => Path.Combine(_dir, name);

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var s = AppSettingsStore.Load(PathIn("yok.json"));
        Assert.Equal(PaletteOptions.DefaultMaxColors, s.DefaultMaxColors);
        Assert.Equal(ThemeChoice.System, s.Theme);
        Assert.True(s.HexWithHash);
        Assert.True(s.CardShowsShare);
        Assert.Equal(default, s.HexStyle);
    }

    [Fact]
    public void Save_then_load_round_trips_and_creates_the_folder()
    {
        var path = PathIn("a/b/settings.json");
        var s = new AppSettings
        {
            Theme = ThemeChoice.Dark, Language = LanguageChoice.Turkish, DefaultMaxColors = 17,
            DefaultMergeStrength = 0.25, DefaultValueFormat = 3, HexLowercase = true, HexWithHash = false, CardShowsShare = false,
        };
        Assert.True(AppSettingsStore.Save(path, s));
        Assert.False(File.Exists(path + ".tmp"));

        var back = AppSettingsStore.Load(path);
        Assert.Equal(ThemeChoice.Dark, back.Theme);
        Assert.Equal(LanguageChoice.Turkish, back.Language);
        Assert.Equal(17, back.DefaultMaxColors);
        Assert.Equal(0.25, back.DefaultMergeStrength);
        Assert.Equal(3, back.DefaultValueFormat);
        Assert.True(back.HexLowercase);
        Assert.False(back.HexWithHash);
        Assert.False(back.CardShowsShare);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"Theme\":\"Neon\"}")]
    public void Corrupt_file_gives_defaults(string content)
    {
        Directory.CreateDirectory(_dir);
        var path = PathIn("bozuk.json");
        File.WriteAllText(path, content);
        var s = AppSettingsStore.Load(path);
        Assert.Equal(ThemeChoice.System, s.Theme);
        Assert.Equal(PaletteOptions.DefaultMaxColors, s.DefaultMaxColors);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        Directory.CreateDirectory(_dir);
        var path = PathIn("aralik.json");
        File.WriteAllText(path, "{\"DefaultMaxColors\":999,\"DefaultMergeStrength\":-4,\"DefaultValueFormat\":9,\"Theme\":7}");
        var s = AppSettingsStore.Load(path);
        Assert.Equal(PaletteOptions.MaxColorsLimit, s.DefaultMaxColors);
        Assert.Equal(0, s.DefaultMergeStrength);
        Assert.Equal(0, s.DefaultValueFormat);
        Assert.Equal(ThemeChoice.System, s.Theme);
    }

    [Fact]
    public void Null_path_never_writes()
    {
        Assert.False(AppSettingsStore.Save(null, new AppSettings()));
        Assert.Equal(PaletteOptions.DefaultMaxColors, AppSettingsStore.Load(null).DefaultMaxColors);
    }

    [Fact]
    public void Save_and_load_do_not_depend_on_the_culture()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var path = PathIn("tr.json");
            AppSettingsStore.Save(path, new AppSettings { DefaultMergeStrength = 0.35 });
            Assert.Contains("0.35", File.ReadAllText(path));
            Assert.Equal(0.35, AppSettingsStore.Load(path).DefaultMergeStrength);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Theory]
    [InlineData(false, false, "#E63946")]
    [InlineData(true, false, "#e63946")]
    [InlineData(false, true, "E63946")]
    [InlineData(true, true, "e63946")]
    public void HexStyle_applies(bool lower, bool omitHash, string expected) =>
        Assert.Equal(expected, new HexStyle(lower, omitHash).Apply("#E63946"));

    [Fact]
    public void HexStyle_lowercase_is_culture_invariant()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal("#aabbcc", new HexStyle(true).Apply("#AABBCC"));
            Assert.Equal("#ffffff", new HexStyle(true).Apply("#FFFFFF"));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Exports_follow_the_hex_style_but_code_formats_keep_the_hash()
    {
        PaletteColor[] p = [new(0xE6, 0x39, 0x46, 0.5), new(0x2A, 0x9D, 0x8F, 0.5)];
        var style = new HexStyle(Lowercase: true, OmitHash: true);
        Assert.Equal("e63946\n2a9d8f", PaletteExport.HexList(p, style));
        Assert.Contains("--color-1: #e63946;", PaletteExport.CssVariables(p, style));
        Assert.Contains(">#e63946<", PaletteExport.AndroidColorsXml(p, style));
        Assert.Contains("\"hex\": \"#e63946\"", PaletteExport.Json(p, true, style));
        // Varsayılan davranış değişmedi.
        Assert.Equal("#E63946\n#2A9D8F", PaletteExport.HexList(p));
    }
}
