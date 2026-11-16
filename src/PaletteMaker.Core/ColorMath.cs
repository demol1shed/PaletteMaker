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

    public static string FormatRgb(byte r, byte g, byte b) =>
        string.Create(CultureInfo.InvariantCulture, $"rgb({r}, {g}, {b})");

    public static string FormatCmyk(byte r, byte g, byte b)
    {
        var (c, m, y, k) = ToCmyk(r, g, b);
        return string.Create(CultureInfo.InvariantCulture, $"cmyk({c}%, {m}%, {y}%, {k}%)");
    }
}
