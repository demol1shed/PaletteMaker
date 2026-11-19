using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaletteMaker.Core;
using PaletteMaker.Settings;

namespace PaletteMaker.ViewModels;

/// <summary>
/// Ayarlar ekranı. Her değişiklik anında <see cref="SettingsService"/> ile kaydedilir; tema anında uygulanır,
/// dil yeniden başlatınca (arayüz metinleri açılışta bir kez okunur), palet varsayılanları sonraki açılışta geçerlidir.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly LanguageChoice _startupLanguage;
    private bool _loading;

    public SettingsViewModel(SettingsService? service = null)
    {
        _service = service ?? new SettingsService();
        _startupLanguage = _service.Current.Language;
        Load();
    }

    /// <summary>Hex yazımı ya da kart tercihi değişince tetiklenir (kabuk iki palet ekranına iletir).</summary>
    public event EventHandler? OutputChanged;

    public AppSettings Settings => _service.Current;

    [ObservableProperty] private int _themeIndex;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RestartNeeded))] private int _languageIndex;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DefaultMaxColorsText))] private double _defaultMaxColors;
    [ObservableProperty] private double _defaultMergeStrength;
    [ObservableProperty] private int _defaultFormatIndex;
    [ObservableProperty] private bool _hexLowercase;
    [ObservableProperty] private bool _hexWithHash = true;
    [ObservableProperty] private bool _cardShowsShare = true;

    public string DefaultMaxColorsText => ((int)Math.Round(DefaultMaxColors)).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Seçilen dil açılıştaki dilden farklıysa "yeniden başlatınca uygulanır" notu gösterilir.</summary>
    public bool RestartNeeded => (LanguageChoice)LanguageIndex != _startupLanguage;

    [RelayCommand]
    private void ResetToDefaults()
    {
        _service.Reset();
        Load();
        Apply();
    }

    private void Load()
    {
        _loading = true;
        var s = _service.Current;
        ThemeIndex = (int)s.Theme;
        LanguageIndex = (int)s.Language;
        DefaultMaxColors = s.DefaultMaxColors;
        DefaultMergeStrength = s.DefaultMergeStrength;
        DefaultFormatIndex = s.DefaultValueFormat;
        HexLowercase = s.HexLowercase;
        HexWithHash = s.HexWithHash;
        CardShowsShare = s.CardShowsShare;
        _loading = false;
    }

    /// <summary>Sayfadaki değerleri ayar nesnesine yazar, kaydeder, tema ve çıktı tercihlerini yayar.</summary>
    private void Commit()
    {
        if (_loading) return;
        var s = _service.Current;
        s.Theme = (ThemeChoice)ThemeIndex;
        s.Language = (LanguageChoice)LanguageIndex;
        s.DefaultMaxColors = (int)Math.Round(DefaultMaxColors);
        s.DefaultMergeStrength = DefaultMergeStrength;
        s.DefaultValueFormat = DefaultFormatIndex;
        s.HexLowercase = HexLowercase;
        s.HexWithHash = HexWithHash;
        s.CardShowsShare = CardShowsShare;
        s.Normalize();
        _service.Save();
    }

    private void Apply()
    {
        ApplyTheme(_service.Current.Theme);
        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnThemeIndexChanged(int value) { Commit(); if (!_loading) ApplyTheme((ThemeChoice)value); }
    partial void OnLanguageIndexChanged(int value) => Commit();
    partial void OnDefaultMaxColorsChanged(double value) => Commit();
    partial void OnDefaultMergeStrengthChanged(double value) => Commit();
    partial void OnDefaultFormatIndexChanged(int value) => Commit();
    partial void OnHexLowercaseChanged(bool value) { Commit(); if (!_loading) OutputChanged?.Invoke(this, EventArgs.Empty); }
    partial void OnHexWithHashChanged(bool value) { Commit(); if (!_loading) OutputChanged?.Invoke(this, EventArgs.Empty); }
    partial void OnCardShowsShareChanged(bool value) { Commit(); if (!_loading) OutputChanged?.Invoke(this, EventArgs.Empty); }

    public static void ApplyTheme(ThemeChoice choice)
    {
        if (Application.Current is not { } app) return;
        app.RequestedThemeVariant = choice switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
