namespace PaletteMaker.Core;

/// <summary>Renk uyumu kuralları (Adobe Color'daki gibi). Sıra arayüzdeki açılır menü sırasıdır.</summary>
public enum HarmonyRule
{
    Analogous,
    Monochromatic,
    Triad,
    Complementary,
    SplitComplementary,
    DoubleSplitComplementary,
    Square,
    Compound,
    Shades,
}

/// <summary>
/// Bir taban renkten 5 renklik uyum paleti üretir. Taban renk her zaman <see cref="BaseIndex"/> (ortadaki) sıradadır ve
/// girdiyle birebir aynıdır. Her kural, her sıra için (ton farkı, açıklık kayması) çiftleri tablosudur; sonuç deterministiktir.
/// </summary>
public static class ColorHarmony
{
    public const int Count = 5;
    public const int BaseIndex = 2;

    /// <summary>
    /// Açıklık kayması −1…+1: pozitif değer rengi o oranda beyaza (doygunluk düşer, parlaklık artar), negatif değer siyaha
    /// (parlaklık düşer) doğru karıştırır. 0 → değişmez.
    /// </summary>
    private readonly record struct Slot(double HueOffset, double Shift = 0);

    private static readonly Dictionary<HarmonyRule, Slot[]> Rules = new()
    {
        [HarmonyRule.Analogous] = [new(-60), new(-30), new(0), new(30), new(60)],
        [HarmonyRule.Monochromatic] = [new(0, 0.5), new(0, 0.25), new(0), new(0, -0.25), new(0, -0.5)],
        [HarmonyRule.Triad] = [new(120, -0.3), new(120), new(0), new(-120), new(-120, -0.3)],
        [HarmonyRule.Complementary] = [new(180, -0.3), new(180), new(0), new(0, -0.35), new(0, 0.35)],
        [HarmonyRule.SplitComplementary] = [new(150, -0.3), new(150), new(0), new(210), new(210, -0.3)],
        [HarmonyRule.DoubleSplitComplementary] = [new(150), new(-30), new(0), new(30), new(210)],
        [HarmonyRule.Square] = [new(90), new(180), new(0), new(270), new(0, -0.35)],
        [HarmonyRule.Compound] = [new(180), new(210), new(0), new(30), new(60)],
        // Yalnızca parlaklık değişir (doygunluk aynı kalır); ayrı bir tablo girdisi olarak Generate içinde işlenir.
        [HarmonyRule.Shades] = [new(0, -0.7), new(0, -0.4), new(0), new(0, 0.5), new(0, 1.0)],
    };

    /// <summary>Verilen sıradaki rengin tabana göre ton farkı (derece, RYB).</summary>
    public static double HueOffset(HarmonyRule rule, int index) => Rules[rule][index].HueOffset;

    public static IReadOnlyList<Hsv> Generate(HarmonyRule rule, Hsv baseColor)
    {
        var slots = Rules[rule];
        double s = Math.Clamp(baseColor.S, 0, 1), v = Math.Clamp(baseColor.V, 0, 1);
        var result = new Hsv[Count];
        for (int i = 0; i < Count; i++)
        {
            var slot = slots[i];
            double h = RybWheel.Normalize(baseColor.H + slot.HueOffset);
            result[i] = i == BaseIndex
                ? new Hsv(RybWheel.Normalize(baseColor.H), s, v)
                : rule == HarmonyRule.Shades ? ShadeOnly(h, s, v, slot.Shift) : Shift(h, s, v, slot.Shift);
        }
        return result;
    }

    private static Hsv Shift(double h, double s, double v, double t) => t switch
    {
        > 0 => new Hsv(h, s * (1 - t), v + (1 - v) * t),
        < 0 => new Hsv(h, s, v * (1 + t)),
        _ => new Hsv(h, s, v),
    };

    /// <summary>"Tonlar": doygunluğa dokunmadan yalnızca parlaklık değişir (negatif: orantılı koyulaşır, pozitif: beyaza doğru açılır).</summary>
    private static Hsv ShadeOnly(double h, double s, double v, double t) =>
        new(h, s, t < 0 ? v * (1 + t) : v + (1 - v) * t * 0.6);
}
