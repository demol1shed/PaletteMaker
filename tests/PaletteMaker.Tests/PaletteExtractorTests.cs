using PaletteMaker.Core;
using Xunit;

namespace PaletteMaker.Tests;

public class PaletteExtractorTests
{
    [Fact]
    public void Defaults_are_as_specified()
    {
        var o = new PaletteOptions();
        Assert.Equal(10, o.MaxColors);
        Assert.Equal(2, PaletteOptions.MinColorsLimit);
        Assert.Equal(24, PaletteOptions.MaxColorsLimit);
        o.MaxColors = 99; Assert.Equal(24, o.MaxColors);
        o.MaxColors = 0; Assert.Equal(2, o.MaxColors);
    }

    [Fact]
    public void Three_solid_colors_give_exactly_three_with_shares()
    {
        // 60x10: kırmızı 50%, yeşil 30%, mavi 20%
        var buf = TestImages.Rgba(100, 10, (x, _) =>
            x < 50 ? ((byte)255, (byte)0, (byte)0, (byte)255) :
            x < 80 ? ((byte)0, (byte)255, (byte)0, (byte)255) :
                     ((byte)0, (byte)0, (byte)255, (byte)255));
        var pal = PaletteExtractor.Extract(buf);
        Assert.Equal(new[] { "#FF0000", "#00FF00", "#0000FF" }, pal.Select(c => c.Hex));
        Assert.Equal(0.5, pal[0].Share, 3);
        Assert.Equal(0.3, pal[1].Share, 3);
        Assert.Equal(0.2, pal[2].Share, 3);
    }

    [Fact]
    public void Single_hue_lightness_ramp_collapses_to_at_most_two()
    {
        // Sabit ton/doygunluk, orta açıklık aralığı (L 0.45–0.75).
        var buf = TestImages.Rgba(256, 4, (x, _) =>
        {
            double L = 0.45 + 0.30 * x / 255.0;
            var (r, g, b) = ColorMath.OklabToRgb(new Lab(L, -0.03, -0.12));
            return (r, g, b, (byte)255);
        });
        var pal = PaletteExtractor.Extract(buf);
        Assert.InRange(pal.Count, 1, 2);
    }

    [Fact]
    public void Distinct_hues_are_not_merged_at_default_strength()
    {
        var buf = TestImages.Rgba(100, 10, (x, _) =>
            x < 50 ? ((byte)200, (byte)30, (byte)30, (byte)255) : ((byte)30, (byte)30, (byte)200, (byte)255));
        Assert.Equal(2, PaletteExtractor.Extract(buf).Count);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(24)]
    [InlineData(2)]
    public void Random_noise_never_exceeds_max(int max)
    {
        var rnd = new Random(7);
        var buf = TestImages.Rgba(200, 200, (_, _) =>
            ((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)255));
        var pal = PaletteExtractor.Extract(buf, new PaletteOptions { MaxColors = max });
        Assert.InRange(pal.Count, 1, max);
        Assert.Equal(1.0, pal.Sum(c => c.Share), 6);
    }

    [Fact]
    public void Result_is_deterministic()
    {
        var rnd = new Random(3);
        var buf = TestImages.Rgba(120, 120, (_, _) =>
            ((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)255));
        var a = PaletteExtractor.Extract(buf).Select(c => c.Hex);
        var b = PaletteExtractor.Extract(buf).Select(c => c.Hex);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Transparent_pixels_are_ignored()
    {
        var buf = TestImages.Rgba(10, 10, (x, _) =>
            x < 5 ? ((byte)255, (byte)0, (byte)0, (byte)255) : ((byte)0, (byte)255, (byte)0, (byte)0));
        var pal = PaletteExtractor.Extract(buf);
        Assert.Single(pal);
        Assert.Equal("#FF0000", pal[0].Hex);
    }

    [Fact]
    public void Fully_transparent_gives_empty_palette()
    {
        var buf = TestImages.Rgba(10, 10, (_, _) => ((byte)1, (byte)2, (byte)3, (byte)0));
        Assert.Empty(PaletteExtractor.Extract(buf));
    }
}
