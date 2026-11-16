# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Görselden (PNG/JPEG/WebP) renk paleti çıkaran uygulama. C# / .NET 10 + Avalonia 12, arayüz Türkçe. Linux, Windows 11 ve Android hedeflenir. `~/CLAUDE.md` kuralları geçerlidir: yazma yalnızca bu proje dizininde (ve NuGet önbelleğinde) yapılır. Tek istisna kullanıcının onayladığı Android araç zinciridir: `android` workload'ı `~/.dotnet`, Android SDK `~/Android/Sdk`, JDK `~/.local/share/jdk-21`.

## Komutlar

```bash
dotnet build                                      # tüm çözüm (PaletteMaker.slnx; Android hariç)
dotnet test                                       # birim + arayüz yerleşim testleri
dotnet test --filter "FullyQualifiedName~Cmyk"    # tek test/grup
dotnet run --project src/PaletteMaker -- <resim>  # isteğe bağlı ilk argüman: açılışta yüklenecek dosya

# Tek dosyalı, self-contained yayın (Windows .exe Linux'tan derlenir)
dotnet publish src/PaletteMaker -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o dist/win-x64      # linux-x64 için -r ve -o değişir

# Android APK (arm64, debug anahtarıyla imzalı; yalnızca kendi cihazına kurmak için)
dotnet publish src/PaletteMaker.Android -c Release -f net10.0-android -o dist/android
```

Çıktılar ~100 MB'dır (çalışma zamanı + Skia gömülü). Trimming kapalı tutulur. `RemoveNativePdbs` hedefi (csproj) yayın klasöründen NuGet'in getirdiği yüzlerce MB'lık `.pdb`'leri siler. `dist/` ve `bin/obj` git dışıdır.

## Mimari

- `src/PaletteMaker.Core` — arayüzden bağımsız. `ImageLoader` (SkiaSharp ile çöz + uzun kenarı 256 px'e küçült → düz RGBA), `PaletteExtractor` (palet algoritması), `ColorMath` (sRGB↔OKLab, CMYK, hex/metin biçimleme), `PaletteOptions`. `ImageLoader` bilerek Core'dadır ki testler Avalonia'ya bağlanmadan SkiaSharp ile gerçek PNG/JPEG/WebP kodlayıp çözebilsin.
- `src/PaletteMaker.UI` — paylaşılan Avalonia arayüzü (MVVM, CommunityToolkit.Mvvm kaynak üreticileri, compiled bindings; `RootNamespace=PaletteMaker`). Tek ekran `Views/MainView` (UserControl); `MainWindow` yalnızca onu barındıran masaüstü kabuğudur. `MainViewModel` yükleme + palet hesaplamasını arka planda yapar; kaydırıcı değişince 120 ms debounce ile yeniden hesaplar (önceki hesap iptal edilir). Pano, dosya seçici ve sürükle-bırak `Views/MainView.axaml.cs` içindedir (TopLevel gerektirdiği için). `App.MainVm` tek görünüm modelidir; yaşam döngüsü dalları: masaüstü → `MainWindow`, Android → `IActivityApplicationLifetime.MainViewFactory`.
- `src/PaletteMaker` — masaüstü başlatıcı (WinExe): `Program.cs`, `app.manifest`, `Avalonia.Desktop`. Derleme adı ve yayın komutları buradadır.
- `src/PaletteMaker.Android` — Android başlatıcı (`net10.0-android`, min. API 26, ad alanı `PaletteMaker.Droid`): `AndroidApp` (Avalonia kurulumu), `MainActivity` (Paylaş / Birlikte aç ile gelen görseli `App.MainVm`'e yükler), vektör simge. **`PaletteMaker.slnx`'te yoktur**; SDK/JDK yolları git dışı `PaletteMaker.Android.csproj.user` dosyasındadır.
- `tests/PaletteMaker.Tests` — xunit v2, Core testleri.
- `tests/PaletteMaker.UI.Tests` — xunit **v3** (`Avalonia.Headless.XUnit` bunu ister) + gerçek Skia çizimi. `MainView`'i telefon/tablet/masaüstü boyutlarında açar, yerleşimi denetler ve `bin/…/screenshots/` altına PNG yazar; Android cihazı olmadan arayüze bakmanın yolu budur.

### Palet algoritması (`PaletteExtractor`)
5 bitlik RGB histogramı → OKLab'da ağırlıklı k-means (k-means++, sabit tohum 12345, k ≤ 48; çıktı deterministik) → payı %1'in altındaki kümeleri at (JPEG/kenar artıkları) → birbirine `MergeStrength`'e bağlı eşikten yakın kümeleri birleştir (açıklık farkı 0,5 ağırlıkla sayılır) → `MaxColors` (2–24, varsayılan 10) aşılıyorsa en yakın çiftleri birleştirmeye devam. `MaxColors` bir üst sınırdır; palet daha kısa çıkabilir. Alfası 128'in altındaki pikseller yok sayılır. Eşik sabitleri dosyanın başındaki `const`'lardadır.

### Yerleşim ve yükleme (Android ile ortak)
- **Tek görünüm, iki düzen:** genişlik 600 px'in altına inince `MainView` köke `narrow`, yükseklik 520 px'in altına inince `short` sınıfını ekler; telefon düzeni `.narrow` stilleriyle kurulur. Düzene göre değişen bir değeri (kenar boşluğu, sütun sayısı, yükseklik) öğeye yerel olarak yazma, stilde tut: yerel değer `.narrow` stilini ezer. Palet sütun sayısı `MainView.ColumnsFor` ile genişlikten hesaplanır.
- **Yükleme akış tabanlıdır:** Android seçicisi ve paylaşım `content://` verir, `TryGetLocalPath()` `null` döner. `MainViewModel.LoadAsync(ad, akışAç)` baytları `ImageLoader.ReadBounded` ile bir kez okur; palet ve önizleme aynı baytlardan üretilir. Yerel yol varsa (masaüstü) uzantı zorunludur, akışta uzantısız ad kabul edilir (`EnsureSupportedName`); içerik denetimi her iki yolda da geçerlidir.
- Görsel alanı `Tapped` ile açılır, `PointerPressed` ile değil (dokunmatikte sayfa kaydırılırken seçici açılmasın). Dar düzende dokunma hedefleri en az 40 px'tir (testle denetlenir).
- Güvenli alan (çentik, sistem çubukları): Avalonia `TopLevel.AutoSafeAreaPadding` ile ana görünümün `Padding`'ini stil önceliğinde kendisi ayarlar; `MainView`'e yerel `Padding` verme.
- Android Release paketi kırpılır (SDK varsayılanı); masaüstünde trimming kapalıdır. Debug APK hızlı dağıtıma dayanır, elle kopyalanınca açılmaz: cihaza Release derlemesi kurulur.

## Dikkat edilecekler

- **SkiaSharp sürümü Avalonia.Skia'nınkiyle aynı olmalı** (şu an 3.119.4). Core'daki `SkiaSharp` ve `SkiaSharp.NativeAssets.Linux` referansları buna sabitlenmiştir; Avalonia güncellenirse `dotnet list src/PaletteMaker package --include-transitive` ile bakıp eşitle. Yönetilen/yerel sürüm uyuşmazlığı çalışma zamanında çöker. `NativeAssets.Linux` Core'da olduğu için testler de yerel kitaplığı bulur.
- **Yerel ayar (tr-TR):** kopyalanan metinler (`rgb(...)`, `cmyk(...)`, `#RRGGBB`, yüzde) `InvariantCulture` ile üretilir; uzantı denetimi `OrdinalIgnoreCase`'dir (tr-TR'de `"GIF".ToLower()` → `"gıf"`). Testler kültürü kodla tr-TR'ye çekerek bunu denetler.
- `ImageLoader` bellek koruması: dosya ≤ 100 MB, çözünürlük ≤ ~100 MP (başlıktan okunur, çözmeden reddedilir); JPEG küçültülmüş boyutta çözülür. Önizleme `Bitmap.DecodeToWidth(…, 800)` ile açılır. Eski önizleme bitmap'i yenisi atandıktan sonra dispose edilir.
- Desteklenen biçimler yalnızca PNG/JPEG/WebP'dir; `ImageLoader` uzantıya ek olarak dosya içeriğini (`SKCodec.EncodedFormat`) de doğrular. Skia GIF kodlayamaz; testte hazır GIF baytları kullanılır.
- Avalonia 12 API'leri: sürükle-bırak `e.DataTransfer.TryGetFiles()`, pano `SetTextAsync` için `using Avalonia.Input.Platform;` gerekir. Android'de `AvaloniaMainActivity` jenerik değildir; `AppBuilder` özelleştirmesi `AvaloniaAndroidApplication<App>` alt sınıfındadır.
- CMYK profilsiz (ICC'siz) standart formüldür, yaklaşık değerdir.
