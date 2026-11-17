using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
    public MainViewModel MainVm { get; } = new();

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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = MainVm };
            // Komut satırından verilen dosya (örn. "birlikte aç") hemen yüklenir.
            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
                desktop.MainWindow.Opened += async (_, _) => await MainVm.LoadAsync(args[0]);
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Android: her etkinlik için yeni görünüm, aynı görünüm modeli.
            activity.MainViewFactory = () => new MainView { DataContext = MainVm };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = MainVm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
