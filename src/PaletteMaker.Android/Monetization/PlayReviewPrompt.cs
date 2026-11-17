using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Avalonia.Threading;
using Google.Android.Play.Core.Review;
using PaletteMaker.Engagement;
using PaletteMaker.ViewModels;

namespace PaletteMaker.Droid.Monetization;

/// <summary>
/// Play içi değerlendirme isteği (yalnızca Store=play). Başarılı palet sayısını SharedPreferences'ta sayar ve
/// <see cref="ReviewPolicy"/> uygunsa, palet ekranda göründükten kısa süre sonra Google'ın değerlendirme sayfasını açar.
/// Google kendi kotasını uygular: istek hiç gösterilmeyebilir; bu normaldir ve kullanıcıya hata gösterilmez.
/// </summary>
public sealed class PlayReviewPrompt : IDisposable
{
    private const string PrefsName = "palettemaker_review";
    private const string KeyPalettes = "palettes", KeyFirst = "first_launch", KeyLast = "last_prompt";

    private readonly Activity _activity;
    private readonly MainViewModel _vm;
    private readonly ISharedPreferences _prefs;
    private bool _disposed;

    public PlayReviewPrompt(Activity activity, MainViewModel vm)
    {
        _activity = activity;
        _vm = vm;
        _prefs = activity.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        if (!_prefs.Contains(KeyFirst)) _prefs.Edit()!.PutLong(KeyFirst, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())!.Apply();
        _vm.PaletteLoaded += OnPaletteLoaded;
    }

    private ReviewState Load()
    {
        long first = _prefs.GetLong(KeyFirst, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        long last = _prefs.GetLong(KeyLast, 0);
        return new ReviewState(
            _prefs.GetInt(KeyPalettes, 0),
            DateTimeOffset.FromUnixTimeMilliseconds(first),
            last == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(last));
    }

    private async void OnPaletteLoaded(object? sender, EventArgs e)
    {
        var state = ReviewPolicy.CountPalette(Load());
        _prefs.Edit()!.PutInt(KeyPalettes, state.SuccessfulPalettes)!.Apply();

        if (!ReviewPolicy.ShouldPrompt(state, DateTimeOffset.UtcNow, otherFlowOpen: _vm.IsExportOpen || _vm.IsBusy)) return;
        // Önce zamanı kaydet: akış başarısız olsa bile aynı oturumda tekrar tekrar denenmesin.
        _prefs.Edit()!.PutLong(KeyLast, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())!.Apply();

        try
        {
            await Task.Delay(1500); // palet görünsün, sonra iste
            if (_disposed || _vm.IsExportOpen) return;
#if DEBUG
            IReviewManager manager = new Google.Android.Play.Core.Review.Testing.FakeReviewManager(_activity); // Play'siz denemek için sahte
#else
            IReviewManager manager = ReviewManagerFactory.Create(_activity)!;
#endif
            var info = await manager.RequestReviewFlow()!.AsAsync<ReviewInfo>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (!_disposed) await manager.LaunchReviewFlow(_activity, info)!.AsAsync();
            });
        }
        catch (Exception)
        {
            // Değerlendirme isteği isteğe bağlıdır; hata kullanıcıya yansıtılmaz.
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _vm.PaletteLoaded -= OnPaletteLoaded;
    }
}
