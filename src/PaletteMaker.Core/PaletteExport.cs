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

    /// <summary>
    /// Adobe Swatch Exchange (<c>.ase</c>): Illustrator, InDesign ve Photoshop'un (2020+) renk örnekleri paneline aktarılan
    /// ikili biçim. <c>ASEF</c> başlığı, ardından renk başına bir blok (ad = HEX kod, RGB modeli, 0–1 aralığında 3 float32).
    /// Düz liste yazılır (grup bloğu yok). Sayılar big-endian, dizeler UTF-16BE ve <c>\0</c> ile biter.
    /// </summary>
    public static byte[] AdobeSwatchExchange(IReadOnlyList<PaletteColor> palette)
    {
        using var ms = new MemoryStream();
        Span<byte> buf = stackalloc byte[4];
        ms.Write("ASEF"u8);
        WriteU16(ms, 1); // sürüm 1.0
        WriteU16(ms, 0);
        WriteU32(ms, (uint)palette.Count);
        foreach (var c in palette)
        {
            string name = c.Hex;
            int nameChars = name.Length + 1; // sonlandırıcı \0 dahil
            WriteU16(ms, 0x0001); // renk bloğu
            WriteU32(ms, (uint)(2 + nameChars * 2 + 4 + 12 + 2));
            WriteName(ms, name);
            ms.Write("RGB "u8);
            foreach (byte channel in (ReadOnlySpan<byte>)[c.R, c.G, c.B])
            {
                System.Buffers.Binary.BinaryPrimitives.WriteSingleBigEndian(buf, channel / 255f);
                ms.Write(buf);
            }
            WriteU16(ms, 2); // 0 = global, 1 = spot, 2 = normal
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Photoshop renk örnekleri (<c>.aco</c>): önce adsız sürüm 1 bölümü (eski okuyucular), ardından adlı sürüm 2 bölümü
    /// (Photoshop adları buradan okur). RGB renk uzayı (0), kanallar 16 bit (<c>değer × 257</c>, yani 255 → 65535).
    /// Sayılar big-endian, ad UTF-16BE ve <c>\0</c> ile biter. Renk adı HEX koddur.
    /// </summary>
    public static byte[] PhotoshopSwatches(IReadOnlyList<PaletteColor> palette)
    {
        using var ms = new MemoryStream();
        foreach (int version in (ReadOnlySpan<int>)[1, 2])
        {
            WriteU16(ms, (ushort)version);
            WriteU16(ms, (ushort)palette.Count);
            foreach (var c in palette)
            {
                WriteU16(ms, 0); // RGB
                WriteU16(ms, (ushort)(c.R * 257));
                WriteU16(ms, (ushort)(c.G * 257));
                WriteU16(ms, (ushort)(c.B * 257));
                WriteU16(ms, 0);
                if (version == 2)
                {
                    WriteU16(ms, 0);
                    WriteName(ms, c.Hex);
                }
            }
        }
        return ms.ToArray();
    }

    private static void WriteU16(Stream s, ushort v) => s.Write([(byte)(v >> 8), (byte)v]);

    private static void WriteU32(Stream s, uint v) => s.Write([(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]);

    /// <summary>Uzunluk öneki (karakter, sonlandırıcı dahil) + UTF-16BE ad + <c>\0</c>.</summary>
    private static void WriteName(Stream s, string name)
    {
        WriteU16(s, (ushort)(name.Length + 1));
        s.Write(Encoding.BigEndianUnicode.GetBytes(name));
        WriteU16(s, 0);
    }
}
