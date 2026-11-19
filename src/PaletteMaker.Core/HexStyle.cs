namespace PaletteMaker.Core;

/// <summary>
/// HEX kodunun yazım tercihi. <c>default</c> değer bugünkü davranıştır (<c>#E63946</c>: büyük harf, "#" var).
/// </summary>
public readonly record struct HexStyle(bool Lowercase = false, bool OmitHash = false)
{
    /// <summary>"#E63946" biçimindeki kodu tercihe göre yazar (ör. <c>e63946</c>).</summary>
    public string Apply(string hex)
    {
        var s = Lowercase ? hex.ToLowerInvariant() : hex;
        return OmitHash ? s.TrimStart('#') : s;
    }

    /// <summary>"#" her zaman korunur (CSS, XML, JSON gibi söz diziminin gerektirdiği yerler); yalnızca harf boyu uygulanır.</summary>
    public HexStyle KeepHash => this with { OmitHash = false };
}
