using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Core;

namespace PaletteMaker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private LoadedImage? _image;
    private CancellationTokenSource? _computeCts;
    private CancellationTokenSource? _toastCts;
    private int _loadVersion;

    public ObservableCollection<SwatchViewModel> Swatches { get; } = new();

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _toastMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxColorsText))]
    private double _maxColors = PaletteOptions.DefaultMaxColors;

    [ObservableProperty] private double _mergeStrength = PaletteOptions.DefaultMergeStrength;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRgb))]
    private bool _useCmyk;

    public bool IsRgb
    {
        get => !UseCmyk;
        set => UseCmyk = !value;
    }

    public string MaxColorsText => ((int)Math.Round(MaxColors)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool HasImage => _image != null;
    public bool HasPalette => Swatches.Count > 0;

    partial void OnMaxColorsChanged(double value) => _ = RecomputeAsync();
    partial void OnMergeStrengthChanged(double value) => _ = RecomputeAsync();

    partial void OnUseCmykChanged(bool value)
    {
        foreach (var s in Swatches) s.UseCmyk = value;
    }

    /// <summary>Yerel dosya yolundan yükler (komut satırı, masaüstü seçici/sürükle-bırak). Uzantı zorunludur.</summary>
    public Task LoadAsync(string path) =>
        LoadAsync(Path.GetFileName(path), () => Task.FromResult<Stream>(File.OpenRead(path)), requireExtension: true);

    /// <summary>
    /// Akıştan yükler (Android content:// adresleri, paylaşım). Baytlar bir kez okunur; palet ve önizleme
    /// aynı baytlardan arka planda üretilir.
    /// </summary>
    public async Task LoadAsync(string? name, Func<Task<Stream>> open, bool requireExtension = false)
    {
        int version = ++_loadVersion;
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var (image, bmp) = await Task.Run(async () =>
            {
                ImageLoader.EnsureSupportedName(name, requireExtension);
                byte[] data;
                try
                {
                    await using var stream = await open();
                    data = ImageLoader.ReadBounded(stream);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new ImageLoadException("Dosya okunamadı.");
                }
                var decoded = ImageLoader.Decode(data);
                Bitmap? preview = null;
                try
                {
                    // Önizleme için tam çözünürlüğü belleğe açma; 800 px genişlikte çöz.
                    using var ms = new MemoryStream(data, writable: false);
                    preview = Bitmap.DecodeToWidth(ms, 800);
                }
                catch { /* önizleme olmasa da palet çıkar */ }
                return (decoded, preview);
            });
            if (version != _loadVersion)
            {
                bmp?.Dispose();
                return;
            }
            _image = image;
            var old = Preview;
            Preview = bmp;
            old?.Dispose(); // yenisi atandıktan sonra bırak; ekran bırakılmış bitmap'i çizmesin
            FileName = name;
            OnPropertyChanged(nameof(HasImage));
            await RecomputeAsync();
        }
        catch (ImageLoadException ex)
        {
            if (version == _loadVersion) ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            if (version == _loadVersion) ErrorMessage = "Görsel açılırken beklenmeyen bir hata oluştu.";
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
        }
    }

    private async Task RecomputeAsync()
    {
        var image = _image;
        if (image == null) return;

        _computeCts?.Cancel();
        var cts = _computeCts = new CancellationTokenSource();
        var options = new PaletteOptions { MaxColors = (int)Math.Round(MaxColors), MergeStrength = MergeStrength };
        try
        {
            IsBusy = true;
            await Task.Delay(120, cts.Token); // kaydırıcı sürüklenirken art arda hesaplamayı önle
            var palette = await Task.Run(() => PaletteExtractor.Extract(image.Rgba, options, cts.Token), cts.Token);
            if (cts.IsCancellationRequested) return;

            Swatches.Clear();
            foreach (var c in palette) Swatches.Add(new SwatchViewModel(c, UseCmyk));
            OnPropertyChanged(nameof(HasPalette));
            ErrorMessage = palette.Count == 0 ? "Bu görselde okunabilir renk bulunamadı (tamamen saydam olabilir)." : null;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_computeCts == cts) IsBusy = false;
        }
    }

    public async void ShowToast(string message)
    {
        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();
        ToastMessage = message;
        try { await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { return; }
        if (_toastCts == cts) ToastMessage = null;
    }

    public string AllHexText() => string.Join("\n", Swatches.Select(s => s.Hex));
}
