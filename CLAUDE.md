# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Görselden (PNG/JPEG/WebP) renk paleti çıkaran masaüstü uygulaması. C# / .NET 10 + Avalonia 12, arayüz Türkçe. Linux ve Windows 11 hedeflenir. `~/CLAUDE.md` kuralları geçerlidir: yazma yalnızca bu proje dizininde (ve NuGet önbelleğinde) yapılır.

## Komutlar

```bash
dotnet build                                      # tüm çözüm (PaletteMaker.slnx)
dotnet test                                       # birim testleri
dotnet test --filter "FullyQualifiedName~Cmyk"    # tek test/grup
dotnet run --project src/PaletteMaker -- <resim>  # isteğe bağlı ilk argüman: açılışta yüklenecek dosya

# Tek dosyalı, self-contained yayın (Windows .exe Linux'tan derlenir)
dotnet publish src/PaletteMaker -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o dist/win-x64      # linux-x64 için -r ve -o değişir
```

Çıktılar ~100 MB'dır (çalışma zamanı + Skia gömülü). Trimming kapalı tutulur. `RemoveNativePdbs` hedefi (csproj) yayın klasöründen NuGet'in getirdiği yüzlerce MB'lık `.pdb`'leri siler. `dist/` ve `bin/obj` git dışıdır.

## Mimari

- `src/PaletteMaker.Core` — arayüzden bağımsız. `ImageLoader` (SkiaSharp ile çöz + uzun kenarı 256 px'e küçült → düz RGBA), `PaletteExtractor` (palet algoritması), `ColorMath` (sRGB↔OKLab, CMYK, hex/metin biçimleme), `PaletteOptions`. `ImageLoader` bilerek Core'dadır ki testler Avalonia'ya bağlanmadan SkiaSharp ile gerçek PNG/JPEG/WebP kodlayıp çözebilsin.
- `src/PaletteMaker` — Avalonia uygulaması (WinExe, MVVM, CommunityToolkit.Mvvm kaynak üreticileri, compiled bindings). `MainViewModel` yükleme + palet hesaplamasını arka planda yapar; kaydırıcı değişince 120 ms debounce ile yeniden hesaplar (önceki hesap iptal edilir). Pano, dosya seçici ve sürükle-bırak `Views/MainWindow.axaml.cs` içindedir (TopLevel gerektirdiği için).
- `tests/PaletteMaker.Tests` — xunit.

### Palet algoritması (`PaletteExtractor`)
5 bitlik RGB histogramı → OKLab'da ağırlıklı k-means (k-means++, sabit tohum 12345, k ≤ 48; çıktı deterministik) → payı %1'in altındaki kümeleri at (JPEG/kenar artıkları) → birbirine `MergeStrength`'e bağlı eşikten yakın kümeleri birleştir (açıklık farkı 0,5 ağırlıkla sayılır) → `MaxColors` (2–24, varsayılan 10) aşılıyorsa en yakın çiftleri birleştirmeye devam. `MaxColors` bir üst sınırdır; palet daha kısa çıkabilir. Alfası 128'in altındaki pikseller yok sayılır. Eşik sabitleri dosyanın başındaki `const`'lardadır.

## Dikkat edilecekler

- **SkiaSharp sürümü Avalonia.Skia'nınkiyle aynı olmalı** (şu an 3.119.4). Core'daki `SkiaSharp` ve `SkiaSharp.NativeAssets.Linux` referansları buna sabitlenmiştir; Avalonia güncellenirse `dotnet list src/PaletteMaker package --include-transitive` ile bakıp eşitle. Yönetilen/yerel sürüm uyuşmazlığı çalışma zamanında çöker. `NativeAssets.Linux` Core'da olduğu için testler de yerel kitaplığı bulur.
- **Yerel ayar (tr-TR):** kopyalanan metinler (`rgb(...)`, `cmyk(...)`, `#RRGGBB`, yüzde) `InvariantCulture` ile üretilir; uzantı denetimi `OrdinalIgnoreCase`'dir (tr-TR'de `"GIF".ToLower()` → `"gıf"`). Testler kültürü kodla tr-TR'ye çekerek bunu denetler.
- `ImageLoader` bellek koruması: dosya ≤ 100 MB, çözünürlük ≤ ~100 MP (başlıktan okunur, çözmeden reddedilir); JPEG küçültülmüş boyutta çözülür. Önizleme `Bitmap.DecodeToWidth(…, 800)` ile açılır. Eski önizleme bitmap'i yenisi atandıktan sonra dispose edilir.
- Desteklenen biçimler yalnızca PNG/JPEG/WebP'dir; `ImageLoader` uzantıya ek olarak dosya içeriğini (`SKCodec.EncodedFormat`) de doğrular. Skia GIF kodlayamaz; testte hazır GIF baytları kullanılır.
- Avalonia 12 API'leri: sürükle-bırak `e.DataTransfer.TryGetFiles()`, pano `SetTextAsync` için `using Avalonia.Input.Platform;` gerekir.
- CMYK profilsiz (ICC'siz) standart formüldür, yaklaşık değerdir.
