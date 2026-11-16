namespace PaletteMaker.Core;

public sealed class PaletteOptions
{
    public const int MinColorsLimit = 2;
    public const int MaxColorsLimit = 24;
    public const int DefaultMaxColors = 10;
    public const double DefaultMergeStrength = 0.5;

    private int _maxColors = DefaultMaxColors;
    private double _mergeStrength = DefaultMergeStrength;

    /// <summary>Paletteki en fazla renk sayısı (2–24).</summary>
    public int MaxColors
    {
        get => _maxColors;
        set => _maxColors = Math.Clamp(value, MinColorsLimit, MaxColorsLimit);
    }

    /// <summary>Yakın tonların birleştirilme gücü, 0 (az) – 1 (çok).</summary>
    public double MergeStrength
    {
        get => _mergeStrength;
        set => _mergeStrength = Math.Clamp(value, 0, 1);
    }
}
