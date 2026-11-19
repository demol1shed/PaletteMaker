using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;

namespace PaletteMaker.ViewModels;

/// <summary>Alt gezinme çubuğundaki hedefler; sıra çubuktaki soldan sağa sıradır.</summary>
public enum ShellTab
{
    Settings = 0,
    Image = 1,
    Harmony = 2,
}

/// <summary>Henüz içeriği olmayan Ayarlar ekranı.</summary>
public sealed class SettingsViewModel : ObservableObject;

/// <summary>
/// Uygulama kabuğu: seçili sekme ve üç ekranın görünüm modelleri. Etkin palet ekranının durumuna (alt sayfa, bildirim)
/// kabuk bağlanır. Android etkinliği yeniden oluşabildiği için <c>App</c> tarafından tek örnek olarak tutulur.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    public ShellViewModel(MainViewModel main, HarmonyViewModel? harmony = null, SettingsViewModel? settings = null)
    {
        Main = main;
        Harmony = harmony ?? new HarmonyViewModel();
        Settings = settings ?? new SettingsViewModel();
        Harmony.ValueFormat = Main.ValueFormat;

        Main.HarmonyBaseRequested += (_, color) =>
        {
            Harmony.SetBase(color.R, color.G, color.B);
            Tab = ShellTab.Harmony;
        };
        // Değer biçimi tek seçimdir: iki ekranda aynıdır.
        Main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ValueFormat)) Harmony.ValueFormat = Main.ValueFormat; };
        Harmony.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ValueFormat)) Main.ValueFormat = Harmony.ValueFormat; };
    }

    private static string ValueFormat => nameof(PaletteScreenViewModel.ValueFormat);

    public MainViewModel Main { get; }
    public HarmonyViewModel Harmony { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsTab))]
    [NotifyPropertyChangedFor(nameof(IsImageTab))]
    [NotifyPropertyChangedFor(nameof(IsHarmonyTab))]
    [NotifyPropertyChangedFor(nameof(ActiveScreen))]
    private ShellTab _tab = ShellTab.Image;

    public bool IsSettingsTab { get => Tab == ShellTab.Settings; set { if (value) Tab = ShellTab.Settings; } }
    public bool IsImageTab { get => Tab == ShellTab.Image; set { if (value) Tab = ShellTab.Image; } }
    public bool IsHarmonyTab { get => Tab == ShellTab.Harmony; set { if (value) Tab = ShellTab.Harmony; } }

    /// <summary>Alt sayfa ve bildirimin bağlandığı palet ekranı; Ayarlar sekmesinde yoktur.</summary>
    public PaletteScreenViewModel? ActiveScreen => Tab switch
    {
        ShellTab.Image => Main,
        ShellTab.Harmony => Harmony,
        _ => null,
    };

    partial void OnTabChanged(ShellTab value)
    {
        // Perde gezinme çubuğunu da örter ama sekme klavyeyle/kısayolla da değişebilir: açık alt sayfa kalmasın.
        Main.IsExportOpen = false;
        Harmony.IsExportOpen = false;
    }

    /// <summary>
    /// Geri tuşu / Esc: önce açık alt sayfayı, sonra orta sekme dışındaysa orta sekmeye dönmeyi dener.
    /// Bir şey yaptıysa true döner; false ise olay işlenmemiş bırakılır (uygulamadan çıkılabilir).
    /// </summary>
    public bool HandleBack()
    {
        if (ActiveScreen is { IsExportOpen: true } screen)
        {
            screen.IsExportOpen = false;
            return true;
        }
        if (Tab != ShellTab.Image)
        {
            Tab = ShellTab.Image;
            return true;
        }
        return false;
    }
}
