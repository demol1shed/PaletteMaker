using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PaletteMaker.Core;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainViewModel Vm => (MainViewModel)DataContext!;

    private async void OnDropZonePressed(object? sender, PointerPressedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Görsel seç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Görseller") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.PNG", "*.JPG", "*.JPEG", "*.WEBP"] }],
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path != null) await Vm.LoadAsync(path);
    }

    private static string? FirstLocalPath(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = FirstLocalPath(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var path = FirstLocalPath(e);
        e.Handled = true;
        if (path != null) await Vm.LoadAsync(path);
    }

    private async Task CopyAsync(string text, string toast)
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
        Vm.ShowToast(toast);
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

    private async void OnCopyAll(object? sender, RoutedEventArgs e) =>
        await CopyAsync(Vm.AllHexText(), "Tüm hex kodları kopyalandı");
}
