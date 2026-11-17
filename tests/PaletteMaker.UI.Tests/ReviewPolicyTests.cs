using PaletteMaker.Engagement;
using PaletteMaker.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace PaletteMaker.UI.Tests;

public class ReviewPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Does_not_prompt_before_enough_use()
    {
        var s = new ReviewState(ReviewPolicy.MinPalettes - 1, T0, null);
        Assert.False(ReviewPolicy.ShouldPrompt(s, T0.AddDays(30), otherFlowOpen: false));
    }

    [Fact]
    public void Does_not_prompt_in_the_first_days_even_with_many_palettes()
    {
        var s = new ReviewState(50, T0, null);
        Assert.False(ReviewPolicy.ShouldPrompt(s, T0.AddDays(2), false));
        Assert.True(ReviewPolicy.ShouldPrompt(s, T0.AddDays(3), false));
    }

    [Fact]
    public void Prompts_once_then_waits_sixty_days()
    {
        var s = new ReviewState(10, T0, T0.AddDays(10));
        Assert.False(ReviewPolicy.ShouldPrompt(s, T0.AddDays(10).AddDays(59), false));
        Assert.True(ReviewPolicy.ShouldPrompt(s, T0.AddDays(10).AddDays(60), false));
    }

    [Fact]
    public void Never_prompts_while_another_flow_is_open()
    {
        var s = new ReviewState(10, T0, null);
        Assert.True(ReviewPolicy.ShouldPrompt(s, T0.AddDays(5), false));
        Assert.False(ReviewPolicy.ShouldPrompt(s, T0.AddDays(5), otherFlowOpen: true));
    }

    [Fact]
    public void Counting_a_palette_adds_exactly_one()
    {
        var s = new ReviewState(4, T0, null);
        Assert.Equal(5, ReviewPolicy.CountPalette(s).SuccessfulPalettes);
        Assert.Equal(4, s.SuccessfulPalettes); // değişmez kayıt
    }

    [Fact]
    public void Threshold_boundary_is_exact()
    {
        var s = new ReviewState(ReviewPolicy.MinPalettes, T0, null);
        Assert.True(ReviewPolicy.ShouldPrompt(s, T0 + ReviewPolicy.MinAge, false));
        Assert.False(ReviewPolicy.ShouldPrompt(s, T0 + ReviewPolicy.MinAge - TimeSpan.FromSeconds(1), false));
    }
}

public class PaletteLoadedEventTests
{
    private static byte[] Png()
    {
        using var bmp = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(40, 20, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
        using (var c = new SkiaSharp.SKCanvas(bmp)) { c.Clear(new SkiaSharp.SKColor(200, 30, 30)); }
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [AvaloniaFact]
    public async Task Fires_once_per_successful_load_but_not_for_slider_changes_or_errors()
    {
        var vm = new MainViewModel();
        int count = 0;
        vm.PaletteLoaded += (_, _) => count++;
        var png = Png();

        await vm.LoadAsync("a.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
        Assert.Equal(1, count);

        vm.MaxColors = 5; await Task.Delay(400); // yeniden hesaplama, yeni görsel değil
        Assert.Equal(1, count);

        await vm.LoadAsync("kotu.png", () => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 }))); // geçersiz görsel
        Assert.Equal(1, count);

        await vm.LoadAsync("b.png", () => Task.FromResult<Stream>(new MemoryStream(png)));
        Assert.Equal(2, count);
    }
}
