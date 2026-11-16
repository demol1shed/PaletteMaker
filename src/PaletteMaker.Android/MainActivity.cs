using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using Avalonia.Android;
using Avalonia.Threading;
using AndroidUri = Android.Net.Uri;

namespace PaletteMaker.Droid;

/// <summary>
/// Tek etkinlik. Görünümü <see cref="App"/> içindeki MainViewFactory üretir. Ek olarak başka uygulamalardan
/// "Paylaş" (ACTION_SEND) ve "Birlikte aç" (ACTION_VIEW) ile gelen görselleri karşılar; masaüstündeki
/// sürükle-bırakın karşılığıdır.
/// </summary>
[Activity(
    Label = "Palet Çıkarıcı",
    Theme = "@style/AppTheme",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    Exported = true,
    // Açıkken yeni bir paylaşım gelirse ikinci bir etkinlik açılmaz, OnNewIntent çağrılır.
    LaunchMode = LaunchMode.SingleTop,
    // Döndürme, tema ve ekran değişimlerinde etkinlik yeniden oluşturulmaz; Avalonia yerleşimi kendisi günceller.
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode | ConfigChanges.Density
                           | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault],
    DataMimeTypes = ["image/png", "image/jpeg", "image/jpg", "image/webp"])]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault], DataScheme = "content",
    DataMimeTypes = ["image/png", "image/jpeg", "image/jpg", "image/webp"])]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Sistem etkinliği eski durumdan geri yüklüyorsa aynı paylaşımı ikinci kez işleme.
        if (savedInstanceState == null) HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    private void HandleIntent(Intent? intent)
    {
        if (intent == null) return;
        AndroidUri? uri = intent.Action switch
        {
            Intent.ActionSend => IntentCompat.GetParcelableExtra(intent, Intent.ExtraStream,
                Java.Lang.Class.FromType(typeof(AndroidUri))) as AndroidUri,
            Intent.ActionView => intent.Data,
            _ => null,
        };
        var resolver = ContentResolver;
        if (uri == null || resolver == null || Avalonia.Application.Current is not App app) return;

        string? name = DisplayName(resolver, uri);
        // Akış, görünüm modelinin arka plan iş parçacığında açılır ve okunur (URI izni etkinlik yaşadıkça geçerlidir).
        Dispatcher.UIThread.Post(() => _ = app.MainVm.LoadAsync(name, () => Task.FromResult(Open(resolver, uri))));
    }

    private static Stream Open(ContentResolver resolver, AndroidUri uri)
    {
        try
        {
            return resolver.OpenInputStream(uri) ?? throw new IOException("İçerik sağlayıcı akış vermedi.");
        }
        catch (Java.Lang.Exception ex) // FileNotFoundException, SecurityException …
        {
            throw new IOException(ex.Message, ex);
        }
    }

    /// <summary>Sağlayıcının bildirdiği dosya adı; yoksa adresin son parçası. Uzantısız olabilir.</summary>
    private static string? DisplayName(ContentResolver resolver, AndroidUri uri)
    {
        try
        {
            using var cursor = resolver.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                int column = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
                if (column >= 0 && cursor.GetString(column) is { Length: > 0 } name) return name;
            }
        }
        catch (Java.Lang.Exception) { /* ad alınamazsa içerik denetimi yeterlidir */ }
        return uri.LastPathSegment;
    }
}
