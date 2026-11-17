using System.Globalization;
using System.Text.Json;
using PaletteMaker.Core;
using Xunit;

namespace PaletteMaker.Tests;

public class ColorSpaceFormatTests
{
    [Theory]
    [InlineData(255, 0, 0, "hsl(0, 100%, 50%)")]
    [InlineData(0, 255, 0, "hsl(120, 100%, 50%)")]
    [InlineData(0, 0, 255, "hsl(240, 100%, 50%)")]
    [InlineData(128, 128, 128, "hsl(0, 0%, 50%)")]
    [InlineData(255, 255, 255, "hsl(0, 0%, 100%)")]
    [InlineData(0, 0, 0, "hsl(0, 0%, 0%)")]
    [InlineData(230, 57, 70, "hsl(355, 78%, 56%)")]   // #E63946
    [InlineData(255, 255, 0, "hsl(60, 100%, 50%)")]
    public void Hsl_matches_known_values(int r, int g, int b, string expected) =>
        Assert.Equal(expected, ColorMath.FormatHsl((byte)r, (byte)g, (byte)b));

    [Theory]
    [InlineData(255, 0, 0, "oklch(62.8% 0.258 29.2)")]
    [InlineData(0, 255, 0, "oklch(86.6% 0.295 142.5)")]
    [InlineData(0, 0, 255, "oklch(45.2% 0.313 264.1)")]
    [InlineData(255, 255, 255, "oklch(100% 0 0)")]
    [InlineData(0, 0, 0, "oklch(0% 0 0)")]
    [InlineData(128, 128, 128, "oklch(60% 0 0)")]
    public void Oklch_matches_css_reference_values(int r, int g, int b, string expected) =>
        Assert.Equal(expected, ColorMath.FormatOklch((byte)r, (byte)g, (byte)b));

    [Fact]
    public void Formats_are_culture_independent()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); // ondalık ayırıcı virgül
            Assert.Equal("oklch(62.8% 0.258 29.2)", ColorMath.FormatOklch(255, 0, 0));
            var c = new PaletteColor(230, 57, 70, 0.1234);
            Assert.Equal("hsl(355, 78%, 56%)", c.HslText);
            Assert.Contains("\"share\": 0.1234", PaletteExport.Json([c]));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Hsl_hue_stays_below_360() // 359.6° gibi değerler 360'a yuvarlanıp 0'a sarılmalı
    {
        for (int r = 250; r <= 255; r++)
            for (int b = 0; b <= 6; b++)
            {
                var (h, _, _) = ColorMath.ToHsl((byte)r, 0, (byte)b);
                Assert.InRange(h, 0, 359);
            }
    }
}

public class PaletteExportTests
{
    private static readonly PaletteColor[] Two =
    [
        new(230, 57, 70, 0.6),
        new(42, 157, 143, 0.4),
    ];

    [Fact]
    public void HexList_is_one_code_per_line_without_trailing_newline() =>
        Assert.Equal("#E63946\n#2A9D8F", PaletteExport.HexList(Two));

    [Fact]
    public void CssVariables_golden() =>
        Assert.Equal(":root {\n  --color-1: #E63946;\n  --color-2: #2A9D8F;\n}", PaletteExport.CssVariables(Two));

    [Fact]
    public void AndroidColorsXml_golden() =>
        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n    <color name=\"palette_1\">#E63946</color>\n    <color name=\"palette_2\">#2A9D8F</color>\n</resources>",
            PaletteExport.AndroidColorsXml(Two));

    [Fact]
    public void GimpPalette_golden() =>
        Assert.Equal(
            "GIMP Palette\nName: Test\nColumns: 2\n#\n230  57  70\t#E63946\n 42 157 143\t#2A9D8F\n",
            PaletteExport.GimpPalette(Two, "Test"));

    [Theory]
    [InlineData(1, 1)] [InlineData(5, 5)] [InlineData(8, 8)] [InlineData(9, 8)] [InlineData(48, 8)]
    public void GimpPalette_default_columns_are_at_most_eight(int count, int expectedColumns)
    {
        var palette = Enumerable.Range(0, count).Select(i => new PaletteColor((byte)i, 0, 0, 1.0 / count)).ToArray();
        Assert.Contains($"Columns: {expectedColumns}\n", PaletteExport.GimpPalette(palette));
    }

    [Fact]
    public void GimpPalette_name_cannot_inject_extra_lines()
    {
        var text = PaletteExport.GimpPalette(Two, "Evil\nColumns: 99\r\n230 0 0\tx");
        var lines = text.Split('\n');
        Assert.Equal("GIMP Palette", lines[0]);
        Assert.StartsWith("Name: ", lines[1]);
        Assert.DoesNotContain(lines, l => l == "Columns: 99");
        Assert.Equal(1, lines.Count(l => l.StartsWith("Columns:")));
    }

    [Fact]
    public void GimpPalette_blank_name_falls_back()
    {
        Assert.Contains("Name: PaletteMaker\n", PaletteExport.GimpPalette(Two, "  "));
        Assert.Contains("Name: PaletteMaker\n", PaletteExport.GimpPalette(Two));
    }

    /// <summary>Küçük bir .gpl okuyucu: GIMP, Resprite ve benzerlerinin beklediği yapıyı doğrular.</summary>
    private static (string Name, int Columns, List<(int R, int G, int B, string Label)> Colors) ParseGpl(string text)
    {
        var lines = text.Split('\n');
        Assert.Equal("GIMP Palette", lines[0]);
        string name = "";
        int columns = -1;
        var colors = new List<(int, int, int, string)>();
        foreach (var line in lines.Skip(1).Where(l => l.Length > 0))
        {
            if (line.StartsWith("Name:")) name = line["Name:".Length..].Trim();
            else if (line.StartsWith("Columns:")) columns = int.Parse(line["Columns:".Length..].Trim(), CultureInfo.InvariantCulture);
            else if (line.StartsWith('#')) continue;
            else
            {
                var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                Assert.True(parts.Length >= 3, $"renk satırı: '{line}'");
                int r = int.Parse(parts[0]), g = int.Parse(parts[1]), b = int.Parse(parts[2]);
                Assert.InRange(r, 0, 255); Assert.InRange(g, 0, 255); Assert.InRange(b, 0, 255);
                colors.Add((r, g, b, string.Join(' ', parts.Skip(3))));
            }
        }
        return (name, columns, colors);
    }

    [Fact]
    public void GimpPalette_round_trips_through_a_parser()
    {
        var palette = Enumerable.Range(0, 48).Select(i => new PaletteColor((byte)(i * 5), (byte)(255 - i * 5), (byte)(i * 3), 1.0 / 48)).ToArray();
        var (name, columns, colors) = ParseGpl(PaletteExport.GimpPalette(palette, "Gün batımı"));
        Assert.Equal("Gün batımı", name);
        Assert.Equal(8, columns);
        Assert.Equal(48, colors.Count);
        for (int i = 0; i < palette.Length; i++)
        {
            Assert.Equal((palette[i].R, palette[i].G, palette[i].B), ((byte)colors[i].R, (byte)colors[i].G, (byte)colors[i].B));
            Assert.Equal(palette[i].Hex, colors[i].Label);
        }
    }

    [Fact]
    public void Json_has_the_expected_structure()
    {
        using var doc = JsonDocument.Parse(PaletteExport.Json(Two));
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("#E63946", items[0].GetProperty("hex").GetString());
        Assert.Equal("rgb(230, 57, 70)", items[0].GetProperty("rgb").GetString());
        Assert.Equal("hsl(355, 78%, 56%)", items[0].GetProperty("hsl").GetString());
        Assert.StartsWith("oklch(", items[0].GetProperty("oklch").GetString());
        Assert.StartsWith("cmyk(", items[0].GetProperty("cmyk").GetString());
        Assert.Equal(0.6, items[0].GetProperty("share").GetDouble(), 6);
        Assert.DoesNotContain("\r", PaletteExport.Json(Two));
    }

    [Fact]
    public void Empty_palette_gives_valid_but_empty_outputs()
    {
        Assert.Equal("", PaletteExport.HexList([]));
        Assert.Equal(":root {\n}", PaletteExport.CssVariables([]));
        Assert.Equal("[]", PaletteExport.Json([]));
        Assert.Contains("Columns: 1\n", PaletteExport.GimpPalette([]));
    }
}

public class PaletteCardRendererTests
{
    [Theory]
    [InlineData(1, 1)] [InlineData(3, 3)] [InlineData(4, 3)] [InlineData(12, 3)] [InlineData(13, 4)]
    [InlineData(24, 4)] [InlineData(25, 6)] [InlineData(48, 6)]
    public void Columns_follow_the_color_count(int count, int expected) =>
        Assert.Equal(expected, PaletteCardRenderer.ColumnsFor(count));

    [Theory]
    [InlineData(1)] [InlineData(4)] [InlineData(10)] [InlineData(48)]
    public void Card_is_a_png_of_the_expected_size(int count)
    {
        var palette = Enumerable.Range(0, count).Select(i => new PaletteColor((byte)(i * 5), (byte)(100 + i), (byte)(255 - i * 5), 1.0 / count)).ToList();
        var png = PaletteCardRenderer.RenderPng(palette);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        using var codec = SkiaSharp.SKCodec.Create(new SkiaSharp.SKMemoryStream(png));
        int cols = PaletteCardRenderer.ColumnsFor(count), rows = (count + cols - 1) / cols, tile = PaletteCardRenderer.CardWidth / cols;
        Assert.Equal(tile * cols, codec.Info.Width);
        Assert.Equal(tile * rows, codec.Info.Height);
        Assert.True(codec.Info.Width <= PaletteCardRenderer.CardWidth);
    }

    [Fact]
    public void Card_round_trips_through_the_palette_extractor()
    {
        PaletteColor[] palette =
        [
            new(230, 57, 70, 0.25), new(42, 157, 143, 0.25), new(38, 70, 83, 0.25), new(244, 162, 97, 0.25),
        ];
        var decoded = ImageLoader.Decode(PaletteCardRenderer.RenderPng(palette));
        var extracted = PaletteExtractor.Extract(decoded.Rgba, new PaletteOptions { MaxColors = 8 });
        foreach (var original in palette)
        {
            Assert.Contains(extracted, e => Math.Abs(e.R - original.R) <= 6 && Math.Abs(e.G - original.G) <= 6 && Math.Abs(e.B - original.B) <= 6);
        }
    }

    [Fact]
    public void Unused_cells_in_the_last_row_are_light_not_black()
    {
        var palette = Enumerable.Range(0, 10).Select(i => new PaletteColor((byte)(20 * i), 40, 200, 0.1)).ToList(); // 3 sütun: son satırda 2 boş hücre
        using var bmp = SkiaSharp.SKBitmap.Decode(PaletteCardRenderer.RenderPng(palette));
        var empty = bmp.GetPixel(bmp.Width - 10, bmp.Height - 10);
        Assert.True(empty.Red > 200 && empty.Green > 200 && empty.Blue > 200, $"boş hücre {empty}");
    }

    [Fact]
    public void Empty_palette_is_rejected() =>
        Assert.Throws<ArgumentException>(() => PaletteCardRenderer.RenderPng([]));
}
