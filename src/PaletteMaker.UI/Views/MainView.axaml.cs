using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PaletteMaker.Localization;
using PaletteMaker.Monetization;
using PaletteMaker.Sharing;
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

    /// <summary>Seçici filtresi; platform çözücüsü varsa (Android) onun ek uzantı ve MIME türleri de eklenir.</summary>
    private FilePickerFileType BuildImageFileType()
    {
        var extra = Vm?.Transcoder;
        return new FilePickerFileType(Loc.Get("PickerFilter"))
        {
            Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.PNG", "*.JPG", "*.JPEG", "*.WEBP",
                ..(extra?.Extensions.Select(e => "*" + e) ?? [])],
            // Android sistem seçicisi desenlere değil MIME türüne bakar.
            MimeTypes = ["image/png", "image/jpeg", "image/webp", ..(extra?.MimeTypes ?? [])],
        };
    }

    private async void OnPickTapped(object? sender, TappedEventArgs e) => await PickImageAsync();

    private async Task PickImageAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.Get("PickerTitle"),
            AllowMultiple = false,
            FileTypeFilter = [BuildImageFileType()],
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
            await CopyAsync(s.Hex, Loc.Format("ToastHexCopied", s.Hex));
    }

    /// <summary>Klavye / ekran okuyucu: odaklı karta Enter ya da Boşluk, dokunmayla aynı şekilde HEX kopyalar.</summary>
    private async void OnSwatchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space) || e.Source != sender) return;
        e.Handled = true;
        if ((sender as Control)?.DataContext is SwatchViewModel s)
            await CopyAsync(s.Hex, Loc.Format("ToastHexCopied", s.Hex));
    }

    /// <summary>Klavye / ekran okuyucu: odaklı görsel alanına Enter ya da Boşluk dosya seçiciyi açar.</summary>
    private async void OnDropZoneKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        e.Handled = true;
        await PickImageAsync();
    }

    private async void OnValueTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true; // karta (hex kopyalama) yayılmasın
        if ((sender as Control)?.DataContext is SwatchViewModel s)
            await CopyAsync(s.ValueText, Loc.Get("ToastCopied"));
    }

    // ---- Paylaş ve dışa aktar (alt sayfa) ---------------------------------------------------

    private static readonly FilePickerFileType GimpPaletteType = new("GIMP / Resprite palette")
    {
        Patterns = ["*.gpl"],
        // "text/plain" olursa Android dosya adına ".txt" ekler; genel ikili tür adı olduğu gibi bırakır.
        MimeTypes = ["application/octet-stream"],
    };

    private static readonly FilePickerFileType PngType = new("PNG")
    {
        Patterns = ["*.png"],
        MimeTypes = ["image/png"],
    };

    private void OnShare(object? sender, RoutedEventArgs e)
    {
        if (Vm is { HasPalette: true } vm) vm.IsExportOpen = true;
    }

    private void OnScrimTapped(object? sender, RoutedEventArgs e) => CloseExportSheet();

    private void CloseExportSheet()
    {
        if (Vm is { } vm) vm.IsExportOpen = false;
    }

    private async void OnExportCopy(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || (sender as Control)?.Tag is not string tag || !Enum.TryParse<ExportKind>(tag, out var kind)) return;
        string label = kind switch
        {
            ExportKind.Css => Loc.Get("ExportCss"),
            ExportKind.Json => Loc.Get("ExportJson"),
            ExportKind.AndroidColorsXml => Loc.Get("ExportAndroid"),
            _ => Loc.Get("ExportHexList"),
        };
        vm.IsExportOpen = false;
        await CopyAsync(vm.ExportText(kind), Loc.Format("ToastHexCopied", label));
    }

    /// <summary>Resprite/GIMP paleti: gerçek bir .gpl dosyası kaydeder (Resprite içe aktarmayı dosya seçiciyle yapar).</summary>
    private async void OnExportGpl(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.IsExportOpen = false;
        var bytes = new System.Text.UTF8Encoding(false).GetBytes(vm.ExportText(ExportKind.GimpPalette));
        await SaveFileAsync(bytes, vm.ExportFileName("gpl"), GimpPaletteType, "gpl");
    }

    private async void OnExportPng(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.IsExportOpen = false;
        try
        {
            var png = await Task.Run(vm.RenderCardPng);
            var name = vm.ExportFileName("png");
            if (App.ShareService is { } share) await share.SharePngAsync(png, name, Loc.Get("ExportShareTitle"));
            else await SaveFileAsync(png, name, PngType, "png");
        }
        catch (Exception)
        {
            vm.ShowToast(Loc.Get("ToastSaveFailed"));
        }
    }

    /// <summary>"Farklı kaydet" iletişim kutusu (Android'de sistem SAF penceresi, izin gerekmez). İptal sessizce geçilir.</summary>
    private async Task SaveFileAsync(byte[] data, string suggestedName, FilePickerFileType type, string extension)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        try
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = suggestedName,
                DefaultExtension = extension,
                FileTypeChoices = [type],
                ShowOverwritePrompt = true,
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0); // üzerine yazarken eski içerikten artık kalmasın
            await stream.WriteAsync(data);
            await stream.FlushAsync();
            Vm?.ShowToast(Loc.Get("ToastSaved"));
        }
        catch (Exception)
        {
            Vm?.ShowToast(Loc.Get("ToastSaveFailed"));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Vm is { IsExportOpen: true })
        {
            CloseExportSheet();
            e.Handled = true;
        }
    }

    private TopLevel? _backTopLevel;

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

    /// <summary>Android Geri tuşu: alt sayfa açıksa önce onu kapatır.</summary>
    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (Vm is { IsExportOpen: true })
        {
            CloseExportSheet();
            e.Handled = true;
        }
    }

    private async void OnCopyAll(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) await CopyAsync(vm.AllHexText(), Loc.Get("ToastAllCopied"));
    }
}
