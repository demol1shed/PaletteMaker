using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PaletteMaker.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>Düğmenin <c>Tag</c>'indeki https adresini sistem tarayıcısında açar (başarısızsa sessizce vazgeçer).</summary>
    private async void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url } || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher == null) return;
        try { await launcher.LaunchUriAsync(uri); }
        catch (Exception) { /* tarayıcı yok / engellendi: ayarlar sayfasını bozmasın */ }
    }
}
