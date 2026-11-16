using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
