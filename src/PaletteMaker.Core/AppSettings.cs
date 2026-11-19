using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaletteMaker.Core;

public enum ThemeChoice { System = 0, Light = 1, Dark = 2 }

public enum LanguageChoice { System = 0, English = 1, Turkish = 2 }

/// <summary>Kalıcı uygulama tercihleri. Her alan geçersiz değerde <see cref="Normalize"/> ile güvenli aralığa çekilir.</summary>
public sealed class AppSettings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public LanguageChoice Language { get; set; } = LanguageChoice.System;

    public int DefaultMaxColors { get; set; } = PaletteOptions.DefaultMaxColors;
    public double DefaultMergeStrength { get; set; } = PaletteOptions.DefaultMergeStrength;

    /// <summary>0 RGB, 1 CMYK, 2 HSL, 3 OKLCH (arayüzdeki <c>ValueFormat</c> sırası).</summary>
    public int DefaultValueFormat { get; set; }

    public bool HexLowercase { get; set; }
    public bool HexWithHash { get; set; } = true;

    /// <summary>Paylaşılan kart PNG'sinde görseldeki pay yazılsın mı.</summary>
    public bool CardShowsShare { get; set; } = true;

    [JsonIgnore]
    public HexStyle HexStyle => new(HexLowercase, !HexWithHash);

    /// <summary>Değerleri geçerli aralıklara çeker (bozuk dosya, elle düzenleme).</summary>
    public AppSettings Normalize()
    {
        if (!Enum.IsDefined(Theme)) Theme = ThemeChoice.System;
        if (!Enum.IsDefined(Language)) Language = LanguageChoice.System;
        DefaultMaxColors = Math.Clamp(DefaultMaxColors, PaletteOptions.MinColorsLimit, PaletteOptions.MaxColorsLimit);
        DefaultMergeStrength = double.IsNaN(DefaultMergeStrength) ? PaletteOptions.DefaultMergeStrength : Math.Clamp(DefaultMergeStrength, 0, 1);
        if (DefaultValueFormat is < 0 or > 3) DefaultValueFormat = 0;
        return this;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

/// <summary>Ayarları JSON dosyasından okur / dosyaya yazar. Eksik ya da bozuk dosya varsayılanları verir; yazma atomiktir.</summary>
public static class AppSettingsStore
{
    public static AppSettings Load(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(path);
            return (JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings()).Normalize();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Başarılıysa true. Yazılamıyorsa (salt okunur disk vb.) sessizce false döner; ayar yalnızca bu oturumda geçerli kalır.</summary>
    public static bool Save(string? path, AppSettings settings)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
