using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PaletteMaker.Localization;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Views;

/// <summary>"Paylaş ve dışa aktar" alt sayfası. DataContext: etkin <see cref="PaletteScreenViewModel"/> (boş olabilir).</summary>
public partial class ExportSheet : UserControl
{
    private static readonly FilePickerFileType GimpPaletteType = new("GIMP / Resprite palette")
    {
        Patterns = ["*.gpl"],
        // "text/plain" olursa Android dosya adına ".txt" ekler; genel ikili tür adı olduğu gibi bırakır.
        MimeTypes = ["application/octet-stream"],
    };

    private static readonly FilePickerFileType AseType = new("Adobe Swatch Exchange")
    {
        Patterns = ["*.ase"],
        MimeTypes = ["application/octet-stream"],
    };

    private static readonly FilePickerFileType AcoType = new("Photoshop swatches")
    {
        Patterns = ["*.aco"],
        MimeTypes = ["application/octet-stream"],
    };

    private static readonly FilePickerFileType PngType = new("PNG")
    {
        Patterns = ["*.png"],
        MimeTypes = ["image/png"],
    };

    public ExportSheet() => InitializeComponent();

    private PaletteScreenViewModel? Vm => DataContext as PaletteScreenViewModel;

    /// <summary>Kabuk, genişliğe göre dar düzeni (daha büyük dokunma hedefleri) buradan bildirir.</summary>
    public void SetNarrow(bool narrow) => ExportSheetHost.Classes.Set("narrow", narrow);

    private void OnScrimTapped(object? sender, RoutedEventArgs e)
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
        await ClipboardHelper.CopyAsync(this, vm, vm.ExportText(kind), Loc.Format("ToastHexCopied", label));
    }

    /// <summary>Resprite/GIMP paleti: gerçek bir .gpl dosyası kaydeder (Resprite içe aktarmayı dosya seçiciyle yapar).</summary>
    private async void OnExportGpl(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.IsExportOpen = false;
        var bytes = new System.Text.UTF8Encoding(false).GetBytes(vm.ExportText(ExportKind.GimpPalette));
        await SaveFileAsync(vm, bytes, vm.ExportFileName("gpl"), GimpPaletteType, "gpl");
    }

    /// <summary>Adobe Swatch Exchange (.ase): Illustrator, InDesign, Photoshop.</summary>
    private async void OnExportAse(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.IsExportOpen = false;
        await SaveFileAsync(vm, vm.ExportAse(), vm.ExportFileName("ase"), AseType, "ase");
    }

    /// <summary>Photoshop renk örnekleri (.aco).</summary>
    private async void OnExportAco(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        vm.IsExportOpen = false;
        await SaveFileAsync(vm, vm.ExportAco(), vm.ExportFileName("aco"), AcoType, "aco");
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
            else await SaveFileAsync(vm, png, name, PngType, "png");
        }
        catch (Exception)
        {
            vm.ShowToast(Loc.Get("ToastSaveFailed"));
        }
    }

    /// <summary>"Farklı kaydet" iletişim kutusu (Android'de sistem SAF penceresi, izin gerekmez). İptal sessizce geçilir.</summary>
    private async Task SaveFileAsync(PaletteScreenViewModel vm, byte[] data, string suggestedName, FilePickerFileType type, string extension)
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
            vm.ShowToast(Loc.Get("ToastSaved"));
        }
        catch (Exception)
        {
            vm.ShowToast(Loc.Get("ToastSaveFailed"));
        }
    }
}
