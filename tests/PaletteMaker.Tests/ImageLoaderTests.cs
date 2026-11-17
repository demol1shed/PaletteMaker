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
        Assert.Equal(ImageLoadError.UnsupportedType, Assert.Throws<ImageLoadException>(() => ImageLoader.Load(name)).Kind);
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
        try { Assert.Equal(ImageLoadError.NotAnImage, Assert.Throws<ImageLoadException>(() => ImageLoader.Load(path)).Kind); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Garbage_bytes_are_rejected() =>
        Assert.Equal(ImageLoadError.NotAnImage, Assert.Throws<ImageLoadException>(() => ImageLoader.Decode(new byte[] { 1, 2, 3, 4, 5 })).Kind);

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
        Assert.Equal(ImageLoadError.ResolutionTooHigh, ex.Kind);
    }

    /// <summary>Android içerik sağlayıcılarının akışları gibi: aranamaz, uzunluğu bilinmez.</summary>
    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private int _pos;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = Math.Min(Math.Min(count, 7), data.Length - _pos); // küçük parçalar halinde gelsin
            Array.Copy(data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void ReadBounded_reads_non_seekable_stream_completely()
    {
        var png = TestImages.Encode(TwoColor(64, 64), 64, 64, SKEncodedImageFormat.Png);
        var read = ImageLoader.ReadBounded(new ForwardOnlyStream(png));
        Assert.Equal(png, read);
        Assert.Equal(2, PaletteExtractor.Extract(ImageLoader.Decode(read).Rgba).Count);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ReadBounded_rejects_streams_over_the_limit(bool seekable)
    {
        var data = new byte[1001];
        Stream stream = seekable ? new MemoryStream(data) : new ForwardOnlyStream(data);
        var ex = Assert.Throws<ImageLoadException>(() => ImageLoader.ReadBounded(stream, maxBytes: 1000));
        Assert.Equal(ImageLoadError.TooLarge, ex.Kind);
        Assert.Equal(1000, ImageLoader.ReadBounded(new MemoryStream(new byte[1000]), maxBytes: 1000).Length);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("1000012345")] [InlineData("foto.JPG")] [InlineData("a.webp")]
    public void Stream_names_without_or_with_supported_extension_are_accepted(string? name) =>
        ImageLoader.EnsureSupportedName(name);

    [Theory]
    [InlineData("a.gif")] [InlineData("a.heic")] [InlineData("belge.pdf")]
    public void Stream_names_with_unsupported_extension_are_rejected(string name) =>
        Assert.Equal(ImageLoadError.UnsupportedType, Assert.Throws<ImageLoadException>(() => ImageLoader.EnsureSupportedName(name)).Kind);

    [Theory]
    [InlineData("IMG_1.heic")] [InlineData("foto.HEIC")] [InlineData("a.avif")] [InlineData("a.Bmp")]
    public void Extra_extensions_are_accepted_only_when_the_platform_can_convert_them(string name)
    {
        string[] extras = [".heic", ".heif", ".avif", ".bmp"];
        ImageLoader.EnsureSupportedName(name, extraExtensions: extras);
        Assert.Equal(ImageLoadError.UnsupportedType, Assert.Throws<ImageLoadException>(() => ImageLoader.EnsureSupportedName(name)).Kind);
    }

    [Fact]
    public void Extra_extensions_do_not_widen_anything_else()
    {
        string[] extras = [".heic"];
        Assert.Throws<ImageLoadException>(() => ImageLoader.EnsureSupportedName("belge.pdf", extraExtensions: extras));
        Assert.Throws<ImageLoadException>(() => ImageLoader.EnsureSupportedName("a.exe", extraExtensions: extras));
        ImageLoader.EnsureSupportedName("a.png", extraExtensions: extras); // temel biçimler hep geçer
    }

    [Theory]
    [InlineData(null)] [InlineData("a")] [InlineData("a.gif")]
    public void Local_paths_require_a_supported_extension(string? name) =>
        Assert.Equal(ImageLoadError.UnsupportedType, Assert.Throws<ImageLoadException>(() => ImageLoader.EnsureSupportedName(name, requireExtension: true)).Kind);

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
