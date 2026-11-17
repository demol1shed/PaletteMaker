# Play Console formu cevapları (Google Play sürümü: AdMob + Play Billing)

Aşağıdaki değerler **Google AdMob'un yayımladığı veri açıklama rehberine** dayanır ve **Play Console'da formu doldururken
Google'ın o günkü rehberiyle karşılaştırılmalıdır** (SDK sürümüyle değişebilir). Hiçbir cevap uydurulmamalı: şüphede olan,
gerçeğe en yakın ve *daha fazla veri toplanıyor* yönündeki seçenek işaretlenir.

## Uygulama içeriği (Policy → App content)

| Bölüm | Cevap |
|---|---|
| Gizlilik politikası URL'si | `https://demol1shed.github.io/PaletteMaker/privacy.html` (Pages açıldıktan sonra) |
| Reklamlar | **Evet, uygulama reklam içeriyor** |
| Uygulama erişimi | Tüm işlevler özel erişim olmadan kullanılabilir |
| Reklam kimliği (Advertising ID) | **Evet, kullanıyor** · amaç: *Reklam veya pazarlama* (+ *Dolandırıcılık önleme*) |
| Hedef kitle | **13+** (ya da 18+); çocuklara yönelik **değil**, Families programına başvurulmaz |
| İçerik derecelendirme (IARC) | Şiddet, cinsellik, kumar, uyuşturucu, kullanıcı etkileşimi/paylaşım, konum paylaşımı: **hepsi Hayır**; dijital satın alma: **Evet**; reklam: **Evet** |
| Haber uygulaması / COVID / Devlet / Finans / Sağlık | Hayır |
| Veri güvenliği: veri toplama ve paylaşım | **Evet** (aşağıda) |
| Veri şifreleme (aktarımda) | Evet |
| Silme talebi | Hesap yok; uygulama kullanıcı verisi saklamıyor (reklam verisi Google'da) |

## Veri güvenliği: toplanan / paylaşılan veri türleri

Hepsi **AdMob SDK'sı** tarafından toplanır ve **Google ile paylaşılır** (hizmet sağlayıcı olarak *değil*, reklam amaçlı paylaşım).
**İsteğe bağlı değil** (reklam bu verilerle çalışır; rıza formu AB/BK'da UMP ile alınır).

| Kategori → tür | Toplanıyor | Paylaşılıyor | Amaç |
|---|---|---|---|
| Konum → Yaklaşık konum (IP'den) | Evet | Evet | Reklam, dolandırıcılık önleme |
| Cihaz veya diğer kimlikler → Cihaz/diğer kimlikler (reklam kimliği) | Evet | Evet | Reklam, dolandırıcılık önleme, analiz |
| Uygulama etkinliği → Uygulama etkileşimleri (reklam etkileşimi) | Evet | Evet | Reklam, analiz |
| Uygulama bilgileri ve performansı → Çökme günlükleri, tanılama, diğer performans verileri | Evet | Evet | Analiz, dolandırıcılık önleme |

**Toplanmayan** (uygulama kendisi hiçbirini almaz): kişisel bilgiler (ad, e-posta, adres), finansal bilgiler (ödeme Google Play'de),
sağlık, mesajlar, **fotoğraf ve videolar** (görseller yalnızca cihazda işlenir), ses, dosyalar, takvim, kişiler, web gezinme geçmişi.

## Uygulama içi ürün

| Alan | Değer |
|---|---|
| Ürün kimliği | `remove_ads` (**Yönetilen ürün**, tüketilmeyen/tek seferlik) |
| Başlık (TR) | Reklamları kaldır |
| Açıklama (TR) | Sayfa sonundaki reklamı kalıcı olarak kapatır ve geliştiriciyi destekler. |
| Başlık (EN) | Remove ads |
| Açıklama (EN) | Permanently turns off the ad at the end of the page and supports the developer. |
| Fiyat | Console'da sen belirlersin (TRY için yerel fiyat) |

## Play'de görecek manifest izinleri (Play derlemesi)

`INTERNET`, `ACCESS_NETWORK_STATE`, `AD_ID`, `BILLING` ve AdMob SDK'sının WorkManager bileşeninden gelen `WAKE_LOCK`,
`RECEIVE_BOOT_COMPLETED`, `FOREGROUND_SERVICE`, `ACCESS_ADSERVICES_*` (Privacy Sandbox). Hepsi AdMob'lu uygulamalarda olağandır.
Uygulama **hiçbir ön plan hizmeti türü ya da özel izin beyanı gerektiren özellik kullanmaz**.
