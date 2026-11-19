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
    Label = "@string/app_name",
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
    DataMimeTypes = ["image/png", "image/jpeg", "image/jpg", "image/webp", "image/heic", "image/heif", "image/avif", "image/gif", "image/bmp"])]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault], DataScheme = "content",
    DataMimeTypes = ["image/png", "image/jpeg", "image/jpg", "image/webp", "image/heic", "image/heif", "image/avif", "image/gif", "image/bmp"])]
public class MainActivity : AvaloniaMainActivity
{
#if PLAY_STORE
    private PaletteMaker.Droid.Monetization.PlayMonetization? _monetization;
    private PaletteMaker.Droid.Monetization.AdBannerHost? _banner;
    private PaletteMaker.Droid.Monetization.PlayReviewPrompt? _review;
#endif

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Sistem paylaşım menüsü ("Palet görselini paylaş"); görünüm oluşmadan önce atanır.
        App.ShareService = new PaletteMaker.Droid.Sharing.AndroidShareService(this);
        // Android 13+ panoya kopyalamada kendi onayını gösterir; bizim toast'ımız çift mesaj olurdu.
        App.SystemShowsClipboardConfirmation = Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu;
        // HEIC/AVIF/GIF/BMP: Skia açamaz, Android'in sistem çözücüsü PNG'ye çevirir.
        if (Avalonia.Application.Current is App imagingApp)
            imagingApp.MainVm.Transcoder = new PaletteMaker.Droid.Imaging.AndroidImageTranscoder();
#if PLAY_STORE
        // Google Play sürümü: reklam ve satın alma. Görünüm (base.OnCreate) oluşmadan önce bağlanmalı,
        // çünkü MainViewFactory orada çağrılır. GitHub / kendi derlemede bu blok derlenmez.
        if (Avalonia.Application.Current is App playApp)
        {
            var monetization = _monetization = new PaletteMaker.Droid.Monetization.PlayMonetization(this);
            playApp.MainVm.Monetization = monetization;
            App.AdControlFactory = () => _banner = new PaletteMaker.Droid.Monetization.AdBannerHost(this, monetization.BannerUnitId);
            monetization.Start();
            _review = new PaletteMaker.Droid.Monetization.PlayReviewPrompt(this, playApp.MainVm);
        }
#endif
        base.OnCreate(savedInstanceState);
        // Sistem etkinliği eski durumdan geri yüklüyorsa aynı paylaşımı ikinci kez işleme.
        if (savedInstanceState == null) HandleIntent(Intent);
    }

#if PLAY_STORE
    protected override void OnPause() { _banner?.Pause(); base.OnPause(); }
    protected override void OnResume() { base.OnResume(); _banner?.Resume(); }

    protected override void OnDestroy()
    {
        if (Avalonia.Application.Current is App playApp && ReferenceEquals(playApp.MainVm.Monetization, _monetization))
            playApp.MainVm.Monetization = PaletteMaker.Monetization.NullMonetization.Instance;
        _monetization?.Dispose();
        _monetization = null;
        _review?.Dispose();
        _review = null;
        _banner = null;
        base.OnDestroy();
    }
#endif

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
        Dispatcher.UIThread.Post(() =>
        {
            // Renk uyumu ya da Ayarlar sekmesindeyken paylaşılan görsel sessizce yüklenmesin: Görsel sekmesine geç.
            app.ShellVm.Tab = PaletteMaker.ViewModels.ShellTab.Image;
            _ = app.MainVm.LoadAsync(name, () => Task.FromResult(Open(resolver, uri)));
        });
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
