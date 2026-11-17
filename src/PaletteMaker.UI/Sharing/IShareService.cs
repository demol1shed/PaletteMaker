namespace PaletteMaker.Sharing;

/// <summary>
/// Platformun paylaşım menüsü (Android'de sistem "Paylaş" sayfası). Atanmamışsa (masaüstü) arayüz PNG'yi
/// "Farklı kaydet" iletişim kutusuyla kaydeder. Atama <c>App.ShareService</c> üzerinden, görünüm oluşmadan önce yapılır.
/// </summary>
public interface IShareService
{
    /// <summary>PNG'yi paylaşım menüsüyle gönderir. Başarıyla açıldıysa true.</summary>
    Task<bool> SharePngAsync(byte[] png, string fileName, string title);
}
