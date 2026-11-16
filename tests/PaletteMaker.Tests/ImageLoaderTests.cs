using PaletteMaker.Core;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.Tests;

public class ImageLoaderTests
{
    private static byte[] TwoColor(int w, int h) => TestImages.Rgba(w, h, (x, _) =>
        x < w / 2 ? ((byte)250, (byte)20, (byte)20, (byte)255) : ((byte)20, (byte)20, (byte)250, (byte)255));

    private static void AssertNear(string hex, byte r, byte g, byte b, int tol)
    {
        int R = Convert.ToInt32(hex[1..3], 16), G = Convert.ToInt32(hex[3..5], 16), B = Convert.ToInt32(hex[5..7], 16);
        Assert.True(Math.Abs(R - r) <= tol && Math.Abs(G - g) <= tol && Math.Abs(B - b) <= tol, $"{hex} !~ ({r},{g},{b})");
    }

    [Fact]
    public void Png_roundtrip_is_exact()
    {
        var png = TestImages.Encode(TwoColor(64, 32), 64, 32, SKEncodedImageFormat.Png);
        var pal = PaletteExtractor.Extract(ImageLoader.Decode(png).Rgba);
        Assert.Equal(new[] { "#FA1414", "#1414FA" }.OrderBy(x => x), pal.Select(c => c.Hex).OrderBy(x => x));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Lossy_formats_decode_within_tolerance(SKEncodedImageFormat fmt)
    {
        var data = TestImages.Encode(TwoColor(256, 128), 256, 128, fmt, 95);
        var pal = PaletteExtractor.Extract(ImageLoader.Decode(data).Rgba);
        Assert.Equal(2, pal.Count);
        Assert.Contains(pal, c => Near(c, 250, 20, 20));
        Assert.Contains(pal, c => Near(c, 20, 20, 250));
    }

    private static bool Near(PaletteColor c, int r, int g, int b) =>
        Math.Abs(c.R - r) <= 25 && Math.Abs(c.G - g) <= 25 && Math.Abs(c.B - b) <= 25;

    [Fact]
    public void Large_image_is_downscaled_to_max_edge()
    {
        var png = TestImages.Encode(TwoColor(1000, 500), 1000, 500, SKEncodedImageFormat.Png);
        var img = ImageLoader.Decode(png);
        Assert.Equal(ImageLoader.MaxEdge, img.Width);
        Assert.Equal(128, img.Height);
        Assert.Equal(img.Width * img.Height * 4, img.Rgba.Length);
    }

    [Theory]
    [InlineData("a.gif")] [InlineData("a.bmp")] [InlineData("a.img")] [InlineData("a")]
    public void Unsupported_extensions_are_rejected(string name)
    {
        Assert.False(ImageLoader.IsSupportedExtension(name));
        Assert.Throws<ImageLoadException>(() => ImageLoader.Load(name));
    }

    [Theory]
    [InlineData("a.PNG")] [InlineData("a.JPG")] [InlineData("a.Jpeg")] [InlineData("a.WEBP")]
    public void Supported_extensions_are_case_insensitive_under_tr_culture(string name)
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.True(ImageLoader.IsSupportedExtension(name));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Gif_content_with_png_name_is_rejected()
    {
        // Geçerli 1x1 GIF (Skia GIF kodlayamaz, bu yüzden hazır baytlar).
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        var path = Path.Combine(Path.GetTempPath(), $"pm-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, gif);
        try { Assert.Throws<ImageLoadException>(() => ImageLoader.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Garbage_bytes_are_rejected() =>
        Assert.Throws<ImageLoadException>(() => ImageLoader.Decode(new byte[] { 1, 2, 3, 4, 5 }));

    [Fact]
    public void Jpeg_large_image_is_downscaled_to_max_edge()
    {
        var jpg = TestImages.Encode(TwoColor(1000, 500), 1000, 500, SKEncodedImageFormat.Jpeg, 90);
        var img = ImageLoader.Decode(jpg);
        // JPEG, 1/2, 1/4, 1/8 ölçekli çözebilir; uzun kenar 256'yı aşmamalı, oran korunmalı.
        Assert.InRange(img.Width, 125, ImageLoader.MaxEdge);
        Assert.Equal(img.Width / 2, img.Height);
        Assert.Equal(img.Width * img.Height * 4, img.Rgba.Length);
    }

    [Fact]
    public void Huge_declared_dimensions_are_rejected_without_decoding()
    {
        // Küçük bir PNG'nin IHDR'ını 60000x60000 yap (CRC'yi yeniden hesapla): başlık küçük, çözülmüş boyut ~14 GB.
        var png = TestImages.Encode(TwoColor(8, 8), 8, 8, SKEncodedImageFormat.Png);
        WriteBigEndian(png, 16, 60000);
        WriteBigEndian(png, 20, 60000);
        WriteBigEndian(png, 29, Crc32(png.AsSpan(12, 17)));
        var ex = Assert.Throws<ImageLoadException>(() => ImageLoader.Decode(png));
        Assert.Contains("çözünürlüğü", ex.Message);
    }

    private static void WriteBigEndian(byte[] b, int at, uint v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var x in data)
        {
            crc ^= x;
            for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
