using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PaletteMaker.Monetization;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Views;

/// <summary>Uygulamanın tek ekranı. Masaüstünde MainWindow içinde, Android'de doğrudan etkinlikte gösterilir.</summary>
public partial class MainView : UserControl
{
    /// <summary>Bu genişliğin altında telefon düzeni ("narrow" sınıfı) kullanılır.</summary>
    public const double NarrowBreakpoint = 600;

    /// <summary>Bu yüksekliğin altında (yatay telefon) görsel alanı kısaltılır ("short" sınıfı).</summary>
    public const double ShortBreakpoint = 520;

    private const double WideMargin = 20, NarrowMargin = 16;   // MainView.axaml'daki Page kenar boşluklarıyla aynı
    private const double MaxContentWidth = 1100;                // Page.MaxWidth ile aynı
    private const double MinSwatchWidth = 156, SwatchSpacing = 12;
    private const int MinSwatchColumns = 2, MaxSwatchColumns = 8;

    public static readonly StyledProperty<int> SwatchColumnsProperty =
        AvaloniaProperty.Register<MainView, int>(nameof(SwatchColumns), 5);

    public MainView()
    {
        InitializeComponent();
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        DataContextChanged += (_, _) => AttachMonetization();
    }

    private MainViewModel? _attachedVm;
    private IMonetization? _attachedMonetization;

    /// <summary>Reklam alanını görünüm modelinin (ve onun IMonetization'ının) durumuna bağlar.</summary>
    private void AttachMonetization()
    {
        if (_attachedVm != null) _attachedVm.PropertyChanged -= OnVmPropertyChanged;
        if (_attachedMonetization != null) _attachedMonetization.PropertyChanged -= OnMonetizationChanged;
        _attachedVm = Vm;
        _attachedMonetization = Vm?.Monetization;
        if (_attachedVm != null) _attachedVm.PropertyChanged += OnVmPropertyChanged;
        if (_attachedMonetization != null) _attachedMonetization.PropertyChanged += OnMonetizationChanged;
        RefreshAd();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Monetization)) AttachMonetization();
    }

    private void OnMonetizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshAd();

    private void RefreshAd()
    {
        bool show = Vm?.Monetization.ShowAds == true && App.AdControlFactory != null;
        if (show && AdSlot.Content == null) AdSlot.Content = App.AdControlFactory!();
        else if (!show && AdSlot.Content != null) AdSlot.Content = null;
    }

    private async void OnRemoveAds(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) await vm.Monetization.BuyRemoveAdsAsync();
    }

    private void OnPrivacyOptions(object? sender, RoutedEventArgs e) => Vm?.Monetization.ShowPrivacyOptions();

    /// <summary>Palet ızgarasının sütun sayısı; genişlikten hesaplanır.</summary>
    public int SwatchColumns
    {
        get => GetValue(SwatchColumnsProperty);
        private set => SetValue(SwatchColumnsProperty, value);
    }

    public bool IsNarrow => Root.Classes.Contains("narrow");

    private MainViewModel? Vm => DataContext as MainViewModel;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var size = e.NewSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        Root.Classes.Set("narrow", size.Width < NarrowBreakpoint);
        Root.Classes.Set("short", size.Height < ShortBreakpoint);
        SwatchColumns = ColumnsFor(size.Width);
    }

    /// <summary>Verilen görünüm genişliğinde kaç palet sütunu sığar (kartlar en az ~156 px).</summary>
    public static int ColumnsFor(double viewWidth)
    {
        double margin = viewWidth < NarrowBreakpoint ? NarrowMargin : WideMargin;
        double content = Math.Min(viewWidth - 2 * margin, MaxContentWidth);
        int columns = (int)Math.Floor((content + SwatchSpacing) / (MinSwatchWidth + SwatchSpacing));
        return Math.Clamp(columns, MinSwatchColumns, MaxSwatchColumns);
    }

    private static readonly FilePickerFileType ImageFileType = new("Görseller")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.PNG", "*.JPG", "*.JPEG", "*.WEBP"],
        // Android sistem seçicisi desenlere değil MIME türüne bakar.
        MimeTypes = ["image/png", "image/jpeg", "image/webp"],
    };

    private async void OnPickTapped(object? sender, TappedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Görsel seç",
            AllowMultiple = false,
            FileTypeFilter = [ImageFileType],
        });
        if (files.Count > 0) await LoadAsync(files[0]);
    }

    /// <summary>Yerel yol varsa (masaüstü) onu, yoksa (Android content:// vb.) dosyanın akışını yükler.</summary>
    private async Task LoadAsync(IStorageFile file)
    {
        if (Vm is not { } vm) return;
        var path = file.TryGetLocalPath();
        if (path != null) await vm.LoadAsync(path);
        else await vm.LoadAsync(file.Name, () => file.OpenReadAsync());
    }

    private static IStorageFile? FirstFile(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault();

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = FirstFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var file = FirstFile(e);
        e.Handled = true;
        if (file != null) await LoadAsync(file);
    }

    private async Task CopyAsync(string text, string toast)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
        Vm?.ShowToast(toast);
    }

    private async void OnSwatchTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SwatchViewModel s)
            await CopyAsync(s.Hex, $"{s.Hex} kopyalandı");
    }

    private async void OnValueTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true; // karta (hex kopyalama) yayılmasın
        if ((sender as Control)?.DataContext is SwatchViewModel s)
            await CopyAsync(s.ValueText, "Kopyalandı");
    }

    private async void OnCopyAll(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) await CopyAsync(vm.AllHexText(), "Tüm hex kodları kopyalandı");
    }
}
