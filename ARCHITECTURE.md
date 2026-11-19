# Architecture

PaletteMaker extracts a color palette from an image (PNG/JPEG/WebP). C# / .NET 10 + Avalonia 12. The UI is **English and Turkish** (follows the system language). Targets: Linux, Windows 11 and Android.

## Solution layout

- `src/PaletteMaker.Core`: UI-independent. `ImageLoader` (decode with SkiaSharp, downscale the long edge to 256 px, flat RGBA), `PaletteExtractor` (the palette algorithm), `ColorMath` (sRGB<->OKLab, CMYK, hex/text formatting), `PaletteOptions`. `ImageLoader` is deliberately in Core so tests can encode and decode real PNG/JPEG/WebP with SkiaSharp without depending on Avalonia.
- `src/PaletteMaker.UI`: shared Avalonia UI (MVVM, CommunityToolkit.Mvvm source generators, compiled bindings; `RootNamespace=PaletteMaker`).
  - Root view is `Views/ShellView` (bottom navigation + 3 tabs, see "Shell and Harmony"). `Views/MainView` (a UserControl) is the "Image" tab. `MainWindow` is only the desktop window hosting `ShellView`.
  - `MainViewModel` does loading and palette computation in the background and recomputes with a 120 ms debounce when a slider changes (the previous computation is cancelled).
  - Clipboard, file picker and drag-and-drop live in `Views/MainView.axaml.cs` (they need a `TopLevel`).
  - `App.ShellVm` (containing `App.MainVm`) is the single view-model tree. Lifetime branches: desktop -> `MainWindow`, Android -> `IActivityApplicationLifetime.MainViewFactory` (both produce a `ShellView`).
- `src/PaletteMaker`: desktop launcher (WinExe): `Program.cs`, `app.manifest`, `Avalonia.Desktop`. The assembly name and publish commands live here.
- `src/PaletteMaker.Android`: Android launcher (`net10.0-android`, min API 26, namespace `PaletteMaker.Droid`): `AndroidApp` (Avalonia setup), `MainActivity` (loads an image received via Share / Open with into `App.MainVm`), vector icon. **It is not in `PaletteMaker.slnx`**; SDK/JDK paths are in the git-ignored `PaletteMaker.Android.csproj.user`.
- `docs/privacy.html` (published via Pages) and `store/` (store listings TR/EN, `data-safety.md`, `graphics/`) are for the Play listing. `store/graphics/` (icon shared; graphics and screenshots under `tr/` and `en/`) is regenerated with `PALETTE_STORE_ASSETS=1 dotnet test tests/PaletteMaker.UI.Tests --filter StoreAssets`.
- `tests/PaletteMaker.Tests`: xunit v2, Core tests (RYB wheel, harmony rules, HEX parsing, ...).
- `tests/PaletteMaker.UI.Tests`: xunit **v3** (`Avalonia.Headless.XUnit` requires it) + real Skia rendering. Opens `MainView` at phone/tablet/desktop sizes, checks the layout and writes PNGs under `bin/.../screenshots/`. This is how to look at the UI without an Android device.

## Palette algorithm (`PaletteExtractor`)

1. 5-bit RGB histogram.
2. Weighted k-means in OKLab (k-means++, fixed seed 12345, k = `max(48, 2 x MaxColors)`, so at most 96; the output is deterministic).
3. Drop clusters whose share is below the noise threshold (JPEG/edge artifacts; threshold 1%, and `0.25/MaxColors` when `MaxColors` > 24, i.e. about 0.52% at 48).
4. Merge clusters closer than a threshold that depends on `MergeStrength` (lightness difference counts with weight 0.5; threshold 0.01-0.14, default strength 0.4 is about 0.062).
5. If more than `MaxColors` (2-48, default 10) remain, keep merging the closest pairs.

Notes:
- `MaxColors` is an upper bound; the palette may be shorter. To get 48 colors from a very colorful image, lower the hue merging to "less" (colors closer than 0.06 merge by default).
- `Extract` takes a `CancellationToken` (k-means checks it every iteration, merging every round); `MainViewModel` really stops the previous computation while a slider is dragged.
- Timing (desktop, 256 px): 24 colors on a photo-like image about 150 ms, 48 colors about 255 ms; worst case (every pixel a different color) about 1.6 s at 48.
- Pixels with alpha below 128 are ignored.
- Threshold constants are the `const`s at the top of the file.

## Shell and Color harmony

- **Shell (`ShellView` + `ShellViewModel`):** bottom navigation bar, left to right **Settings, Image, Harmony** (`ShellTab`). The start tab is Image. The three tab views live permanently in one `Grid` and only `IsVisible` changes (scroll/state and the ad view survive tab changes). **A hidden tab's template is not applied until it is first shown** (in tests a named element inside it cannot be found with `Find`; show the tab first). `ShellVm` is held in `App` (the tab survives an Android activity recreation). Safe-area padding is applied to the shell, so never give `ShellView` a local `Padding`. Navigation items are `RadioButton.nav` (custom template, icons from `StreamGeometry` resources in `App.axaml`, Material Icons / Apache-2.0); `Ctrl+1/2/3` is handled in `ShellView.OnKeyDown`.
- **Common base `PaletteScreenViewModel`** (`MainViewModel` and `HarmonyViewModel` derive from it): swatches (`Swatches`/`SetPalette`), `ValueFormat` (**kept in sync** across the two screens by `ShellViewModel`), `IsExportOpen`, notifications (`ShowToast`), export texts, `HasShares`. **The bottom sheet (`ExportSheet`) and toast (`Toast`) live in the shell** and bind to `ShellVm.ActiveScreen` (on Settings it is `null`, so the sheet is hidden; `FallbackValue=False` is required). The scrim also covers the navigation bar, and an open sheet closes on tab change. Clipboard is `Views/ClipboardHelper` (the system confirmation rule is kept). The named sheet card is `SheetCard` (so the class name does not clash with `ExportSheet`).
- **Back / Esc:** Android Back goes `ShellView.OnBackRequested` -> `ShellViewModel.HandleBack()`: close the open sheet -> if not on the middle tab, return to Image -> otherwise leave it unhandled (the app exits). Esc only closes an open sheet. An image received via Share (`MainActivity`) switches to the Image tab.
- **Harmony core (`Core`, pure, tested):**
  - `Hsv` (hue in **RYB degrees**).
  - `RybWheel`: piecewise linear, strictly monotonic hue mapping; anchors red 0<->0, yellow 120<->60, green 180<->120, blue 240<->240, 360 wraps to 0; `ToRgb`/`FromRgb`.
  - `ColorHarmony.Generate(HarmonyRule, Hsv)` always returns **5 colors, the base in 2nd place and identical to the input**. Rules are a table of `(hue difference, lightness shift)` (shift +: mixes toward white, -: toward black; "Tones" only varies brightness). Tests check invariants (ranges, base, wrap-around, round trip), not table values.
  - `ColorMath.TryParseHex` (`#RGB`/`#RRGGBB`, `#` optional).
  - `PaletteExport.Json(..., includeShare)` and `PaletteCardRenderer.RenderPng(..., showShare)`: a harmony palette has no pixel share, so `Share = 0` and it is not written to the output or card (`SwatchViewModel.ShowShare`).
- **`HarmonyViewModel`:** rule, base hue/saturation/brightness, `Colors` (wheel markers), `HexInput`/`HexInvalid`; recomputes immediately on every change (cheap). `SetBase(r,g,b)` (bridge + HEX). `DragMarker(index, hue, saturation)` rotates the whole harmony so the marker follows the pointer; only the base marker also changes saturation. The file name is `"{rule}-{HEX}"`. The palette button on the Image card (`harmonyAction`) -> `MainViewModel.RequestHarmonyBase` -> the shell switches to the Harmony tab and sets the base.
- **`Controls/ColorWheel`:** no bitmap; a `ConicGradientBrush` (0 degrees at top, clockwise, verified with a screenshot) + a white radial overlay (saturation) + a black overlay (brightness). Dragging uses `PointerPressed` + capture (`PreventGestureRecognition` so the ScrollViewer does not steal touch scrolling; **not tried on a device**); arrow keys also move it (left/right hue, up/down saturation, Shift for a large step). `HarmonyView` layout: two columns when wide, stacked when narrow; column/row values live in the style (a local `Grid.Column` overrides the `.narrow` style).
- **Test helpers:** `VisualFind.Find<T>(root, name)` crosses nested UserControl name scopes (`FindControl` does not); `ShellTestHelpers.OpenShellAsync`. `ShellViewTests` and `HarmonyViewTests` write screenshots to `screenshots/kabuk-*.png`.

## Localization (`src/PaletteMaker.UI/Localization`)

- Strings are in `Strings.resx` (neutral = **English**) and `Strings.tr.resx`. Every non-Turkish language sees English; the language is chosen at startup from `CurrentUICulture` (no runtime switching). In XAML use `{loc:T Key}` (`xmlns:loc="using:PaletteMaker.Localization"`); in code `Loc.Get("Key")` / `Loc.Format("Key", args)`.
- If `"Key.Android"` exists, Android uses it (click -> tap). **This variant must exist in both `.resx` files** (if missing in `tr`, Turkish on Android would show English).
- There is no `Strings` class; `LocalizationTests` catches typos: both files have the same key set, no empty values, equal placeholder counts, every key used in code/XAML exists, no unused keys.
- **Core does not localize:** `ImageLoadException.Kind` (`ImageLoadError`) carries the error and `Message` is neutral English; the UI maps it to localized text with `MainViewModel.ErrorText(kind)`. A new error kind must be added to that `switch` and to both `.resx` files.
- The Android app name is `Resources/values/strings.xml` (EN) + `values-tr/strings.xml` (TR) -> `@string/app_name`. Satellite resource assemblies were verified to ship in the single-file desktop publish and in the Android Release.
- Layout tests run in both TR and EN (`LocalizationTests`).

## Export and sharing

- **Core (pure, tested):**
  - `ColorMath.ToHsl/ToOklch` + `FormatHsl/FormatOklch` (`hsl(355, 78%, 56%)`, `oklch(62.8% 0.258 29.2)`).
  - `PaletteExport`: `HexList`, `CssVariables`, `Json`, `AndroidColorsXml`, `GimpPalette(palette, name, columns)`, `AdobeSwatchExchange` / `PhotoshopSwatches` (`byte[]`). `.ase` is ASEF, a flat color list, RGB float32 0-1. `.aco` is v1 + a named v2 section, channel = value x 257. All big-endian, name = hex, UTF-16BE. Tests round-trip them with small binary parsers.
  - `PaletteCardRenderer.RenderPng` (tile grid PNG; columns from `ColumnsFor`, empty cells light gray).
  - Texts use `InvariantCulture`, line ends are `\n`; the GIMP name is sanitized against line injection. Tests: golden text + a round trip through a small `.gpl` parser + PNG -> `PaletteExtractor` round trip.
- **Resprite (Android)** imports only `.gpl` and its own `.rpl`, via the file picker (Menu > Palette > Import RPL/GPL file); copied clipboard text does not work. `.rpl` is undocumented, so it is not written. `.gpl` saving uses `StorageProvider.SaveFilePickerAsync` **on all platforms** (the system SAF dialog on Android, no permission); MIME `application/octet-stream` (`text/plain` makes Android append `.txt` to the file name). Resprite's real import was not tried on a device. `.ase`/`.aco` saving follows the same path (`SaveFileAsync`); the output was read back with Python against the specification, but **not tried in real Photoshop/Illustrator**.
- **Value format:** `ValueFormat` (Rgb, Cmyk, Hsl, Oklch) is a single `ComboBox` in the header (`FormatBox`); `MainViewModel.FormatIndex` <-> `ValueFormat`. `SwatchViewModel.AccessibleName` is the card's full text.
- **Notifications:** the app's own toast (`ShowToast`) is shown on clipboard copy, but not when `App.SystemShowsClipboardConfirmation` (Android 13+, set by `MainActivity`) is `true`: the system already shows its own "Copied" confirmation and both would double up. Load errors (`ErrorMessage`) appear in `ErrorCard` inside the image area (replacing the hint via `ShowDropHint`).
- **Bottom sheet ("Share and export"):** the `ExportSheet` UserControl, an overlay in `ShellView` (`ExportSheetHost`: scrim + bottom-anchored `SheetCard`; theme-based `SheetBackground`/`ErrorText`/`NavPill` resources are in `App.axaml`); `PaletteScreenViewModel.IsExportOpen`; closed by tapping the scrim, the close button, **Esc** and **Android Back**. Copy items go to the clipboard; `.gpl`/`.ase`/`.aco`/PNG are saved as files. For PNG, if `App.ShareService` (`IShareService`) is assigned (Android) the system share menu is used, otherwise "Save as".
- **Android sharing:** `AndroidShareService` writes the PNG to the cache (`cache/shared`, files older than 1 day are deleted) and hands it over with `FileProvider` (`io.github.demol1shed.palettemaker.fileprovider`, a `<provider>` in both manifests + `Resources/xml/file_paths.xml`) via `ACTION_SEND`; **no extra permission** (the default APK is still permissionless).

## Android flavors and image handling

- **Play flavor (`-p:Store=play`):** the default Android build has no ads and no permissions (also for people building from GitHub). `Store=play` only defines the `PLAY_STORE` constant in `PaletteMaker.Android.csproj`, selects `AndroidManifest.play.xml` (INTERNET, ACCESS_NETWORK_STATE, AdMob metadata), adds the AdMob/UMP/Billing packages, and in Release produces an `aab` + `android-arm64;android-x64`. The `Monetization/` folder is **excluded from the build** unless `Store=play`.
- Shared UI knows only `IMonetization` (+ `NullMonetization`) and `App.AdControlFactory`. On Play, `MainActivity` wires `PlayMonetization` into `App.MainVm.Monetization` and `AdBannerHost` (NativeControlHost + AdView, inline adaptive banner, <= 90 dp, height 0 until loaded) into `AdControlFactory` **before** `base.OnCreate`. The ad sits at the very end of the page (`AdSlot`), under the "Remove ads" / "Ad privacy settings" buttons.
- IDs (AdMob app/banner, product `remove_ads`) are embedded as manifest placeholders and read from PackageManager at runtime (unaffected by trimming). Defaults are Google's TEST IDs; real ones are supplied with `-p:AdMobAppId=... -p:AdMobBannerUnitId=...` or `.csproj.user` (Release warns when a test ID is used).
- AdMob wants AppCompat 1.8.0 instead of the 1.7.1 that Avalonia pins: direct `AppCompat`/`AppCompatResources` 1.8.0 references in the csproj resolve the conflict (an NU1608 warning is expected). The binding libraries' XML docs are empty; read the API with `System.Reflection.MetadataLoadContext` (the UMP namespace is `Xamarin.Google.UserMesssagingPlatform`, three "s").
- **Ads:** whether `AdBannerHost` (NativeControlHost) hides the native view while it is outside the visible tab is **not verified on a device**; if it does not hide it, the ad floats over the Harmony tab and `AdSlot.Content` must be cleared while `MainView` is not visible.
- **In-app review (`Store=play` only):** `PlayReviewPrompt` counts successful palettes (`MainViewModel.PaletteLoaded`, new image loads only) in SharedPreferences. `ReviewPolicy` (pure, `Engagement/`): >= 5 palettes, >= 3 days since first launch, >= 60 days since the last request, not while a sheet is open or busy. It records the time first; errors are swallowed; DEBUG uses `FakeReviewManager`. Google enforces quotas, so the prompt may not show.
- **HEIC/AVIF/GIF/BMP (Android only):** `IImageTranscoder` (`MainViewModel.Transcoder`, `AndroidImageTranscoder` on Android). When Skia cannot open the content (`ImageLoadError.NotAnImage`), it decodes with `BitmapFactory` (size first, rejected if `ImageLoader.MaxPixels` is exceeded; the long edge is reduced to <= ~1024 by powers of 2) and **converts to PNG; the output still goes through the `ImageLoader.Decode` content check**. `EnsureSupportedName` accepts the extra extensions (`.heic .heif .avif .gif .bmp`) only when a decoder exists; extensionless (content://) names already pass. The picker MIME list and share `intent-filter`s were extended. HEIC needs API 28+, AVIF API 31+ (a generic error on older versions). EXIF orientation is lost on HEIC/AVIF/GIF/BMP conversion (the palette is unaffected, the preview may look rotated); for PNG/JPEG/WebP the preview is rotated per EXIF with `ImageLoader.RenderOrientedPreviewPng` (the fast `Bitmap.DecodeToWidth` path is used when the orientation is already correct). Desktop behavior is the same. Not tried on a device.
- The Android Release package is trimmed (SDK default); trimming is off on desktop. A Debug APK relies on fast deployment and will not open when copied by hand: install a Release build on the device.

## Layout and loading (shared with Android)

- **One view, two layouts:** below 600 px width `MainView` adds the `narrow` class to the root, below 520 px height the `short` class; the phone layout is built with `.narrow` styles. Do not write a layout-dependent value (margin, column count, height) locally on an element; keep it in the style, because a local value overrides the `.narrow` style. The palette column count is computed from the width by `MainView.ColumnsFor`.
- **Loading is stream-based:** the Android picker and share give `content://`, and `TryGetLocalPath()` returns `null`. `MainViewModel.LoadAsync(name, openStream)` reads the bytes once with `ImageLoader.ReadBounded`; the palette and preview come from the same bytes. With a local path (desktop) the extension is required; for a stream an extensionless name is accepted (`EnsureSupportedName`); the content check applies on both paths.
- The image area opens on `Tapped`, not `PointerPressed` (so the picker does not open while scrolling the page on touch). In the narrow layout touch targets are at least 40 px (checked by a test).
- Safe area (notch, system bars): Avalonia's `TopLevel.AutoSafeAreaPadding` sets the main view's `Padding` itself at style priority; never give `MainView` a local `Padding`.

## Quality and operations

- **CI (`.github/`):** `ci.yml` runs `dotnet build -c Release` + `dotnet test -c Release --no-build` on every push/PR. `android.yml` (only when Android/UI/Core paths change) installs the workload + SDK and builds the default and `Store=play` flavors in **Debug** (no upload/secrets). `release.yml` on a `v*` tag packages the Windows/Linux single-file publishes, produces `SHA256SUMS.txt` and attaches it to the Release (no APK; Android only via Play). `dependabot.yml` covers NuGet + Actions weekly; **SkiaSharp\*/HarfBuzzSharp\* are ignored** (they must match Avalonia.Skia's version and are aligned by hand), and Avalonia\*/Xamarin.\*/xunit are grouped. Actions cannot run locally: commands were verified locally, real verification is the run after a push (`gh run watch`).
- **Accessibility:** every interactive element has `AutomationProperties.Name` (cards use `SwatchViewModel.AccessibleName`, sliders name + `HelpText`, the preview, the image area); the image area and cards are `Focusable`, activate with Enter/Space, with a `:focus-visible` outline; notifications use `LiveSetting=Polite`, errors `Assertive`. The error color is the theme resource `ErrorText` (light `#B3261E`, dark `#F2B8B5`; `IndianRed` failed AA contrast). `AccessibilityTests` checks names, keyboard and WCAG 4.5:1 contrast. Real TalkBack behavior was not tried on a device.
- **Release and signing:**
  - Outputs are about 100 MB (runtime + Skia embedded). Trimming stays off. The `RemoveNativePdbs` target (csproj) deletes the hundreds of MB of `.pdb`s that NuGet brings from the publish folder. `dist/` and `bin/obj` are git-ignored.
  - `apksigner` reads the store password and the key password from **consecutive lines** when given the same `file:` for both, so `~/.local/share/palettemaker-signing/password.txt` contains the same password on **two lines** (with one line, APK signing fails with "end of file" and `java exited with code 2`; the AAB path is unaffected).

## Constraints and gotchas

- **SkiaSharp must match Avalonia.Skia's version** (currently 3.119.4). The `SkiaSharp` and `SkiaSharp.NativeAssets.Linux` references in Core are pinned to it; when Avalonia is updated, check with `dotnet list src/PaletteMaker package --include-transitive` and align. A managed/native version mismatch crashes at runtime. `NativeAssets.Linux` is in Core so tests also find the native library.
- **Locale (tr-TR):** copied texts (`rgb(...)`, `cmyk(...)`, `#RRGGBB`, percentages) are produced with `InvariantCulture`; extension checks use `OrdinalIgnoreCase` (in tr-TR `"GIF".ToLower()` gives `"gıf"`). Tests force the culture to tr-TR in code to check this.
- **`ImageLoader` memory guard:** file <= 100 MB, resolution <= ~100 MP (read from the header, rejected before decoding); JPEG is decoded at reduced size. The preview opens with `Bitmap.DecodeToWidth(..., 800)`. The old preview bitmap is disposed after the new one is assigned.
- Supported formats are only PNG/JPEG/WebP; besides the extension, `ImageLoader` also verifies the file content (`SKCodec.EncodedFormat`). Skia cannot encode GIF; tests use prebuilt GIF bytes.
- Avalonia 12 APIs: drag-and-drop uses `e.DataTransfer.TryGetFiles()`; the clipboard `SetTextAsync` needs `using Avalonia.Input.Platform;`. On Android `AvaloniaMainActivity` is not generic; the `AppBuilder` customization is in the `AvaloniaAndroidApplication<App>` subclass.
- CMYK is the standard formula without an ICC profile, so the values are approximate.
