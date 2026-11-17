using Avalonia.Headless.XUnit;
using PaletteMaker.Core;
using PaletteMaker.Imaging;
using PaletteMaker.ViewModels;
using SkiaSharp;
using Xunit;

namespace PaletteMaker.UI.Tests;

/// <summary>Android'in HEIC/AVIF gibi biçimleri PNG'ye çeviren çözücüsü; burada sahte bir çözücüyle akış denenir.</summary>
public class TranscoderTests
{
    private sealed class FakeTranscoder(Func<byte[], byte[]?> convert) : IImageTranscoder
    {
        public int Calls;
        public IReadOnlyCollection<string> Extensions { get; } = [".heic", ".heif", ".avif", ".gif", ".bmp"];
        public IReadOnlyCollection<string> MimeTypes { get; } = ["image/heic", "image/heif", "image/avif", "image/gif", "image/bmp"];
        public byte[]? TryConvertToPng(byte[] data) { Calls++; return convert(data); }
    }

    private static byte[] RedPng()
    {
        using var bmp = new SKBitmap(new SKImageInfo(40, 20, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var c = new SKCanvas(bmp)) c.Clear(new SKColor(200, 30, 30));
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static readonly byte[] FakeHeic = [0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63]; // "ftypheic": Skia tanımaz

    private static Task Load(MainViewModel vm, string name, byte[] bytes) =>
        vm.LoadAsync(name, () => Task.FromResult<Stream>(new MemoryStream(bytes)));

    [AvaloniaFact]
    public async Task Without_a_transcoder_heic_is_rejected_by_name()
    {
        var vm = new MainViewModel();
        await Load(vm, "IMG_0001.HEIC", FakeHeic);
        Assert.Equal(MainViewModel.ErrorText(ImageLoadError.UnsupportedType), vm.ErrorMessage);
        Assert.False(vm.HasPalette);
    }

    [AvaloniaFact]
    public async Task With_a_transcoder_heic_is_converted_and_a_palette_appears()
    {
        var png = RedPng();
        var t = new FakeTranscoder(_ => png);
        var vm = new MainViewModel { Transcoder = t };
        await Load(vm, "IMG_0001.HEIC", FakeHeic);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(1, t.Calls);
        Assert.True(vm.HasPalette);
        Assert.Equal("#C81E1E", vm.Palette[0].Hex);
        Assert.NotNull(vm.Preview); // önizleme dönüştürülmüş PNG'den
    }

    [AvaloniaFact]
    public async Task Extensionless_names_from_content_providers_are_converted_too()
    {
        var png = RedPng();
        var vm = new MainViewModel { Transcoder = new FakeTranscoder(_ => png) };
        await Load(vm, "1000012345", FakeHeic);
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.HasPalette);
    }

    [AvaloniaFact]
    public async Task Supported_formats_never_reach_the_transcoder()
    {
        var t = new FakeTranscoder(_ => throw new InvalidOperationException("çağrılmamalı"));
        var vm = new MainViewModel { Transcoder = t };
        await Load(vm, "a.png", RedPng());
        Assert.Equal(0, t.Calls);
        Assert.True(vm.HasPalette);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task When_the_transcoder_fails_or_returns_garbage_the_error_is_shown(bool garbage)
    {
        var t = new FakeTranscoder(_ => garbage ? [1, 2, 3, 4] : null);
        var vm = new MainViewModel { Transcoder = t };
        await Load(vm, "IMG.heic", FakeHeic);
        Assert.Equal(1, t.Calls);
        Assert.Equal(MainViewModel.ErrorText(ImageLoadError.NotAnImage), vm.ErrorMessage); // çıktı da içerik doğrulamasından geçer
        Assert.False(vm.HasPalette);
    }

    [AvaloniaFact]
    public async Task Unrelated_extensions_are_still_rejected_even_with_a_transcoder()
    {
        var t = new FakeTranscoder(_ => RedPng());
        var vm = new MainViewModel { Transcoder = t };
        await Load(vm, "belge.pdf", FakeHeic);
        Assert.Equal(MainViewModel.ErrorText(ImageLoadError.UnsupportedType), vm.ErrorMessage);
        Assert.Equal(0, t.Calls);
    }
}
