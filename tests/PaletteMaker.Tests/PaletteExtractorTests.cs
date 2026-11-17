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
        Assert.Equal(48, PaletteOptions.MaxColorsLimit);
        Assert.Equal(0.4, PaletteOptions.DefaultMergeStrength);
        o.MaxColors = 99; Assert.Equal(48, o.MaxColors);
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

    // Sabit ton/doygunluk, orta açıklık aralığı (L 0.45–0.75).
    private static byte[] LightnessRamp() => TestImages.Rgba(256, 4, (x, _) =>
    {
        double L = 0.45 + 0.30 * x / 255.0;
        var (r, g, b) = ColorMath.OklabToRgb(new Lab(L, -0.03, -0.12));
        return (r, g, b, (byte)255);
    });

    [Fact]
    public void Single_hue_lightness_ramp_collapses_to_at_most_two_at_full_strength()
    {
        var pal = PaletteExtractor.Extract(LightnessRamp(), new PaletteOptions { MergeStrength = 1.0 });
        Assert.InRange(pal.Count, 1, 2);
    }

    [Fact]
    public void Single_hue_lightness_ramp_stays_within_a_few_tones_at_default()
    {
        var pal = PaletteExtractor.Extract(LightnessRamp());
        Assert.InRange(pal.Count, 1, 3);
    }

    [Fact]
    public void Close_but_distinct_tones_stay_apart_by_default_and_merge_at_full_strength()
    {
        // OKLab'da a ekseninde 0,08 fark: varsayılan eşiğin (≈0,062) üstünde, en yüksek eşiğin (0,14) altında.
        var (r1, g1, b1) = ColorMath.OklabToRgb(new Lab(0.6, -0.04, 0.0));
        var (r2, g2, b2) = ColorMath.OklabToRgb(new Lab(0.6, 0.04, 0.0));
        var buf = TestImages.Rgba(100, 10, (x, _) => x < 50 ? (r1, g1, b1, (byte)255) : (r2, g2, b2, (byte)255));
        Assert.Equal(2, PaletteExtractor.Extract(buf).Count);
        Assert.Single(PaletteExtractor.Extract(buf, new PaletteOptions { MergeStrength = 1.0 }));
    }

    [Fact]
    public void Higher_merge_strength_never_gives_more_colors()
    {
        var rnd = new Random(11);
        var buf = TestImages.Rgba(100, 100, (_, _) =>
            ((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)255));
        int previous = int.MaxValue;
        foreach (double strength in new[] { 0.0, 0.2, 0.4, 0.6, 0.8, 1.0 })
        {
            int count = PaletteExtractor.Extract(buf, new PaletteOptions { MaxColors = 48, MergeStrength = strength }).Count;
            Assert.True(count <= previous, $"güç {strength}: {count} renk, önceki {previous}");
            previous = count;
        }
    }

    /// <summary>
    /// 600×10, 48 birbirinden uzak renk şeridi: 36'sı %2,5, 12'si %0,83 paylı. Eski sabit %1 gürültü eşiği
    /// küçük 12'sini atardı; 48 renkte eşik ölçeklendiği için hepsi kalmalı.
    /// </summary>
    private static byte[] FortyEightStrips()
    {
        var colors = new List<(byte, byte, byte)>();
        foreach (var r in new byte[] { 30, 100, 170, 240 })
            foreach (var g in new byte[] { 30, 100, 170, 240 })
                foreach (var b in new byte[] { 40, 140, 230 })
                    colors.Add((r, g, b));
        var edges = new List<int>();
        int acc = 0;
        for (int i = 0; i < 48; i++) { acc += i < 36 ? 15 : 5; edges.Add(acc); }
        Assert.Equal(600, acc);
        return TestImages.Rgba(600, 10, (x, _) =>
        {
            int i = edges.FindIndex(e => x < e);
            var (r, g, b) = colors[i];
            return (r, g, b, (byte)255);
        });
    }

    [Fact]
    public void Forty_eight_distinct_colors_are_all_kept_at_max_48()
    {
        var pal = PaletteExtractor.Extract(FortyEightStrips(), new PaletteOptions { MaxColors = 48, MergeStrength = 0 });
        Assert.Equal(48, pal.Count);
        Assert.Equal(1.0, pal.Sum(c => c.Share), 6);
    }

    [Fact]
    public void Forty_eight_distinct_colors_are_capped_at_24_when_max_is_24()
    {
        var pal = PaletteExtractor.Extract(FortyEightStrips(), new PaletteOptions { MaxColors = 24, MergeStrength = 0 });
        Assert.Equal(24, pal.Count);
    }

    [Theory]
    [InlineData(2, 48)] [InlineData(10, 48)] [InlineData(24, 48)] [InlineData(32, 64)] [InlineData(48, 96)]
    public void Initial_cluster_count_grows_with_max_colors(int max, int expected) =>
        Assert.Equal(expected, PaletteExtractor.ClusterCount(max));

    [Theory]
    [InlineData(2, 0.01)] [InlineData(24, 0.01)] [InlineData(48, 0.25 / 48)]
    public void Noise_threshold_is_one_percent_up_to_24_colors_and_shrinks_above(int max, double expected) =>
        Assert.Equal(expected, PaletteExtractor.NoiseThreshold(max), 6);

    [Fact]
    public void Cancelled_token_stops_extraction()
    {
        var rnd = new Random(5);
        var buf = TestImages.Rgba(128, 128, (_, _) =>
            ((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)255));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => PaletteExtractor.Extract(buf, null, cts.Token));
    }

    [Fact]
    public void Worst_case_noise_at_48_colors_finishes_in_reasonable_time()
    {
        // Algoritmik patlamayı yakalamak için cömert üst sınır (masaüstünde ≈ 1,6 sn; her bir piksel ayrı kutu).
        var rnd = new Random(1);
        var buf = TestImages.Rgba(256, 256, (_, _) =>
            ((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)255));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pal = PaletteExtractor.Extract(buf, new PaletteOptions { MaxColors = 48 });
        Assert.InRange(pal.Count, 1, 48);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"{sw.Elapsed.TotalSeconds:0.0} sn sürdü");
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
    [InlineData(32)]
    [InlineData(48)]
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
