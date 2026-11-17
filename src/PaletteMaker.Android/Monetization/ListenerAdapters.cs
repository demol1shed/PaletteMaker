using Android.BillingClient.Api;
using Google.Android.Gms.Ads;
using Xamarin.Google.UserMesssagingPlatform;

namespace PaletteMaker.Droid.Monetization;

// Java dinleyici arayüzlerini C# temsilcilerine bağlayan küçük bağdaştırıcılar.

internal sealed class ConsentUpdateSuccess(Action onSuccess) : Java.Lang.Object, IConsentInformationOnConsentInfoUpdateSuccessListener
{
    public void OnConsentInfoUpdateSuccess() => onSuccess();
}

internal sealed class ConsentUpdateFailure(Action<string?> onFailure) : Java.Lang.Object, IConsentInformationOnConsentInfoUpdateFailureListener
{
    public void OnConsentInfoUpdateFailure(FormError error) => onFailure(error?.Message);
}

internal sealed class ConsentDismissed(Action onDismissed) : Java.Lang.Object, IConsentFormOnConsentFormDismissedListener
{
    public void OnConsentFormDismissed(FormError? error) => onDismissed();
}

internal sealed class BillingSetup(Action<BillingResult> onSetup, Action onDisconnected) : Java.Lang.Object, IBillingClientStateListener
{
    public void OnBillingSetupFinished(BillingResult result) => onSetup(result);
    public void OnBillingServiceDisconnected() => onDisconnected();
}

internal sealed class PurchasesUpdated(Action<BillingResult, IList<Purchase>?> handler) : Java.Lang.Object, IPurchasesUpdatedListener
{
    public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases) => handler(result, purchases);
}

internal sealed class PurchasesQueried(Action<BillingResult, IList<Purchase>?> handler) : Java.Lang.Object, IPurchasesResponseListener
{
    public void OnQueryPurchasesResponse(BillingResult result, IList<Purchase>? purchases) => handler(result, purchases);
}

internal sealed class ProductDetailsQueried(Action<BillingResult, QueryProductDetailsResult?> handler) : Java.Lang.Object, IProductDetailsResponseListener
{
    public void OnProductDetailsResponse(BillingResult result, QueryProductDetailsResult? details) => handler(result, details);
}

internal sealed class AcknowledgeDone(Action<BillingResult> handler) : Java.Lang.Object, IAcknowledgePurchaseResponseListener
{
    public void OnAcknowledgePurchaseResponse(BillingResult result) => handler(result);
}

internal sealed class BannerListener(Action onLoaded, Action<string?> onFailed) : AdListener
{
    public override void OnAdLoaded() => onLoaded();
    public override void OnAdFailedToLoad(LoadAdError error) => onFailed(error?.Message);
}
