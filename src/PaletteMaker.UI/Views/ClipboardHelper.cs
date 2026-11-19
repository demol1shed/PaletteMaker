using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Views;

/// <summary>Panoya kopyalama ve bildirim; tüm ekranlar (Görsel, Uyum, dışa aktarma alt sayfası) ortak kullanır.</summary>
internal static class ClipboardHelper
{
    /// <summary>
    /// Metni panoya yazar. Android 13+ kendi "Kopyalandı" onayını gösterdiği için (<see cref="App.SystemShowsClipboardConfirmation"/>)
    /// uygulamanın kendi bildirimi yalnızca o zaman atlanır; çift mesaj olmaz.
    /// </summary>
    public static async Task CopyAsync(Visual owner, PaletteScreenViewModel? vm, string text, string toast)
    {
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
        if (!App.SystemShowsClipboardConfirmation) vm?.ShowToast(toast);
    }
}
