using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PaletteMaker.Core;
using PaletteMaker.Settings;
using PaletteMaker.Sharing;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;

namespace PaletteMaker;

public partial class App : Application
{
    /// <summary>
    /// Tek görünüm modeli. Android etkinliği yeniden oluşturabildiği için görünümden bağımsız tutulur;
    /// paylaşımla gelen görsel de (Android MainActivity) buraya yüklenir.
    /// </summary>
    public MainViewModel MainVm { get; }

    /// <summary>
    /// Ayar dosyasının yolu; başlatıcı (masaüstü <c>Program</c>, <c>AndroidApp</c>) <c>App</c> oluşmadan önce atar.
    /// Boşsa (testler) ayarlar yalnızca bellekte tutulur, kullanıcı dosyasına dokunulmaz.
    /// </summary>
    public static string? SettingsFilePath { get; set; }

    /// <summary>Kalıcı tercihler (tema, dil, palet varsayılanları, dışa aktarma).</summary>
    public SettingsService Settings { get; }

    /// <summary>Kabuk (seçili sekme + üç ekran). <see cref="MainVm"/> onun "Görsel" ekranıdır; sekme durumu etkinlik yeniden oluşunca korunur.</summary>
    public ShellViewModel ShellVm { get; }

    public App()
    {
        Settings = new SettingsService(SettingsFilePath);
        ApplyLanguage(Settings.Current.Language);
        MainVm = new MainViewModel(Settings.Current);
        ShellVm = new ShellViewModel(MainVm, settings: new SettingsViewModel(Settings));
    }

    /// <summary>Arayüz metinleri açılışta bir kez okunur: "Sistem" dışındaki seçim kültürü baştan ayarlar.</summary>
    private static void ApplyLanguage(LanguageChoice choice)
    {
        if (choice == LanguageChoice.System) return;
        var culture = new System.Globalization.CultureInfo(choice == LanguageChoice.Turkish ? "tr" : "en");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
        System.Globalization.CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Android Play sürümünün reklam kontrolünü üreten fabrika (<c>MainActivity</c> görünüm oluşmadan önce atar).
    /// Boşsa sayfada reklam alanı hiç oluşturulmaz.
    /// </summary>
    public static Func<Control>? AdControlFactory { get; set; }

    /// <summary>Platformun paylaşım menüsü (Android). Boşsa PNG "Farklı kaydet" iletişim kutusuyla kaydedilir.</summary>
    public static IShareService? ShareService { get; set; }

    /// <summary>Android 13+ panoya yazınca kendi "Kopyalandı" onayını gösterir; true iken uygulama kendi toast'ını göstermez (çift mesaj olmasın).</summary>
    public static bool SystemShowsClipboardConfirmation { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        SettingsViewModel.ApplyTheme(Settings.Current.Theme);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = ShellVm };
            // Komut satırından verilen dosya (örn. "birlikte aç") hemen yüklenir.
            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
                desktop.MainWindow.Opened += async (_, _) => await MainVm.LoadAsync(args[0]);
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Android: her etkinlik için yeni görünüm, aynı görünüm modeli.
            activity.MainViewFactory = () => new ShellView { DataContext = ShellVm };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new ShellView { DataContext = ShellVm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
