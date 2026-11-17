using System.Globalization;
using System.Resources;
using Avalonia.Markup.Xaml;

namespace PaletteMaker.Localization;

/// <summary>
/// Arayüz metinleri. Dil, açılışta <see cref="CultureInfo.CurrentUICulture"/>'dan (cihaz/sistem dili) belirlenir;
/// çalışma zamanında geçiş yoktur. Nötr kaynak İngilizcedir, Türkçe <c>Strings.tr.resx</c>'tedir.
/// <c>"Anahtar.Android"</c> girdisi varsa Android'de o kullanılır (örn. "tıkla" yerine "dokun").
/// </summary>
public static class Loc
{
    private static readonly ResourceManager Manager = new("PaletteMaker.Localization.Strings", typeof(Loc).Assembly);

    public static string Get(string key)
    {
        var culture = CultureInfo.CurrentUICulture;
        if (OperatingSystem.IsAndroid() && Manager.GetString(key + ".Android", culture) is { } platform) return platform;
        return Manager.GetString(key, culture) ?? $"!{key}!";
    }

    /// <summary>Yer tutuculu metin; sayılar kültürden bağımsız (InvariantCulture) biçimlenir.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), args);
}

/// <summary>XAML'de <c>{loc:T AnahtarAdı}</c>; <c>xmlns:loc="using:PaletteMaker.Localization"</c>.</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(key);
}
