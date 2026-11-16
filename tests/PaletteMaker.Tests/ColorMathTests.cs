using PaletteMaker.Core;
using Xunit;

namespace PaletteMaker.Tests;

public class ColorMathTests
{
    [Theory]
    [InlineData(255, 0, 0, 0, 100, 100, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 100)]
    [InlineData(255, 255, 255, 0, 0, 0, 0)]
    [InlineData(34, 139, 230, 85, 40, 0, 10)]
    public void Cmyk_conversion(byte r, byte g, byte b, int c, int m, int y, int k) =>
        Assert.Equal((c, m, y, k), ColorMath.ToCmyk(r, g, b));

    [Fact]
    public void Hex_is_uppercase_padded() =>
        Assert.Equal("#0A0BFF", ColorMath.ToHex(10, 11, 255));

    [Fact]
    public void Oklab_roundtrip_is_exact_for_all_grays_and_samples()
    {
        for (int v = 0; v < 256; v++)
            Assert.Equal(((byte)v, (byte)v, (byte)v), ColorMath.OklabToRgb(ColorMath.RgbToOklab(v, v, v)));
        var rnd = new Random(1);
        for (int i = 0; i < 2000; i++)
        {
            byte r = (byte)rnd.Next(256), g = (byte)rnd.Next(256), b = (byte)rnd.Next(256);
            Assert.Equal((r, g, b), ColorMath.OklabToRgb(ColorMath.RgbToOklab(r, g, b)));
        }
    }

    [Fact]
    public void Formatting_is_culture_invariant()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("rgb(34, 139, 230)", ColorMath.FormatRgb(34, 139, 230));
            Assert.Equal("cmyk(85%, 40%, 0%, 10%)", ColorMath.FormatCmyk(34, 139, 230));
            Assert.Equal("#ABCDEF", ColorMath.ToHex(0xAB, 0xCD, 0xEF));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }
}
