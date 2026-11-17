namespace PaletteMaker.Engagement;

/// <summary>Play içi değerlendirme isteğinin kalıcı sayaçları (Android'de SharedPreferences'ta tutulur).</summary>
/// <param name="SuccessfulPalettes">Bugüne kadar başarıyla çıkarılan palet sayısı (her görsel yüklemesi bir).</param>
/// <param name="FirstLaunch">İlk açılış zamanı.</param>
/// <param name="LastPrompt">Son değerlendirme isteği zamanı; hiç gösterilmediyse null.</param>
public sealed record ReviewState(int SuccessfulPalettes, DateTimeOffset FirstLaunch, DateTimeOffset? LastPrompt);

/// <summary>
/// Değerlendirme isteğini ne zaman göstereceğimiz. Sakin tutulur: kullanıcı uygulamayı gerçekten kullanmış olmalı
/// (≥ 5 palet, ilk açılıştan ≥ 3 gün) ve son istekten ≥ 60 gün geçmiş olmalı; başka bir akış açıkken gösterilmez.
/// Google ayrıca kendi kotasını uygular; istek gösterilmeyebilir.
/// </summary>
public static class ReviewPolicy
{
    public const int MinPalettes = 5;
    public static readonly TimeSpan MinAge = TimeSpan.FromDays(3);
    public static readonly TimeSpan MinGap = TimeSpan.FromDays(60);

    public static ReviewState CountPalette(ReviewState state) => state with { SuccessfulPalettes = state.SuccessfulPalettes + 1 };

    public static bool ShouldPrompt(ReviewState state, DateTimeOffset now, bool otherFlowOpen) =>
        !otherFlowOpen
        && state.SuccessfulPalettes >= MinPalettes
        && now - state.FirstLaunch >= MinAge
        && (state.LastPrompt is null || now - state.LastPrompt.Value >= MinGap);
}
