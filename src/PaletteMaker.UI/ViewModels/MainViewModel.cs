using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Monetization;
using PaletteMaker.Core;
using PaletteMaker.Imaging;
using PaletteMaker.Localization;

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
    /// <summary>Reklam/satın alma. Masaüstünde ve GitHub derlemelerinde hep boş (<see cref="NullMonetization"/>).</summary>
    [ObservableProperty] private IMonetization _monetization = NullMonetization.Instance;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ShowDropHint))]
    private string? _errorMessage;
    [ObservableProperty] private string? _toastMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxColorsText))]
    private double _maxColors = PaletteOptions.DefaultMaxColors;

    [ObservableProperty] private double _mergeStrength = PaletteOptions.DefaultMergeStrength;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormatIndex))]
    private ValueFormat _valueFormat = ValueFormat.Rgb;

    /// <summary>Başlıktaki açılır menünün seçili sırası (<see cref="ValueFormat"/> ile aynı sıra).</summary>
    public int FormatIndex
    {
        get => (int)ValueFormat;
        set { if (Enum.IsDefined((ValueFormat)value)) ValueFormat = (ValueFormat)value; }
    }

    /// <summary>
    /// Platformun HEIC/AVIF gibi ek biçimleri PNG'ye çeviren çözücüsü (yalnızca Android atar). Boşsa yalnızca PNG/JPEG/WebP açılır.
    /// </summary>
    public IImageTranscoder? Transcoder { get; set; }

    /// <summary>Yeni bir görselden palet başarıyla çıkınca bir kez tetiklenir (Play değerlendirme sayacı bunu kullanır).</summary>
    public event EventHandler? PaletteLoaded;

    /// <summary>"Paylaş / dışa aktar" alt sayfası açık mı.</summary>
    [ObservableProperty] private bool _isExportOpen;

    /// <summary>Şu anki palet (kartlarla aynı sırada: paya göre azalan).</summary>
    public IReadOnlyList<PaletteColor> Palette { get; private set; } = [];

    public string MaxColorsText => ((int)Math.Round(MaxColors)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool HasImage => _image != null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    /// <summary>"Görsel seç / sürükle" ipucu: görsel yokken ve hata kartı açık değilken.</summary>
    public bool ShowDropHint => !HasImage && !HasError;
    public bool HasPalette => Swatches.Count > 0;

    partial void OnMaxColorsChanged(double value) => _ = RecomputeAsync();
    partial void OnMergeStrengthChanged(double value) => _ = RecomputeAsync();

    partial void OnValueFormatChanged(ValueFormat value)
    {
        foreach (var s in Swatches) s.Format = value;
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
                var transcoder = Transcoder;
                ImageLoader.EnsureSupportedName(name, requireExtension, transcoder?.Extensions);
                byte[] data;
                try
                {
                    await using var stream = await open();
                    data = ImageLoader.ReadBounded(stream);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new ImageLoadException(ImageLoadError.Unreadable, "The file could not be read.");
                }
                LoadedImage decoded;
                try
                {
                    decoded = ImageLoader.Decode(data);
                }
                catch (ImageLoadException ex) when (ex.Kind == ImageLoadError.NotAnImage && transcoder != null)
                {
                    // PNG/JPEG/WebP değil: platform çözücüsüyle PNG'ye çevir ve aynı doğrulamadan geçir.
                    var png = transcoder.TryConvertToPng(data);
                    if (png == null) throw;
                    data = png;
                    decoded = ImageLoader.Decode(data);
                }
                Bitmap? preview = null;
                try
                {
                    // Önizleme için tam çözünürlüğü belleğe açma; 800 px genişlikte çöz.
                    // EXIF yönü (telefon fotoğrafları) varsa Skia ile döndürülmüş PNG; yoksa hızlı yol.
                    var oriented = ImageLoader.RenderOrientedPreviewPng(data, 800);
                    using var ms = new MemoryStream(oriented ?? data, writable: false);
                    preview = oriented != null ? new Bitmap(ms) : Bitmap.DecodeToWidth(ms, 800);
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
            OnPropertyChanged(nameof(ShowDropHint));
            await RecomputeAsync();
            // Yeni bir görsel için palet gerçekten çıktı (kaydırıcıyla yeniden hesaplamalar ve hatalar sayılmaz).
            if (version == _loadVersion && Palette.Count > 0) PaletteLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (ImageLoadException ex)
        {
            if (version == _loadVersion) ErrorMessage = ErrorText(ex.Kind);
        }
        catch (Exception)
        {
            if (version == _loadVersion) ErrorMessage = Loc.Get("ErrUnexpected");
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
            Palette = palette;
            foreach (var c in palette) Swatches.Add(new SwatchViewModel(c, ValueFormat));
            OnPropertyChanged(nameof(HasPalette));
            ErrorMessage = palette.Count == 0 ? Loc.Get("ErrNoColors") : null;
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

    /// <summary>Core'un hata türünü yerelleştirilmiş kullanıcı metnine çevirir.</summary>
    public static string ErrorText(ImageLoadError kind) => Loc.Get(kind switch
    {
        ImageLoadError.UnsupportedType => "ErrUnsupportedType",
        ImageLoadError.TooLarge => "ErrTooLarge",
        ImageLoadError.ResolutionTooHigh => "ErrResolutionTooHigh",
        ImageLoadError.NotAnImage => "ErrNotAnImage",
        ImageLoadError.Unreadable => "ErrUnreadable",
        ImageLoadError.DecodeFailed => "ErrDecodeFailed",
        ImageLoadError.ResizeFailed => "ErrResizeFailed",
        _ => "ErrUnexpected",
    });

    public string AllHexText() => PaletteExport.HexList(Palette);

    /// <summary>Verilen türde palet metni (panoya kopyalama / dosyaya yazma).</summary>
    public string ExportText(ExportKind kind) => kind switch
    {
        ExportKind.Css => PaletteExport.CssVariables(Palette),
        ExportKind.Json => PaletteExport.Json(Palette),
        ExportKind.AndroidColorsXml => PaletteExport.AndroidColorsXml(Palette),
        ExportKind.GimpPalette => PaletteExport.GimpPalette(Palette, BaseFileName),
        _ => PaletteExport.HexList(Palette),
    };

    /// <summary>Görsel adından uzantısız, dosya sistemi için güvenli bir ad (yoksa "palette").</summary>
    public string BaseFileName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FileName) ? "" : Path.GetFileNameWithoutExtension(FileName);
            var safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ' ? ch : '_').ToArray()).Trim();
            return safe.Length == 0 ? "palette" : safe;
        }
    }

    /// <summary>Örn. "gun-batimi-palette.gpl".</summary>
    public string ExportFileName(string extension) => $"{BaseFileName}-palette.{extension}";

    /// <summary>Paletti paylaşılabilir PNG kart olarak çizer (arka planda çağrılması önerilir).</summary>
    public byte[] RenderCardPng() => PaletteCardRenderer.RenderPng(Palette);
}
