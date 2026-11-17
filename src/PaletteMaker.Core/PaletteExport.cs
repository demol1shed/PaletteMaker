using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PaletteMaker.Core;

/// <summary>
/// Paleti metin biçimlerine çevirir (panoya kopyalama ve dosya olarak kaydetme için). Hepsi saf ve deterministiktir;
/// sayılar <see cref="CultureInfo.InvariantCulture"/> ile yazılır. Satır sonu her zaman <c>\n</c>'dir.
/// </summary>
public static class PaletteExport
{
    /// <summary>GIMP/Resprite paletinde varsayılan en çok sütun sayısı.</summary>
    public const int DefaultGimpColumns = 8;

    /// <summary>Satır satır HEX kodları: <c>#E63946</c>.</summary>
    public static string HexList(IReadOnlyList<PaletteColor> palette) =>
        string.Join("\n", palette.Select(c => c.Hex));

    /// <summary>CSS özel özellikleri: <c>--color-1: #E63946;</c> bir <c>:root</c> bloğunda.</summary>
    public static string CssVariables(IReadOnlyList<PaletteColor> palette)
    {
        var sb = new StringBuilder(":root {\n");
        for (int i = 0; i < palette.Count; i++)
            sb.Append(CultureInfo.InvariantCulture, $"  --color-{i + 1}: {palette[i].Hex};\n");
        return sb.Append('}').ToString();
    }

    /// <summary>Android <c>colors.xml</c>: <c>&lt;color name="palette_1"&gt;#E63946&lt;/color&gt;</c>.</summary>
    public static string AndroidColorsXml(IReadOnlyList<PaletteColor> palette)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n");
        for (int i = 0; i < palette.Count; i++)
            sb.Append(CultureInfo.InvariantCulture, $"    <color name=\"palette_{i + 1}\">{palette[i].Hex}</color>\n");
        return sb.Append("</resources>").ToString();
    }

    /// <summary>JSON dizisi: her renk için hex, rgb, cmyk, hsl, oklch metinleri ve görseldeki pay (0–1, 4 ondalık).</summary>
    public static string Json(IReadOnlyList<PaletteColor> palette)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartArray();
            foreach (var c in palette)
            {
                w.WriteStartObject();
                w.WriteString("hex", c.Hex);
                w.WriteString("rgb", c.RgbText);
                w.WriteString("cmyk", c.CmykText);
                w.WriteString("hsl", c.HslText);
                w.WriteString("oklch", c.OklchText);
                w.WriteNumber("share", Math.Round(c.Share, 4));
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    /// <summary>
    /// GIMP paleti (<c>.gpl</c>), Resprite, Aseprite, Krita, GIMP, Inkscape ve benzerlerinin içe aktardığı biçim:
    /// <c>GIMP Palette</c> / <c>Name:</c> / <c>Columns:</c> / <c>#</c> ve her renk için <c>RRR GGG BBB&lt;tab&gt;ad</c>
    /// (0–255, 3 genişliğe hizalı). Renk adı HEX koddur. Sütun sayısı verilmezse en çok 8.
    /// </summary>
    public static string GimpPalette(IReadOnlyList<PaletteColor> palette, string? name = null, int? columns = null)
    {
        string title = string.IsNullOrWhiteSpace(name) ? "PaletteMaker" : string.Join(' ', name.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        int cols = Math.Max(1, columns ?? Math.Min(Math.Max(palette.Count, 1), DefaultGimpColumns));
        var sb = new StringBuilder();
        sb.Append("GIMP Palette\n");
        sb.Append(CultureInfo.InvariantCulture, $"Name: {title}\n");
        sb.Append(CultureInfo.InvariantCulture, $"Columns: {cols}\n");
        sb.Append("#\n");
        foreach (var c in palette)
            sb.Append(CultureInfo.InvariantCulture, $"{c.R,3} {c.G,3} {c.B,3}\t{c.Hex}\n");
        return sb.ToString();
    }
}
