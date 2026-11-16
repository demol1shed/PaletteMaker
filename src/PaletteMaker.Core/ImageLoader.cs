using SkiaSharp;

namespace PaletteMaker.Core;

/// <summary>Çözülmüş, küçültülmüş görsel: düz RGBA (premultiply edilmemiş) tampon.</summary>
public sealed record LoadedImage(byte[] Rgba, int Width, int Height);

public sealed class ImageLoadException(string message) : Exception(message);

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

    public static LoadedImage Load(string path)
    {
        if (!IsSupportedExtension(path))
            throw new ImageLoadException("Desteklenmeyen dosya türü. Yalnızca PNG, JPG, JPEG ve WebP açılabilir.");
        byte[] data;
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
                throw new ImageLoadException("Dosya çok büyük (en fazla 100 MB).");
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImageLoadException("Dosya okunamadı.");
        }
        return Decode(data);
    }

    /// <summary>Uzantıya değil içeriğe bakar: yalnızca PNG/JPEG/WebP kabul edilir.</summary>
    public static LoadedImage Decode(byte[] data)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(data));
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            throw new ImageLoadException("Dosya geçerli bir PNG, JPEG veya WebP görseli değil.");

        int w = codec.Info.Width, h = codec.Info.Height;
        if (w <= 0 || h <= 0 || (long)w * h > MaxPixels)
            throw new ImageLoadException("Görselin çözünürlüğü çok yüksek (en fazla ~100 megapiksel).");

        // Codec destekliyorsa (örn. JPEG) küçültülmüş boyutta çöz: bellek ve süre kazancı.
        var target = Math.Max(w, h) > MaxEdge ? codec.GetScaledDimensions((float)MaxEdge / Math.Max(w, h)) : new SKSizeI(w, h);
        if (target.Width <= 0 || target.Height <= 0) target = new SKSizeI(w, h);
        var info = new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var full = SKBitmap.Decode(codec, info);
        if (full == null) throw new ImageLoadException("Görsel çözülemedi.");
        w = full.Width; h = full.Height;

        SKBitmap bmp = full;
        SKBitmap? resized = null;
        if (Math.Max(w, h) > MaxEdge)
        {
            double scale = (double)MaxEdge / Math.Max(w, h);
            int nw = Math.Max(1, (int)Math.Round(w * scale)), nh = Math.Max(1, (int)Math.Round(h * scale));
            resized = full.Resize(new SKImageInfo(nw, nh, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized == null) throw new ImageLoadException("Görsel küçültülemedi.");
            bmp = resized;
        }
        try { return new LoadedImage(bmp.Bytes, bmp.Width, bmp.Height); }
        finally { resized?.Dispose(); }
    }
}
