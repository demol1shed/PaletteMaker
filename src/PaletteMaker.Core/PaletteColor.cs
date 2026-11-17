namespace PaletteMaker.Core;

/// <summary>Paletteki tek renk. <see cref="Share"/> 0–1 arası piksel payıdır.</summary>
public sealed record PaletteColor(byte R, byte G, byte B, double Share)
{
    public string Hex => ColorMath.ToHex(R, G, B);
    public string RgbText => ColorMath.FormatRgb(R, G, B);
    public string CmykText => ColorMath.FormatCmyk(R, G, B);
    public string HslText => ColorMath.FormatHsl(R, G, B);
    public string OklchText => ColorMath.FormatOklch(R, G, B);
}
