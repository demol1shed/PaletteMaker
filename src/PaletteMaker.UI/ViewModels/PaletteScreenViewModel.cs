using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;

namespace PaletteMaker.ViewModels;

/// <summary>
/// Bir palet gösteren ekranların ortak durumu (Görselden palet, Renk uyumu): kartlar, değer biçimi, bildirim ve
/// "Paylaş ve dışa aktar" alt sayfası. Alt sayfa ve bildirim kabukta (<c>ShellView</c>) çizilir ve etkin ekranın bu nesnesine bağlanır.
/// </summary>
public abstract partial class PaletteScreenViewModel : ObservableObject
{
    private CancellationTokenSource? _toastCts;

    public ObservableCollection<SwatchViewModel> Swatches { get; } = new();

    /// <summary>Şu anki palet (kartlarla aynı sırada).</summary>
    public IReadOnlyList<PaletteColor> Palette { get; private set; } = [];

    public bool HasPalette => Swatches.Count > 0;

    /// <summary>Palet bir görselden mi geliyor: öyleyse renklerin piksel payı vardır (kartta, JSON'da, PNG'de gösterilir).</summary>
    public abstract bool HasShares { get; }

    /// <summary>Kartta "uyumda kullan" düğmesi gösterilsin mi.</summary>
    public virtual bool OffersHarmonyAction => false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormatIndex))]
    private ValueFormat _valueFormat = ValueFormat.Rgb;

    /// <summary>Başlıktaki açılır menünün seçili sırası (<see cref="ValueFormat"/> ile aynı sıra).</summary>
    public int FormatIndex
    {
        get => (int)ValueFormat;
        set { if (Enum.IsDefined((ValueFormat)value)) ValueFormat = (ValueFormat)value; }
    }

    /// <summary>"Paylaş / dışa aktar" alt sayfası açık mı.</summary>
    [ObservableProperty] private bool _isExportOpen;

    [ObservableProperty] private string? _toastMessage;

    partial void OnValueFormatChanged(ValueFormat value)
    {
        foreach (var s in Swatches) s.Format = value;
    }

    /// <summary>Paleti ve kartlarını yeniler.</summary>
    protected void SetPalette(IReadOnlyList<PaletteColor> palette)
    {
        Swatches.Clear();
        Palette = palette;
        foreach (var c in palette) Swatches.Add(new SwatchViewModel(c, ValueFormat, HasShares, OffersHarmonyAction));
        OnPropertyChanged(nameof(Palette));
        OnPropertyChanged(nameof(HasPalette));
    }

    public async void ShowToast(string message)
    {
        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();
        ToastMessage = message;
        try { await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { return; }
        if (_toastCts == cts) ToastMessage = null;
    }

    public string AllHexText() => PaletteExport.HexList(Palette);

    /// <summary>Verilen türde palet metni (panoya kopyalama / dosyaya yazma).</summary>
    public string ExportText(ExportKind kind) => kind switch
    {
        ExportKind.Css => PaletteExport.CssVariables(Palette),
        ExportKind.Json => PaletteExport.Json(Palette, HasShares),
        ExportKind.AndroidColorsXml => PaletteExport.AndroidColorsXml(Palette),
        ExportKind.GimpPalette => PaletteExport.GimpPalette(Palette, BaseFileName),
        _ => PaletteExport.HexList(Palette),
    };

    /// <summary>Dosya adları için uzantısız, dosya sistemi için güvenli ad.</summary>
    public abstract string BaseFileName { get; }

    /// <summary>Örn. "gun-batimi-palette.gpl".</summary>
    public string ExportFileName(string extension) => $"{BaseFileName}-palette.{extension}";

    /// <summary>Paleti paylaşılabilir PNG kart olarak çizer (arka planda çağrılması önerilir).</summary>
    public byte[] RenderCardPng() => PaletteCardRenderer.RenderPng(Palette, HasShares);

    /// <summary>Adobe Swatch Exchange (.ase): Illustrator / InDesign / Photoshop.</summary>
    public byte[] ExportAse() => PaletteExport.AdobeSwatchExchange(Palette);

    /// <summary>Photoshop renk örnekleri (.aco).</summary>
    public byte[] ExportAco() => PaletteExport.PhotoshopSwatches(Palette);

    /// <summary>Dosya adından uzantısız, dosya sistemi için güvenli bir ad üretir (boşsa <paramref name="fallback"/>).</summary>
    protected static string SafeName(string? name, string fallback)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "" : Path.GetFileNameWithoutExtension(name);
        var safe = new string(baseName.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ' ? ch : '_').ToArray()).Trim();
        return safe.Length == 0 ? fallback : safe;
    }
}
