using SkiaSharp;

namespace PaletteMaker.Core;

/// <summary>Çözülmüş, küçültülmüş görsel: düz RGBA (premultiply edilmemiş) tampon.</summary>
public sealed record LoadedImage(byte[] Rgba, int Width, int Height);

/// <summary>Yüklemenin neden başarısız olduğu. Arayüz, kullanıcıya göstereceği metni bu türe göre (yerelleştirerek) seçer.</summary>
public enum ImageLoadError
{
    UnsupportedType,
    TooLarge,
    ResolutionTooHigh,
    NotAnImage,
    Unreadable,
    DecodeFailed,
    ResizeFailed,
}

/// <summary>
/// <see cref="Kind"/> programın karar vermesi ve arayüz yerelleştirmesi içindir; <see cref="Exception.Message"/> yalnızca
/// günlük/hata ayıklama için nötr İngilizcedir (Core kütüphanesi yerelleştirme yapmaz).
/// </summary>
public sealed class ImageLoadException(ImageLoadError kind, string message) : Exception(message)
{
    public ImageLoadError Kind { get; } = kind;
}

public static class ImageLoader
{
    public const int MaxEdge = 256;

    /// <summary>Bellek tüketimini sınırlamak için kabul edilen en büyük dosya boyutu (bayt).</summary>
    public const long MaxFileBytes = 100L * 1024 * 1024;

    /// <summary>Kabul edilen en büyük piksel sayısı (≈100 MP). Başlık yalnızca birkaç bayt olsa da çözülmüş görsel GB'larca yer tutabilir.</summary>
    public const long MaxPixels = 100_000_000;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public static bool IsSupportedExtension(string path) =>
        AllowedExtensions.Contains(Path.GetExtension(path));

    private static ImageLoadException UnsupportedType() =>
        new(ImageLoadError.UnsupportedType, "Unsupported file type. Only PNG, JPG, JPEG and WebP can be opened.");

    private static ImageLoadException TooLarge() =>
        new(ImageLoadError.TooLarge, "The file is too large (100 MB at most).");

    private static ImageLoadException Unreadable() =>
        new(ImageLoadError.Unreadable, "The file could not be read.");

    /// <summary>
    /// Akıştan gelen (seçici, sürükle-bırak, Android paylaşımı) dosyanın adını denetler: uzantı varsa ve
    /// desteklenmiyorsa reddeder. Uzantısız adlar geçer, çünkü Android içerik sağlayıcıları sık sık uzantısız
    /// ad verir; asıl denetim <see cref="Decode"/> içindeki içerik kontrolüdür.
    /// <paramref name="requireExtension"/> verilirse (yerel dosya yolu) uzantısız adlar da reddedilir.
    /// <paramref name="extraExtensions"/>, platformun ayrıca çevirebildiği uzantılardır (Android: ".heic", ".avif" …);
    /// bunlar da kabul edilir, içerik yine <see cref="Decode"/> ile doğrulanır.
    /// </summary>
    public static void EnsureSupportedName(string? fileName, bool requireExtension = false, IReadOnlyCollection<string>? extraExtensions = null)
    {
        var ext = string.IsNullOrEmpty(fileName) ? "" : Path.GetExtension(fileName);
        if (ext.Length == 0)
        {
            if (requireExtension) throw UnsupportedType();
            return;
        }
        if (AllowedExtensions.Contains(ext)) return;
        if (extraExtensions != null && extraExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return;
        throw UnsupportedType();
    }

    /// <summary>Akışı en fazla <paramref name="maxBytes"/> bayt okur; aşılırsa reddeder. Aranamayan akışlarda da çalışır.</summary>
    public static byte[] ReadBounded(Stream stream, long maxBytes = MaxFileBytes)
    {
        try
        {
            if (stream.CanSeek && stream.Length - stream.Position > maxBytes)
                throw TooLarge();

            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            long total = 0;
            int n;
            while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += n;
                if (total > maxBytes) throw TooLarge();
                ms.Write(buffer, 0, n);
            }
            return ms.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException)
        {
            throw Unreadable();
        }
    }

    public static LoadedImage Load(string path)
    {
        if (!IsSupportedExtension(path))
            throw UnsupportedType();
        byte[] data;
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
                throw TooLarge();
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Unreadable();
        }
        return Decode(data);
    }

    /// <summary>Uzantıya değil içeriğe bakar: yalnızca PNG/JPEG/WebP kabul edilir.</summary>
    public static LoadedImage Decode(byte[] data)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(data));
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            throw new ImageLoadException(ImageLoadError.NotAnImage, "The file is not a valid PNG, JPEG or WebP image.");

        int w = codec.Info.Width, h = codec.Info.Height;
        if (w <= 0 || h <= 0 || (long)w * h > MaxPixels)
            throw new ImageLoadException(ImageLoadError.ResolutionTooHigh, "The image resolution is too high (about 100 megapixels at most).");

        // Codec destekliyorsa (örn. JPEG) küçültülmüş boyutta çöz: bellek ve süre kazancı.
        var target = Math.Max(w, h) > MaxEdge ? codec.GetScaledDimensions((float)MaxEdge / Math.Max(w, h)) : new SKSizeI(w, h);
        if (target.Width <= 0 || target.Height <= 0) target = new SKSizeI(w, h);
        var info = new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var full = SKBitmap.Decode(codec, info);
        if (full == null) throw new ImageLoadException(ImageLoadError.DecodeFailed, "The image could not be decoded.");
        w = full.Width; h = full.Height;

        SKBitmap bmp = full;
        SKBitmap? resized = null;
        if (Math.Max(w, h) > MaxEdge)
        {
            double scale = (double)MaxEdge / Math.Max(w, h);
            int nw = Math.Max(1, (int)Math.Round(w * scale)), nh = Math.Max(1, (int)Math.Round(h * scale));
            resized = full.Resize(new SKImageInfo(nw, nh, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized == null) throw new ImageLoadException(ImageLoadError.ResizeFailed, "The image could not be resized.");
            bmp = resized;
        }
        try { return new LoadedImage(bmp.Bytes, bmp.Width, bmp.Height); }
        finally { resized?.Dispose(); }
    }
}
