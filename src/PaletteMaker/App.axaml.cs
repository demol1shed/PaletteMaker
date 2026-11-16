using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PaletteMaker.ViewModels;
using PaletteMaker.Views;

namespace PaletteMaker;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            // Komut satırından verilen dosya (örn. "birlikte aç") hemen yüklenir.
            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
                desktop.MainWindow.Opened += async (_, _) => await vm.LoadAsync(args[0]);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
