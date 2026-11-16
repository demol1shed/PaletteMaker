using SkiaSharp;

namespace PaletteMaker.Tests;

internal static class TestImages
{
    public static byte[] Rgba(int w, int h, Func<int, int, (byte R, byte G, byte B, byte A)> px)
    {
        var buf = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var (r, g, b, a) = px(x, y);
                int i = (y * w + x) * 4;
                buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; buf[i + 3] = a;
            }
        return buf;
    }

    public static byte[] Encode(byte[] rgba, int w, int h, SKEncodedImageFormat fmt, int quality = 100)
    {
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(fmt, quality);
        return data.ToArray();
    }
}
