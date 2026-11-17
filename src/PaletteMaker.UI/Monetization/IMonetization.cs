using System.ComponentModel;

namespace PaletteMaker.Monetization;

/// <summary>
/// Reklam ve "Reklamları kaldır" satın alımı için ortak yüzey. Masaüstü ve GitHub derlemelerinde
/// <see cref="NullMonetization"/> kullanılır (hiçbir şey görünmez); yalnızca Android'in Play sürümü
/// (<c>-p:Store=play</c>) gerçek bir uygulama sağlar.
/// </summary>
public interface IMonetization : INotifyPropertyChanged
{
    /// <summary>Sayfanın sonundaki reklam alanı gösterilsin mi (rıza alındı, satın alım yok).</summary>
    bool ShowAds { get; }

    /// <summary>"Reklamları kaldır" düğmesi gösterilsin mi (ürün sorgulandı, henüz alınmadı).</summary>
    bool RemoveAdsAvailable { get; }

    /// <summary>Düğme metni, örn. "Reklamları kaldır (₺49,99)".</summary>
    string RemoveAdsText { get; }

    /// <summary>Bölge gereği "Reklam gizlilik ayarları" bağlantısı sunulmalı mı (UMP).</summary>
    bool PrivacyOptionsRequired { get; }

    Task BuyRemoveAdsAsync();

    void ShowPrivacyOptions();
}

/// <summary>Hiçbir şey yapmayan varsayılan: reklam yok, satın alma yok.</summary>
public sealed class NullMonetization : IMonetization
{
    public static NullMonetization Instance { get; } = new();

    private NullMonetization() { }

    public bool ShowAds => false;
    public bool RemoveAdsAvailable => false;
    public string RemoveAdsText => "";
    public bool PrivacyOptionsRequired => false;

    // Değerler hiç değişmediği için olay hiç tetiklenmez.
    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

    public Task BuyRemoveAdsAsync() => Task.CompletedTask;
    public void ShowPrivacyOptions() { }
}
