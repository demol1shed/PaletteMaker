namespace PaletteMaker.Core;

/// <summary>
/// RYB (sanatçı) renk çarkında bir renk: ton <see cref="H"/> RYB derecesi (0–360; 0 kırmızı, 120 sarı, 240 mavi),
/// doygunluk ve parlaklık 0–1.
/// </summary>
public readonly record struct Hsv(double H, double S, double V);

/// <summary>
/// RYB tonu ile sRGB (HSV) tonu arasındaki eşleme ve <see cref="Hsv"/> ↔ sRGB dönüşümü. Eşleme parça parça doğrusal ve
/// kesin monotondur (yani tersi alınabilir); çapalar: kırmızı 0°, sarı 120° (RGB 60°), yeşil 180° (RGB 120°),
/// mavi 240° (RGB 240°). Böylece kırmızının tamamlayıcısı yeşil, sarınınki mor, mavinin ki turuncu olur.
/// </summary>
public static class RybWheel
{
    // (RYB derece, RGB derece) düğüm noktaları; ikisi de artan ve 0'dan 360'a gider.
    private static readonly (double Ryb, double Rgb)[] Knots =
    [
        (0, 0), (60, 30), (120, 60), (180, 120), (240, 240), (300, 300), (360, 360),
    ];

    public static double Normalize(double degrees)
    {
        double h = degrees % 360.0;
        return h < 0 ? h + 360.0 : h;
    }

    public static double RybToRgbHue(double rybHue) => Map(Normalize(rybHue), static k => k.Ryb, static k => k.Rgb);

    public static double RgbToRybHue(double rgbHue) => Map(Normalize(rgbHue), static k => k.Rgb, static k => k.Ryb);

    private static double Map(double h, Func<(double Ryb, double Rgb), double> from, Func<(double Ryb, double Rgb), double> to)
    {
        for (int i = 1; i < Knots.Length; i++)
        {
            double a = from(Knots[i - 1]), b = from(Knots[i]);
            if (h <= b)
            {
                double t = (h - a) / (b - a);
                return to(Knots[i - 1]) + t * (to(Knots[i]) - to(Knots[i - 1]));
            }
        }
        return to(Knots[^1]);
    }

    /// <summary>RYB tonlu rengi sRGB'ye çevirir.</summary>
    public static (byte R, byte G, byte B) ToRgb(Hsv color)
    {
        double s = Math.Clamp(color.S, 0, 1), v = Math.Clamp(color.V, 0, 1);
        double h = RybToRgbHue(color.H) / 60.0;           // 0–6
        double c = v * s;
        double x = c * (1 - Math.Abs(h % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = (int)Math.Floor(h) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),   // 5 ve h = 6 (360°)
        };
        return (ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    /// <summary>sRGB rengi RYB çarkındaki <see cref="Hsv"/> değerine çevirir. Renksizde ton 0'dır.</summary>
    public static Hsv FromRgb(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf));
        double d = max - min, h = 0;
        if (d > 1e-12)
        {
            if (max == rf) h = ((gf - bf) / d) % 6;
            else if (max == gf) h = (bf - rf) / d + 2;
            else h = (rf - gf) / d + 4;
            h = Normalize(h * 60);
        }
        return new Hsv(RgbToRybHue(h), max <= 0 ? 0 : d / max, max);
    }

    private static byte ToByte(double v) => (byte)Math.Clamp((int)Math.Round(v * 255.0), 0, 255);
}
