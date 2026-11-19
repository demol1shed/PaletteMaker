using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;
using PaletteMaker.Localization;

namespace PaletteMaker.ViewModels;

/// <summary>
/// Renk uyumu ekranı: RYB çarkında bir taban renk ve bir uyum kuralı seçilir, 5 renklik palet türetilir
/// (<see cref="ColorHarmony"/>). Hesap ucuzdur; her değişiklikte hemen yenilenir.
/// </summary>
public partial class HarmonyViewModel : PaletteScreenViewModel
{
    private bool _batch;

    public HarmonyViewModel()
    {
        Recompute();
    }

    public override bool HasShares => false;

    /// <summary>Kural sırası, <see cref="HarmonyRule"/> sırasıyla aynıdır (açılır menü).</summary>
    public int RuleIndex
    {
        get => (int)Rule;
        set { if (Enum.IsDefined((HarmonyRule)value)) Rule = (HarmonyRule)value; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuleIndex))]
    private HarmonyRule _rule = HarmonyRule.Analogous;

    /// <summary>Taban rengin RYB tonu (0–360).</summary>
    [ObservableProperty] private double _baseHue = 0;

    [ObservableProperty] private double _baseSaturation = 0.8;

    /// <summary>Parlaklık sürgüsü (0–1): tabanın ve türevlerinin parlaklığı.</summary>
    [ObservableProperty] private double _baseValue = 0.9;

    /// <summary>Çarkta işaretçi olarak gösterilen 5 renk (taban 2. sırada).</summary>
    public IReadOnlyList<Hsv> Colors { get; private set; } = [];

    /// <summary>HEX giriş kutusunun metni. Taban değişince tabanın HEX'ine döner.</summary>
    [ObservableProperty] private string _hexInput = "";

    /// <summary>Girilen HEX geçersiz mi (kutunun altında hata gösterilir).</summary>
    [ObservableProperty] private bool _hexInvalid;

    public Hsv Base => new(BaseHue, BaseSaturation, BaseValue);

    public string BaseHex => Palette.Count > ColorHarmony.BaseIndex ? Palette[ColorHarmony.BaseIndex].Hex : "";

    /// <summary>Çarkın ekran okuyucu adı (taban rengi söyler).</summary>
    public string WheelName => Loc.Format("WheelNamed", BaseHex);

    public override string BaseFileName => $"{Rule.ToString().ToLowerInvariant()}-{BaseHex.TrimStart('#')}";

    partial void OnRuleChanged(HarmonyRule value) => Recompute();
    partial void OnBaseHueChanged(double value) => Recompute();
    partial void OnBaseSaturationChanged(double value) => Recompute();
    partial void OnBaseValueChanged(double value) => Recompute();

    private void Recompute()
    {
        if (_batch) return;
        Colors = ColorHarmony.Generate(Rule, Base);
        var palette = new List<PaletteColor>(Colors.Count);
        foreach (var c in Colors)
        {
            var (r, g, b) = RybWheel.ToRgb(c);
            palette.Add(new PaletteColor(r, g, b, 0));
        }
        SetPalette(palette);
        OnPropertyChanged(nameof(Colors));
        OnPropertyChanged(nameof(Base));
        OnPropertyChanged(nameof(BaseHex));
        OnPropertyChanged(nameof(WheelName));
        HexInput = BaseHex;
        HexInvalid = false;
    }

    private void Batch(Action change)
    {
        _batch = true;
        try { change(); }
        finally { _batch = false; }
        Recompute();
    }

    /// <summary>Bir sRGB rengini taban yapar (görsel kartından köprü ve HEX girişi).</summary>
    public void SetBase(byte r, byte g, byte b)
    {
        var hsv = RybWheel.FromRgb(r, g, b);
        Batch(() =>
        {
            BaseHue = hsv.H;
            BaseSaturation = hsv.S;
            BaseValue = hsv.V;
        });
    }

    /// <summary>
    /// Çarkta <paramref name="index"/>. işaretçi sürüklenir: tüm uyum, işaretçi imlecin tonuna gelecek kadar döner.
    /// Taban işaretçisi sürüklenirken yarıçap doygunluğu da belirler.
    /// </summary>
    public void DragMarker(int index, double hue, double saturation)
    {
        if (index < 0 || index >= ColorHarmony.Count) return;
        Batch(() =>
        {
            BaseHue = RybWheel.Normalize(hue - ColorHarmony.HueOffset(Rule, index));
            if (index == ColorHarmony.BaseIndex) BaseSaturation = Math.Clamp(saturation, 0, 1);
        });
    }

    /// <summary>HEX kutusundaki metni uygular (Enter / odak kaybı). Geçersizse <see cref="HexInvalid"/> olur, taban değişmez.</summary>
    public void ApplyHexInput()
    {
        if (ColorMath.TryParseHex(HexInput, out var r, out var g, out var b))
        {
            SetBase(r, g, b);
            HexInvalid = false;
        }
        else
        {
            HexInvalid = true;
        }
    }
}
