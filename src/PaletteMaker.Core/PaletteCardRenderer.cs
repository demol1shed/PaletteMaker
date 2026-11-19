using SkiaSharp;

namespace PaletteMaker.Core;

/// <summary>
/// Paleti paylaşılabilir bir PNG karta çizer: renk karoları ızgarası, her karoda HEX kodu ve görseldeki pay;
/// yazı rengi karonun açıklığına göre siyah ya da beyazdır.
/// </summary>
public static class PaletteCardRenderer
{
    public const int CardWidth = 1080;

    private static readonly SKColor Background = new(0xF4, 0xF4, 0xF4);

    /// <summary>Renk sayısına göre sütun: az renkte büyük karolar, çok renkte (48'e kadar) küçük karolar.</summary>
    public static int ColumnsFor(int count) => count <= 3 ? Math.Max(count, 1) : count <= 12 ? 3 : count <= 24 ? 4 : 6;

    /// <param name="showShare">false: HEX'in altına görseldeki pay yazılmaz (renk uyumu paletleri).</param>
    public static byte[] RenderPng(IReadOnlyList<PaletteColor> palette, bool showShare = true)
    {
        if (palette.Count == 0) throw new ArgumentException("Palette is empty.", nameof(palette));
        int cols = ColumnsFor(palette.Count);
        int rows = (palette.Count + cols - 1) / cols;
        int tile = CardWidth / cols;
        int width = tile * cols, height = tile * rows;

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background); // son satırdaki boş hücreler siyah kalmasın
        using var hexFont = new SKFont(SKTypeface.Default, tile * 0.13f);
        using var shareFont = new SKFont(SKTypeface.Default, tile * 0.09f);
        for (int i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            float x = (i % cols) * tile, y = (i / cols) * tile;
            using (var fill = new SKPaint { Color = new SKColor(c.R, c.G, c.B), IsAntialias = false })
                canvas.DrawRect(x, y, tile, tile, fill);
            // WCAG göreli parlaklığa yakın basit ölçü: açık zeminde siyah, koyu zeminde beyaz yazı.
            double luminance = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
            var ink = luminance > 0.55 ? SKColors.Black : SKColors.White;
            using var paint = new SKPaint { Color = ink, IsAntialias = true };
            float pad = tile * 0.07f;
            if (showShare)
            {
                canvas.DrawText(c.Hex, x + pad, y + tile - pad - shareFont.Size * 1.3f, SKTextAlign.Left, hexFont, paint);
                canvas.DrawText(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{c.Share * 100:0.#}%"),
                    x + pad, y + tile - pad, SKTextAlign.Left, shareFont, paint);
            }
            else
            {
                canvas.DrawText(c.Hex, x + pad, y + tile - pad, SKTextAlign.Left, hexFont, paint);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
