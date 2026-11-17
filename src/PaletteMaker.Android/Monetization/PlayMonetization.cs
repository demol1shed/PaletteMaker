using Android.App;
using Android.BillingClient.Api;
using Android.Content;
using Android.Content.PM;
using Google.Android.Gms.Ads;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PaletteMaker.Monetization;
using Xamarin.Google.UserMesssagingPlatform;

namespace PaletteMaker.Droid.Monetization;

/// <summary>
/// Google Play sürümünün reklam ve satın alma mantığı: önce UMP rıza akışı, ardından AdMob; "Reklamları kaldır"
/// tüketilmeyen (tek seferlik) bir ürün olarak Play Billing ile satılır.
/// Hak (reklamsız) SharedPreferences'ta önbelleğe alınır, böylece satın almış kullanıcı açılışta reklam görmez;
/// her açılışta Play'den doğrulanır (iade/iptal edilen alım geri alınır).
/// </summary>
public sealed class PlayMonetization : ObservableObject, IMonetization, IDisposable
{
    private const string PrefsName = "palettemaker_monetization";
    private const string KeyAdsRemoved = "ads_removed";
    private const string InApp = "inapp";

    private readonly Activity _activity;
    private readonly ISharedPreferences _prefs;
    private readonly IConsentInformation _consent;
    private BillingClient? _billing;
    private ProductDetails? _details;
    private bool _adsRemoved, _canRequestAds, _adsInitialized, _billingReady, _disposed;

    public PlayMonetization(Activity activity)
    {
        _activity = activity;
        _prefs = activity.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        _adsRemoved = _prefs.GetBoolean(KeyAdsRemoved, false);
        _consent = UserMessagingPlatform.GetConsentInformation(activity);
        BannerUnitId = MetaData("io.github.demol1shed.palettemaker.BANNER_UNIT_ID") ?? "";
        ProductId = MetaData("io.github.demol1shed.palettemaker.REMOVE_ADS_PRODUCT_ID") ?? "remove_ads";
    }

    public string BannerUnitId { get; }
    public string ProductId { get; }

    public bool ShowAds => !_adsRemoved && _canRequestAds && BannerUnitId.Length > 0;
    public bool RemoveAdsAvailable => !_adsRemoved && _billingReady && _details != null;
    public bool PrivacyOptionsRequired =>
        _consent.PrivacyOptionsRequirementStatus.Equals(ConsentInformationPrivacyOptionsRequirementStatus.Required);

    public string RemoveAdsText
    {
        get
        {
            var price = _details?.GetOneTimePurchaseOfferDetails()?.FormattedPrice;
            return string.IsNullOrEmpty(price) ? "Reklamları kaldır" : $"Reklamları kaldır ({price})";
        }
    }

    /// <summary>Rıza akışını ve Play Billing bağlantısını başlatır (etkinlik oluşurken bir kez).</summary>
    public void Start()
    {
        StartBilling();
        StartConsent();
    }

    // ---- Rıza (UMP) + AdMob ----------------------------------------------------------------

    private void StartConsent()
    {
        // Önceki oturumda rıza alınmışsa reklam hemen başlatılabilir; bilgi yine de güncellenir.
        if (_consent.CanRequestAds()) InitializeAds();

        var builder = new ConsentRequestParameters.Builder().SetTagForUnderAgeOfConsent(false);
#if DEBUG
        // Hata ayıklama derlemelerinde rıza formunu AB bölgesiymiş gibi zorla (1 = EEA); yayın paketinde yok.
        builder.SetConsentDebugSettings(new ConsentDebugSettings.Builder(_activity).SetDebugGeography(1).Build());
#endif
        _consent.RequestConsentInfoUpdate(_activity, builder.Build(),
            new ConsentUpdateSuccess(() => UserMessagingPlatform.LoadAndShowConsentFormIfRequired(_activity,
                new ConsentDismissed(OnConsentResolved))),
            new ConsentUpdateFailure(_ => OnConsentResolved()));
    }

    private void OnConsentResolved()
    {
        if (_disposed) return;
        if (_consent.CanRequestAds()) InitializeAds();
        RaiseAll();
    }

    public void ShowPrivacyOptions() =>
        UserMessagingPlatform.ShowPrivacyOptionsForm(_activity, new ConsentDismissed(OnConsentResolved));

    private void InitializeAds()
    {
        if (_adsInitialized) return;
        _adsInitialized = true;
        // Google, başlatmanın arka plan iş parçacığında yapılmasını önerir.
        Task.Run(() =>
        {
            MobileAds.Initialize(_activity);
            _canRequestAds = true;
            RaiseAll();
        });
    }

    // ---- Play Billing ---------------------------------------------------------------------

    private void StartBilling()
    {
        var pending = PendingPurchasesParams.NewBuilder().EnableOneTimeProducts().Build();
        _billing = BillingClient.NewBuilder(_activity)
            .SetListener(new PurchasesUpdated(OnPurchasesUpdated))
            .EnablePendingPurchases(pending)
            .EnableAutoServiceReconnection()
            .Build();
        _billing.StartConnection(new BillingSetup(OnBillingSetup, () => { _billingReady = false; RaiseAll(); }));
    }

    private void OnBillingSetup(BillingResult result)
    {
        if (_disposed || _billing == null || result.ResponseCode != BillingResponseCode.Ok) return;
        _billingReady = true;

        _billing.QueryPurchases(QueryPurchasesParams.NewBuilder().SetProductType(InApp).Build(),
            new PurchasesQueried((r, purchases) =>
            {
                if (r.ResponseCode == BillingResponseCode.Ok) ApplyOwnedPurchases(purchases, authoritative: true);
            }));

        var product = QueryProductDetailsParams.Product.NewBuilder().SetProductId(ProductId).SetProductType(InApp).Build();
        _billing.QueryProductDetails(QueryProductDetailsParams.NewBuilder().SetProductList(new List<QueryProductDetailsParams.Product> { product }).Build(),
            new ProductDetailsQueried((r, details) =>
            {
                if (r.ResponseCode != BillingResponseCode.Ok) return;
                _details = details?.ProductDetailsList?.FirstOrDefault(d => d.ProductId == ProductId);
                RaiseAll();
            }));
        RaiseAll();
    }

    public Task BuyRemoveAdsAsync()
    {
        if (_billing == null || _details == null) return Task.CompletedTask;
        var detailsParams = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(_details).Build();
        var flow = BillingFlowParams.NewBuilder()
            .SetProductDetailsParamsList(new List<BillingFlowParams.ProductDetailsParams> { detailsParams })
            .Build();
        _billing.LaunchBillingFlow(_activity, flow); // sonuç OnPurchasesUpdated'e gelir
        return Task.CompletedTask;
    }

    private void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
        if (result.ResponseCode == BillingResponseCode.Ok) ApplyOwnedPurchases(purchases, authoritative: false);
        else if (result.ResponseCode == BillingResponseCode.ItemAlreadyOwned) OnBillingSetup(result); // hakkı yeniden sorgula
        // UserCancel ve diğer hatalar: sessizce geçilir, kullanıcı tekrar deneyebilir.
    }

    /// <param name="authoritative">Açılıştaki tam sorgu: listede ürün yoksa hak geri alınır (iade/iptal).</param>
    private void ApplyOwnedPurchases(IList<Purchase>? purchases, bool authoritative)
    {
        bool owned = false;
        foreach (var p in purchases ?? [])
        {
            if (p.Products?.Contains(ProductId) != true) continue;
            if (p.PurchaseState == PurchaseState.Purchased)
            {
                owned = true;
                // Onaylanmayan alım 3 gün içinde Google tarafından iade edilir.
                if (!p.IsAcknowledged)
                    _billing?.AcknowledgePurchase(AcknowledgePurchaseParams.NewBuilder().SetPurchaseToken(p.PurchaseToken).Build(), new AcknowledgeDone(_ => { }));
            }
            // PurchaseState.Pending: ödeme tamamlanana kadar reklam sürer.
        }

        if (owned || authoritative) SetAdsRemoved(owned);
        RaiseAll();
    }

    private void SetAdsRemoved(bool removed)
    {
        _adsRemoved = removed;
        _prefs.Edit()?.PutBoolean(KeyAdsRemoved, removed)?.Apply();
    }

    // ---- Yardımcılar -------------------------------------------------------------------------

    private string? MetaData(string key)
    {
#pragma warning disable CA1422 // PackageInfoFlags aşırı yüklemesi API 33+'ta önerilir; 26+ için bu yeterli.
        var info = _activity.PackageManager?.GetApplicationInfo(_activity.PackageName!, PackageInfoFlags.MetaData);
#pragma warning restore CA1422
        return info?.MetaData?.GetString(key);
    }

    private void RaiseAll()
    {
        void Raise()
        {
            OnPropertyChanged(nameof(ShowAds));
            OnPropertyChanged(nameof(RemoveAdsAvailable));
            OnPropertyChanged(nameof(RemoveAdsText));
            OnPropertyChanged(nameof(PrivacyOptionsRequired));
        }
        if (Dispatcher.UIThread.CheckAccess()) Raise();
        else Dispatcher.UIThread.Post(Raise);
    }

    public void Dispose()
    {
        _disposed = true;
        _billing?.EndConnection();
        _billing = null;
    }
}
