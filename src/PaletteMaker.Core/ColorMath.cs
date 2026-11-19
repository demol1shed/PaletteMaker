using System.Globalization;

namespace PaletteMaker.Core;

/// <summary>OKLab uzayında renk değeri.</summary>
public readonly record struct Lab(double L, double A, double B);

/// <summary>Renk dönüşümleri ve biçimleme. Tüm metinler kültürden bağımsızdır (InvariantCulture).</summary>
public static class ColorMath
{
    private static double SrgbToLinear(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double LinearToSrgb(double c) =>
        c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    public static Lab RgbToOklab(double r, double g, double b)
    {
        double lr = SrgbToLinear(r / 255.0), lg = SrgbToLinear(g / 255.0), lb = SrgbToLinear(b / 255.0);
        double l = Math.Cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
        double m = Math.Cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
        double s = Math.Cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
        return new Lab(
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public static (byte R, byte G, byte B) OklabToRgb(Lab lab)
    {
        double l = lab.L + 0.3963377774 * lab.A + 0.2158037573 * lab.B;
        double m = lab.L - 0.1055613458 * lab.A - 0.0638541728 * lab.B;
        double s = lab.L - 0.0894841775 * lab.A - 1.2914855480 * lab.B;
        l = l * l * l; m = m * m * m; s = s * s * s;
        double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        double b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        return (ToByte(r), ToByte(g), ToByte(b));
    }

    private static byte ToByte(double linear)
    {
        double v = LinearToSrgb(Math.Clamp(linear, 0, 1)) * 255.0;
        return (byte)Math.Clamp((int)Math.Round(v), 0, 255);
    }

    public static string ToHex(byte r, byte g, byte b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");

    /// <summary>
    /// HEX metnini ayrıştırır: <c>#RRGGBB</c>, <c>RRGGBB</c>, <c>#RGB</c> (başta/sonda boşluk yok sayılır). Kültürden bağımsızdır.
    /// </summary>
    public static bool TryParseHex(string? text, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (text == null) return false;
        var s = text.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 3) s = string.Concat(s.Select(ch => new string(ch, 2)));
        if (s.Length != 6 || !s.All(Uri.IsHexDigit)) return false;
        r = byte.Parse(s.AsSpan(0, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        g = byte.Parse(s.AsSpan(2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        b = byte.Parse(s.AsSpan(4, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Profilsiz (ICC'siz) standart RGB → CMYK; yüzde olarak yuvarlanır.</summary>
    public static (int C, int M, int Y, int K) ToCmyk(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double k = 1 - Math.Max(rf, Math.Max(gf, bf));
        if (k >= 1) return (0, 0, 0, 100);
        double c = (1 - rf - k) / (1 - k);
        double m = (1 - gf - k) / (1 - k);
        double y = (1 - bf - k) / (1 - k);
        return (Pct(c), Pct(m), Pct(y), Pct(k));
    }

    private static int Pct(double v) => (int)Math.Round(v * 100, MidpointRounding.AwayFromZero);

    /// <summary>sRGB → HSL: ton 0–359°, doygunluk ve açıklık 0–100 (yuvarlanmış).</summary>
    public static (int H, int S, int L) ToHsl(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf));
        double d = max - min, l = (max + min) / 2, h = 0, s = 0;
        if (d > 1e-12)
        {
            s = d / (1 - Math.Abs(2 * l - 1));
            if (max == rf) h = ((gf - bf) / d) % 6;
            else if (max == gf) h = (bf - rf) / d + 2;
            else h = (rf - gf) / d + 4;
            h *= 60;
            if (h < 0) h += 360;
        }
        int hi = (int)Math.Round(h, MidpointRounding.AwayFromZero) % 360;
        return (hi, Pct(s), Pct(l));
    }

    /// <summary>sRGB → OKLCH (CSS Color 4): açıklık 0–1, kroma ≥ 0, ton 0–360°. Renksizde ton 0.</summary>
    public static (double L, double C, double H) ToOklch(byte r, byte g, byte b)
    {
        var lab = RgbToOklab(r, g, b);
        double c = Math.Sqrt(lab.A * lab.A + lab.B * lab.B);
        double h = c < 1e-4 ? 0 : Math.Atan2(lab.B, lab.A) * 180.0 / Math.PI;
        if (h < 0) h += 360;
        return (lab.L, c < 1e-4 ? 0 : c, h);
    }

    public static string FormatHsl(byte r, byte g, byte b)
    {
        var (h, s, l) = ToHsl(r, g, b);
        return string.Create(CultureInfo.InvariantCulture, $"hsl({h}, {s}%, {l}%)");
    }

    /// <summary>CSS biçimi: <c>oklch(62.8% 0.258 29.2)</c> (açıklık yüzde, 1 ondalık; kroma 3; ton 1).</summary>
    public static string FormatOklch(byte r, byte g, byte b)
    {
        var (l, c, h) = ToOklch(r, g, b);
        return string.Create(CultureInfo.InvariantCulture, $"oklch({l * 100:0.#}% {c:0.###} {h:0.#})");
    }

    public static string FormatRgb(byte r, byte g, byte b) =>
        string.Create(CultureInfo.InvariantCulture, $"rgb({r}, {g}, {b})");

    public static string FormatCmyk(byte r, byte g, byte b)
    {
        var (c, m, y, k) = ToCmyk(r, g, b);
        return string.Create(CultureInfo.InvariantCulture, $"cmyk({c}%, {m}%, {y}%, {k}%)");
    }
}
