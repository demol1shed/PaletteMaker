using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;
using PaletteMaker.Localization;

namespace PaletteMaker.ViewModels;

public partial class SwatchViewModel : ObservableObject
{
    private readonly PaletteColor _color;

    public SwatchViewModel(PaletteColor color, ValueFormat format)
    {
        _color = color;
        _format = format;
        Brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    private ValueFormat _format;

    public IBrush Brush { get; }
    public string Hex => _color.Hex;

    public string ValueText => Format switch
    {
        ValueFormat.Cmyk => _color.CmykText,
        ValueFormat.Hsl => _color.HslText,
        ValueFormat.Oklch => _color.OklchText,
        _ => _color.RgbText,
    };

    public string ShareText => Loc.Format("ShareText", (_color.Share * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Ekran okuyucu için kartın tam açıklaması: "#E63946, 25%, rgb(230, 57, 70)".</summary>
    public string AccessibleName => $"{Hex}, {ShareText}, {ValueText}";
}
