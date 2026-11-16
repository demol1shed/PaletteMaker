using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;

namespace PaletteMaker.ViewModels;

public partial class SwatchViewModel : ObservableObject
{
    private readonly PaletteColor _color;

    public SwatchViewModel(PaletteColor color, bool useCmyk)
    {
        _color = color;
        _useCmyk = useCmyk;
        Brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    private bool _useCmyk;

    public IBrush Brush { get; }
    public string Hex => _color.Hex;
    public string ValueText => UseCmyk ? _color.CmykText : _color.RgbText;
    public string ShareText => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"%{_color.Share * 100:0.#}");
}
