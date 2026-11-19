using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;
using PaletteMaker.Localization;

namespace PaletteMaker.ViewModels;

public partial class SwatchViewModel : ObservableObject
{
    private readonly PaletteColor _color;

    /// <param name="showShare">Görselden gelen paletlerde piksel payı gösterilir; renk uyumunda gösterilmez.</param>
    /// <param name="offersHarmonyAction">Kartta "uyumda kullan" düğmesi var mı.</param>
    public SwatchViewModel(PaletteColor color, ValueFormat format, bool showShare = true, bool offersHarmonyAction = false)
    {
        _color = color;
        _format = format;
        ShowShare = showShare;
        OffersHarmonyAction = offersHarmonyAction;
        Brush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    private ValueFormat _format;

    public PaletteColor PaletteColor => _color;
    public bool ShowShare { get; }
    public bool OffersHarmonyAction { get; }
    public IBrush Brush { get; }
    public string Hex => _color.Hex;

    public string ValueText => Format switch
    {
        ValueFormat.Cmyk => _color.CmykText,
        ValueFormat.Hsl => _color.HslText,
        ValueFormat.Oklch => _color.OklchText,
        _ => _color.RgbText,
    };

    /// <summary>Görseldeki pay ("25%"); pay gösterilmeyen paletlerde boş.</summary>
    public string ShareText => ShowShare
        ? Loc.Format("ShareText", (_color.Share * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
        : "";

    /// <summary>Ekran okuyucu için kartın tam açıklaması: "#E63946, 25%, rgb(230, 57, 70)" (pay yoksa "#E63946, rgb(...)").</summary>
    public string AccessibleName => ShowShare ? $"{Hex}, {ShareText}, {ValueText}" : $"{Hex}, {ValueText}";

    /// <summary>"Uyumda kullan" düğmesinin ekran okuyucu adı: "#E63946 rengini uyum tabanı yap".</summary>
    public string HarmonyActionName => Loc.Format("UseInHarmonyNamed", Hex);
}
