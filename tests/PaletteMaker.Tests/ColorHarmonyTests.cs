using System.Globalization;
using System.Text.Json;
using PaletteMaker.Core;
using Xunit;

namespace PaletteMaker.Tests;

public class RybWheelTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(120, 60)] [InlineData(180, 120)] [InlineData(240, 240)] [InlineData(359.999, 359.999)]
    public void Anchors_map_as_documented(double ryb, double rgb) =>
        Assert.Equal(rgb, RybWheel.RybToRgbHue(ryb), 2);

    [Fact]
    public void Full_circle_wraps_to_zero() => Assert.Equal(0, RybWheel.RybToRgbHue(360), 9);

    [Fact]
    public void Mapping_is_strictly_monotone_and_round_trips()
    {
        double prev = -1;
        for (double h = 0; h < 360; h += 0.5)
        {
            double rgb = RybWheel.RybToRgbHue(h);
            Assert.True(rgb > prev, $"monoton değil: {h}° → {rgb}");
            prev = rgb;
            double back = RybWheel.RgbToRybHue(rgb);
            Assert.True(Math.Abs(back - h) < 0.5, $"gidiş-dönüş: {h} → {rgb} → {back}");
        }
    }

    [Fact]
    public void Hue_normalization_wraps()
    {
        Assert.Equal(350, RybWheel.Normalize(-10), 9);
        Assert.Equal(10, RybWheel.Normalize(370), 9);
        Assert.Equal(RybWheel.RybToRgbHue(20), RybWheel.RybToRgbHue(380), 9);
    }

    [Fact]
    public void Complement_of_red_is_green_yellow_is_purple_blue_is_orange()
    {
        var red = RybWheel.ToRgb(new Hsv(0, 1, 1));
        Assert.Equal((255, 0, 0), ((int)red.R, red.G, red.B));
        var green = RybWheel.ToRgb(new Hsv(180, 1, 1));
        Assert.True(green.G > green.R && green.G > green.B, $"yeşil bekleniyordu: {green}");
        var yellow = RybWheel.ToRgb(new Hsv(120, 1, 1));
        Assert.Equal(255, yellow.R); Assert.Equal(255, yellow.G); Assert.Equal(0, yellow.B);
        var purple = RybWheel.ToRgb(new Hsv(300, 1, 1));
        Assert.True(purple.R > 200 && purple.B > 200 && purple.G < 60, $"mor/macenta bekleniyordu: {purple}");
        var orange = RybWheel.ToRgb(new Hsv(60, 1, 1));
        Assert.Equal(255, orange.R); Assert.InRange((int)orange.G, 100, 160); Assert.Equal(0, orange.B);
    }

    [Theory]
    [InlineData(230, 57, 70)] [InlineData(42, 157, 143)] [InlineData(38, 70, 83)] [InlineData(244, 162, 97)]
    [InlineData(255, 255, 0)] [InlineData(0, 0, 255)] [InlineData(128, 128, 128)] [InlineData(255, 255, 255)] [InlineData(0, 0, 0)]
    public void Rgb_round_trips_through_hsv(int r, int g, int b)
    {
        var hsv = RybWheel.FromRgb((byte)r, (byte)g, (byte)b);
        var back = RybWheel.ToRgb(hsv);
        Assert.InRange(back.R - r, -1, 1);
        Assert.InRange(back.G - g, -1, 1);
        Assert.InRange(back.B - b, -1, 1);
    }

    [Fact]
    public void Gray_has_no_saturation()
    {
        var hsv = RybWheel.FromRgb(128, 128, 128);
        Assert.Equal(0, hsv.S, 9);
        Assert.Equal(128 / 255.0, hsv.V, 9);
    }
}

public class ColorHarmonyTests
{
    private static readonly Hsv Base = new(20, 0.8, 0.9);

    private static double Delta(double a, double b) => RybWheel.Normalize(a - b);

    private static bool HasHueOffset(IReadOnlyList<Hsv> colors, double offset) =>
        colors.Any(c =>
        {
            double d = Delta(c.H, Base.H + offset);
            return d < 0.01 || d > 359.99;
        });

    public static TheoryData<HarmonyRule> AllRules => new(Enum.GetValues<HarmonyRule>());

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Every_rule_gives_five_colors_with_the_base_in_the_middle(HarmonyRule rule)
    {
        var colors = ColorHarmony.Generate(rule, Base);
        Assert.Equal(5, colors.Count);
        Assert.Equal(Base, colors[ColorHarmony.BaseIndex]);
        Assert.All(colors, c =>
        {
            Assert.InRange(c.H, 0, 360);
            Assert.InRange(c.S, 0, 1);
            Assert.InRange(c.V, 0, 1);
        });
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Generation_is_deterministic_and_hue_offsets_match_the_table(HarmonyRule rule)
    {
        Assert.Equal(ColorHarmony.Generate(rule, Base), ColorHarmony.Generate(rule, Base));
        var colors = ColorHarmony.Generate(rule, Base);
        for (int i = 0; i < colors.Count; i++)
            Assert.True(Delta(colors[i].H, Base.H + ColorHarmony.HueOffset(rule, i)) is < 0.01 or > 359.99, $"{rule}[{i}]");
    }

    [Fact]
    public void Complementary_contains_the_opposite_hue() =>
        Assert.True(HasHueOffset(ColorHarmony.Generate(HarmonyRule.Complementary, Base), 180));

    [Fact]
    public void Triad_is_spaced_by_120_degrees()
    {
        var colors = ColorHarmony.Generate(HarmonyRule.Triad, Base);
        Assert.True(HasHueOffset(colors, 0));
        Assert.True(HasHueOffset(colors, 120));
        Assert.True(HasHueOffset(colors, 240));
    }

    [Fact]
    public void Square_is_spaced_by_90_degrees()
    {
        var colors = ColorHarmony.Generate(HarmonyRule.Square, Base);
        foreach (var off in new[] { 0, 90, 180, 270 }) Assert.True(HasHueOffset(colors, off), $"{off}°");
    }

    [Fact]
    public void Split_complementary_flanks_the_complement_by_30_degrees()
    {
        var colors = ColorHarmony.Generate(HarmonyRule.SplitComplementary, Base);
        Assert.True(HasHueOffset(colors, 0));
        Assert.True(HasHueOffset(colors, 150));
        Assert.True(HasHueOffset(colors, 210));
    }

    [Fact]
    public void Double_split_complementary_has_both_pairs()
    {
        var colors = ColorHarmony.Generate(HarmonyRule.DoubleSplitComplementary, Base);
        foreach (var off in new[] { -30, 30, 150, 210 }) Assert.True(HasHueOffset(colors, off), $"{off}°");
    }

    [Fact]
    public void Analogous_steps_by_30_degrees()
    {
        var colors = ColorHarmony.Generate(HarmonyRule.Analogous, Base);
        for (int i = 0; i < 5; i++)
            Assert.True(Delta(colors[i].H, Base.H + (i - 2) * 30) is < 0.01 or > 359.99, $"[{i}]");
    }

    [Theory]
    [InlineData(HarmonyRule.Monochromatic)] [InlineData(HarmonyRule.Shades)]
    public void Single_hue_rules_keep_the_hue_and_vary_the_tone(HarmonyRule rule)
    {
        var colors = ColorHarmony.Generate(rule, Base);
        Assert.All(colors, c => Assert.Equal(Base.H, c.H, 9));
        Assert.True(colors.Select(c => (Math.Round(c.S, 3), Math.Round(c.V, 3))).Distinct().Count() >= 4, "tonlar birbirinden farklı olmalı");
    }

    [Fact]
    public void Shades_keep_saturation()
    {
        var colors = ColorHarmony.Generate(HarmonyRule.Shades, Base);
        Assert.All(colors, c => Assert.Equal(Base.S, c.S, 9));
    }

    [Theory]
    [InlineData(-30)] [InlineData(0)] [InlineData(359.9)] [InlineData(400)] [InlineData(-725)]
    public void Hues_wrap_into_the_circle_for_any_base_hue(double hue)
    {
        foreach (var rule in Enum.GetValues<HarmonyRule>())
            Assert.All(ColorHarmony.Generate(rule, new Hsv(hue, 0.5, 0.5)), c => Assert.InRange(c.H, 0, 360));
    }

    [Fact]
    public void Gray_and_extreme_bases_stay_in_range()
    {
        foreach (var rule in Enum.GetValues<HarmonyRule>())
            foreach (var b in new[] { new Hsv(0, 0, 0), new Hsv(0, 0, 1), new Hsv(180, 1, 1), new Hsv(90, 2, -1) })
                Assert.All(ColorHarmony.Generate(rule, b), c => { Assert.InRange(c.S, 0, 1); Assert.InRange(c.V, 0, 1); });
    }
}

public class HexParsingTests
{
    [Theory]
    [InlineData("#E63946", 0xE6, 0x39, 0x46)] [InlineData("e63946", 0xE6, 0x39, 0x46)] [InlineData("  #e63946 ", 0xE6, 0x39, 0x46)]
    [InlineData("#fa0", 0xFF, 0xAA, 0x00)] [InlineData("000", 0, 0, 0)]
    public void Valid_inputs_parse(string text, int r, int g, int b)
    {
        Assert.True(ColorMath.TryParseHex(text, out var rr, out var gg, out var bb));
        Assert.Equal((r, g, b), ((int)rr, (int)gg, (int)bb));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("#")] [InlineData("#12")] [InlineData("#12345")] [InlineData("#1234567")]
    [InlineData("#GGGGGG")] [InlineData("##E63946")] [InlineData("E6 39 46")] [InlineData("#E63946FF")] [InlineData("+E6394")]
    public void Invalid_inputs_are_rejected(string? text) => Assert.False(ColorMath.TryParseHex(text, out _, out _, out _));

    [Fact]
    public void Parsing_is_culture_independent()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.True(ColorMath.TryParseHex("#abcdef", out var r, out var g, out var b));
            Assert.Equal((0xAB, 0xCD, 0xEF), ((int)r, (int)g, (int)b));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
}

public class ShareFlagTests
{
    private static readonly PaletteColor[] Palette = [new(230, 57, 70, 0), new(42, 157, 143, 0)];

    [Fact]
    public void Json_omits_share_when_asked_and_keeps_it_by_default()
    {
        using var without = JsonDocument.Parse(PaletteExport.Json(Palette, includeShare: false));
        Assert.All(without.RootElement.EnumerateArray(), e => Assert.False(e.TryGetProperty("share", out _)));
        using var with = JsonDocument.Parse(PaletteExport.Json(Palette));
        Assert.All(with.RootElement.EnumerateArray(), e => Assert.True(e.TryGetProperty("share", out _)));
    }

    [Fact]
    public void Card_without_share_still_renders_a_valid_png_with_the_same_colors()
    {
        var png = PaletteCardRenderer.RenderPng(Palette, showShare: false);
        using var bmp = SkiaSharp.SKBitmap.Decode(png);
        Assert.NotNull(bmp);
        var tile = PaletteCardRenderer.CardWidth / PaletteCardRenderer.ColumnsFor(Palette.Length);
        var px = bmp.GetPixel(tile / 2, 10); // karonun üst kısmı: yazı yok
        Assert.Equal((230, 57, 70), ((int)px.Red, (int)px.Green, (int)px.Blue));
    }
}
