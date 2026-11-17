using Android.App;
using Google.Android.Gms.Ads;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PaletteMaker.Droid.Monetization;

/// <summary>
/// AdMob satır içi uyarlanabilir banner'ı Avalonia sayfasına gömer. Reklam yüklenene kadar (ve yüklenemezse)
/// yüksekliği 0'dır: sayfada boş alan bırakmaz. En fazla 90 dp yüksekliğindedir (geçiş/tam ekran reklam yok).
/// </summary>
public sealed class AdBannerHost : NativeControlHost
{
    private const int MaxHeightDp = 90;
    private const int PageMarginDp = 16;

    private readonly Activity _activity;
    private readonly string _unitId;
    private AdView? _ad;

    public AdBannerHost(Activity activity, string unitId)
    {
        _activity = activity;
        _unitId = unitId;
        Height = 0;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var density = _activity.Resources?.DisplayMetrics?.Density ?? 1f;
        double widthDp = Bounds.Width > 0
            ? Bounds.Width
            : (_activity.Resources?.DisplayMetrics?.WidthPixels ?? 0) / density - 2 * PageMarginDp;
        int width = (int)Math.Max(320, Math.Floor(widthDp));

        _ad = new AdView(_activity)
        {
            AdUnitId = _unitId,
            AdSize = AdSize.GetInlineAdaptiveBannerAdSize(width, MaxHeightDp),
        };
        _ad.AdListener = new BannerListener(
            onLoaded: () => Dispatcher.UIThread.Post(() =>
            {
                if (_ad == null) return;
                Height = Math.Max(0, _ad.AdSize.GetHeightInPixels(_activity) / density);
            }),
            onFailed: _ => Dispatcher.UIThread.Post(() => Height = 0));
        _ad.LoadAd(new AdRequest.Builder().Build());
        return new AndroidViewControlHandle(_ad);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _ad?.Destroy();
        _ad = null;
        base.DestroyNativeControlCore(control);
    }

    public void Pause() => _ad?.Pause();
    public void Resume() => _ad?.Resume();
}
