using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PaletteMaker.Localization;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Views;

/// <summary>Renk uyumu ekranı (alt gezinmedeki sağ sekme). Düzen kırılma noktaları <see cref="MainView"/> ile aynıdır.</summary>
public partial class HarmonyView : UserControl
{
    public HarmonyView()
    {
        InitializeComponent();
        Wheel.MarkerDragged += (index, hue, saturation) => Vm?.DragMarker(index, hue, saturation);
        // Enter hemen uygular; odak kaybında yalnızca metin değiştiyse (geçersizse hata gösterilir).
        HexBox.AddHandler(KeyDownEvent, OnHexKeyDown, RoutingStrategies.Tunnel);
        HexBox.LostFocus += (_, _) =>
        {
            if (Vm is { } vm && vm.HexInput != vm.BaseHex) vm.ApplyHexInput();
        };
    }

    public bool IsNarrow => HarmonyRoot.Classes.Contains("narrow");

    private HarmonyViewModel? Vm => DataContext as HarmonyViewModel;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var size = e.NewSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        HarmonyRoot.Classes.Set("narrow", size.Width < MainView.NarrowBreakpoint);
        HarmonyRoot.Classes.Set("short", size.Height < MainView.ShortBreakpoint);
    }

    private void OnHexKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return) Vm?.ApplyHexInput();
    }

    private Task CopyAsync(string text, string toast) => ClipboardHelper.CopyAsync(this, Vm, text, toast);

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

    private async void OnValueTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true; // karta (hex kopyalama) yayılmasın
        if ((sender as Control)?.DataContext is SwatchViewModel s)
            await CopyAsync(s.ValueText, Loc.Get("ToastCopied"));
    }

    private async void OnCopyAll(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) await CopyAsync(vm.AllHexText(), Loc.Get("ToastAllCopied"));
    }

    private void OnShare(object? sender, RoutedEventArgs e)
    {
        if (Vm is { HasPalette: true } vm) vm.IsExportOpen = true;
    }
}
