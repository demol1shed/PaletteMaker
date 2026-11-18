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

    // ---- Adobe: .ase ve .aco ------------------------------------------------------------------

    private static ushort U16(byte[] d, ref int p) { var v = (ushort)(d[p] << 8 | d[p + 1]); p += 2; return v; }
    private static uint U32(byte[] d, ref int p) { var v = (uint)d[p] << 24 | (uint)d[p + 1] << 16 | (uint)d[p + 2] << 8 | d[p + 3]; p += 4; return v; }
    private static string Name(byte[] d, ref int p)
    {
        int chars = U16(d, ref p); // sonlandırıcı \0 dahil
        var s = System.Text.Encoding.BigEndianUnicode.GetString(d, p, (chars - 1) * 2);
        p += chars * 2;
        Assert.Equal(0, d[p - 2] | d[p - 1]); // \0 ile bitiyor
        return s;
    }

    /// <summary>Küçük bir .ase okuyucu: ASEF başlığı, blok uzunlukları ve RGB float kanallarını spesifikasyona göre çözer.</summary>
    private static List<(string Name, float R, float G, float B)> ParseAse(byte[] d)
    {
        Assert.Equal("ASEF", System.Text.Encoding.ASCII.GetString(d, 0, 4));
        int p = 4;
        Assert.Equal(1, U16(d, ref p)); Assert.Equal(0, U16(d, ref p));
        uint blocks = U32(d, ref p);
        var colors = new List<(string, float, float, float)>();
        for (uint i = 0; i < blocks; i++)
        {
            Assert.Equal(0x0001, U16(d, ref p));
            int length = (int)U32(d, ref p), end = p + length;
            string name = Name(d, ref p);
            Assert.Equal("RGB ", System.Text.Encoding.ASCII.GetString(d, p, 4)); p += 4;
            float[] ch = new float[3];
            for (int k = 0; k < 3; k++)
            {
                ch[k] = System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(d.AsSpan(p, 4)); p += 4;
            }
            Assert.Equal(2, U16(d, ref p));
            Assert.Equal(end, p); // blok uzunluğu tam tutuyor
            colors.Add((name, ch[0], ch[1], ch[2]));
        }
        Assert.Equal(d.Length, p);
        return colors;
    }

    /// <summary>Küçük bir .aco okuyucu: v1 ve v2 bölümlerini okur; v2 adlarını döndürür.</summary>
    private static (List<(int R, int G, int B)> V1, List<(int R, int G, int B, string Name)> V2) ParseAco(byte[] d)
    {
        int p = 0;
        var v1 = new List<(int, int, int)>();
        var v2 = new List<(int, int, int, string)>();
        Assert.Equal(1, U16(d, ref p));
        int n1 = U16(d, ref p);
        for (int i = 0; i < n1; i++)
        {
            Assert.Equal(0, U16(d, ref p)); // RGB
            v1.Add((U16(d, ref p), U16(d, ref p), U16(d, ref p))); U16(d, ref p);
        }
        Assert.Equal(2, U16(d, ref p));
        int n2 = U16(d, ref p);
        for (int i = 0; i < n2; i++)
        {
            Assert.Equal(0, U16(d, ref p));
            int r = U16(d, ref p), g = U16(d, ref p), b = U16(d, ref p); U16(d, ref p);
            Assert.Equal(0, U16(d, ref p));
            v2.Add((r, g, b, Name(d, ref p)));
        }
        Assert.Equal(d.Length, p);
        return (v1, v2);
    }

    [Fact]
    public void AdobeSwatchExchange_header_and_first_block_golden()
    {
        var d = PaletteExport.AdobeSwatchExchange(Two);
        // "ASEF", sürüm 1.0, 2 blok; ilk blok: tür 0x0001, uzunluk 2+16+4+12+2=36, ad uzunluğu 8, "#E63946"
        Assert.Equal(Convert.FromHexString("41534546" + "00010000" + "00000002" + "0001" + "00000024" + "0008"), d[..20]);
        Assert.Equal(Convert.FromHexString("0023004500360033003900340036" + "0000" + "52474220"), d[20..40]);
    }

    [Fact]
    public void AdobeSwatchExchange_round_trips_through_a_parser()
    {
        var palette = Enumerable.Range(0, 48).Select(i => new PaletteColor((byte)(i * 5), (byte)(255 - i * 5), (byte)(i * 3), 1.0 / 48)).ToArray();
        var colors = ParseAse(PaletteExport.AdobeSwatchExchange(palette));
        Assert.Equal(48, colors.Count);
        for (int i = 0; i < palette.Length; i++)
        {
            Assert.Equal(palette[i].Hex, colors[i].Name);
            Assert.Equal(palette[i].R, (int)Math.Round(colors[i].R * 255));
            Assert.Equal(palette[i].G, (int)Math.Round(colors[i].G * 255));
            Assert.Equal(palette[i].B, (int)Math.Round(colors[i].B * 255));
        }
    }

    [Fact]
    public void AdobeSwatchExchange_extremes_are_exact()
    {
        var colors = ParseAse(PaletteExport.AdobeSwatchExchange([new(0, 0, 0, 0.5), new(255, 255, 255, 0.5)]));
        Assert.Equal((0f, 0f, 0f), (colors[0].R, colors[0].G, colors[0].B));
        Assert.Equal((1f, 1f, 1f), (colors[1].R, colors[1].G, colors[1].B));
    }

    [Fact]
    public void PhotoshopSwatches_golden()
    {
        var d = PaletteExport.PhotoshopSwatches([new(255, 0, 128, 1)]);
        string expected =
            "0001" + "0001" + "0000" + "FFFF" + "0000" + "8080" + "0000" +                    // v1
            "0002" + "0001" + "0000" + "FFFF" + "0000" + "8080" + "0000" + "0000" +           // v2 renk
            "0008" + "0023" + "0046" + "0046" + "0030" + "0030" + "0038" + "0030" + "0000";   // "#FF0080\0"
        Assert.Equal(Convert.FromHexString(expected), d);
    }

    [Fact]
    public void PhotoshopSwatches_round_trips_all_byte_values()
    {
        var palette = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i ^ 0x55), 1.0 / 256)).ToArray();
        var (v1, v2) = ParseAco(PaletteExport.PhotoshopSwatches(palette));
        Assert.Equal(256, v1.Count);
        Assert.Equal(256, v2.Count);
        for (int i = 0; i < palette.Length; i++)
        {
            Assert.Equal((palette[i].R * 257, palette[i].G * 257, palette[i].B * 257), v1[i]);
            Assert.Equal((v1[i].R, v1[i].G, v1[i].B, palette[i].Hex), v2[i]);
            Assert.Equal(palette[i].R, v1[i].R / 257); // 16 bit → 8 bit tam geri dönüş
        }
    }

    [Fact]
    public void Adobe_exports_of_an_empty_palette_are_valid()
    {
        Assert.Empty(ParseAse(PaletteExport.AdobeSwatchExchange([])));
        var (v1, v2) = ParseAco(PaletteExport.PhotoshopSwatches([]));
        Assert.Empty(v1);
        Assert.Empty(v2);
    }

    [Fact]
    public void Adobe_exports_are_culture_independent()
    {
        var before = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            var ase = PaletteExport.AdobeSwatchExchange(Two);
            var aco = PaletteExport.PhotoshopSwatches(Two);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");
            Assert.Equal(ase, PaletteExport.AdobeSwatchExchange(Two));
            Assert.Equal(aco, PaletteExport.PhotoshopSwatches(Two));
        }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = before; }
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
