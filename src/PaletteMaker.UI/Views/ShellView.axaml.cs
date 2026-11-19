using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Views;

/// <summary>
/// Uygulamanın kabuğu: alt gezinme çubuğu + üç sekme (Ayarlar, Görsel, Uyum). Masaüstünde MainWindow içinde,
/// Android'de doğrudan etkinlikte gösterilir. Geri tuşu / Esc / Ctrl+1-3 burada yönetilir.
/// </summary>
public partial class ShellView : UserControl
{
    private TopLevel? _backTopLevel;

    public ShellView() => InitializeComponent();

    public ShellViewModel? Vm => DataContext as ShellViewModel;

    /// <summary>Sekme görünümleri (testler ve kabuk içi erişim için).</summary>
    public MainView ImageView => ImagePage;
    public HarmonyView HarmonyViewPage => HarmonyPage;
    public ExportSheet ExportSheetView => Sheet;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0) Sheet.SetNarrow(e.NewSize.Width < MainView.NarrowBreakpoint);
    }

    /// <summary>Ctrl+1/2/3 sekme değiştirir; Esc açık alt sayfayı kapatır.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Vm is not { } vm || e.Handled) return;
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            ShellTab? tab = e.Key switch
            {
                Key.D1 or Key.NumPad1 => ShellTab.Settings,
                Key.D2 or Key.NumPad2 => ShellTab.Image,
                Key.D3 or Key.NumPad3 => ShellTab.Harmony,
                _ => null,
            };
            if (tab is { } t)
            {
                vm.Tab = t;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape && vm.ActiveScreen is { IsExportOpen: true } screen)
        {
            screen.IsExportOpen = false;
            e.Handled = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _backTopLevel = TopLevel.GetTopLevel(this);
        if (_backTopLevel != null) _backTopLevel.BackRequested += OnBackRequested;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_backTopLevel != null) _backTopLevel.BackRequested -= OnBackRequested;
        _backTopLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Android Geri tuşu: açık alt sayfayı kapatır, değilse orta sekme dışındaysa ortaya döner, aksi halde işlenmez (çıkış).</summary>
    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (Vm?.HandleBack() == true) e.Handled = true;
    }
}
