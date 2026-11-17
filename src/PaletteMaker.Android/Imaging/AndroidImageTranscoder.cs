using Android.Graphics;
using PaletteMaker.Core;
using PaletteMaker.Imaging;

namespace PaletteMaker.Droid.Imaging;

/// <summary>
/// Android'in sistem görsel çözücüsüyle (<see cref="BitmapFactory"/>) Skia'nın açamadığı biçimleri PNG'ye çevirir:
/// HEIC/HEIF (API 28+), AVIF (API 31+), ayrıca GIF ve BMP. Daha eski sürümlerde çözücü null döner ve kullanıcı
/// "geçerli görsel değil" hatasını görür.
/// <para>Bellek koruması: önce yalnızca boyutlara bakılır (<see cref="ImageLoader.MaxPixels"/> aşılırsa reddedilir),
/// sonra uzun kenar ~1024 px'e inene kadar 2'nin kuvvetiyle küçülterek çözülür. Palet zaten 256 px'te çıkarılır.</para>
/// </summary>
public sealed class AndroidImageTranscoder : IImageTranscoder
{
    private const int TargetEdge = 1024;

    public IReadOnlyCollection<string> Extensions { get; } = [".heic", ".heif", ".avif", ".gif", ".bmp"];

    public IReadOnlyCollection<string> MimeTypes { get; } =
        ["image/heic", "image/heif", "image/avif", "image/gif", "image/bmp", "image/x-ms-bmp"];

    public byte[]? TryConvertToPng(byte[] data)
    {
        try
        {
            using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeByteArray(data, 0, data.Length, bounds);
            int w = bounds.OutWidth, h = bounds.OutHeight;
            if (w <= 0 || h <= 0 || (long)w * h > ImageLoader.MaxPixels) return null;

            int sample = 1;
            while (Math.Max(w, h) / sample > TargetEdge) sample *= 2;

            using var options = new BitmapFactory.Options { InSampleSize = sample, InPreferredConfig = Bitmap.Config.Argb8888 };
            using var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length, options);
            if (bitmap == null) return null;

            using var output = new MemoryStream();
            return bitmap.Compress(Bitmap.CompressFormat.Png!, 100, output) ? output.ToArray() : null;
        }
        catch (Exception)
        {
            return null; // çözücü hataları (bozuk dosya, bellek) kullanıcıya genel hata olarak yansır
        }
    }
}
