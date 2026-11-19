using PaletteMaker.Core;

namespace PaletteMaker.Settings;

/// <summary>
/// Kalıcı tercihlerin tek sahibi. <paramref name="path"/> boşsa (testler, yol atanmamış) yalnızca bellekte tutar, dosyaya dokunmaz.
/// </summary>
public sealed class SettingsService
{
    private readonly string? _path;

    public SettingsService(string? path = null)
    {
        _path = path;
        Current = AppSettingsStore.Load(path);
    }

    /// <summary>Canlı ayar nesnesi; değiştirdikten sonra <see cref="Save"/> çağrılır.</summary>
    public AppSettings Current { get; private set; }

    public void Save() => AppSettingsStore.Save(_path, Current);

    /// <summary>Tüm tercihleri varsayılana döndürür ve kaydeder.</summary>
    public void Reset()
    {
        Current = new AppSettings();
        Save();
    }

    /// <summary>Masaüstü ve Android'de ortak: kullanıcıya özel uygulama verisi klasörü altında <c>settings.json</c>.</summary>
    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PaletteMaker", "settings.json");
}
