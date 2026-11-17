namespace PaletteMaker.Imaging;

/// <summary>
/// PNG/JPEG/WebP dışındaki biçimleri (örn. telefonların HEIC/AVIF fotoğrafları) platformun kendi çözücüsüyle PNG'ye
/// çevirir. Yalnızca Android atar (<c>MainViewModel.Transcoder</c>); masaüstünde boştur ve davranış değişmez.
/// Çıktı yine de <c>ImageLoader.Decode</c>'un içerik doğrulamasından geçer, yani güvenlik kuralı aynıdır.
/// </summary>
public interface IImageTranscoder
{
    /// <summary>Çevirmeyi denemeye değer ek dosya uzantıları (nokta ile, küçük harf): ".heic", ".avif", …</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>Seçici ve paylaşım için ek MIME türleri: "image/heic", …</summary>
    IReadOnlyCollection<string> MimeTypes { get; }

    /// <summary>Çözüp PNG baytları döndürür; çözülemezse (ya da sınırları aşarsa) null.</summary>
    byte[]? TryConvertToPng(byte[] data);
}
