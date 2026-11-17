# 🎨 PaletteMaker

**Drop in an image, get its color palette.** A small, fast app for Linux, Windows 11 and Android that pulls the dominant colors out of any PNG, JPEG or WebP image and shows each one as a swatch with a copy-ready hex code, switchable between **RGB** and **CMYK**.

> The interface is available in **English and Turkish** and follows your device (or system) language; any other language shows English.

## Features

- **Drag & drop** an image onto the window, or click to pick a file. You can also pass a path on the command line.
- **On Android:** tap to pick an image, or use *Share* / *Open with* from your gallery to send one straight to the app. No permissions are requested.
- **One adaptive layout:** the same screen rearranges itself for phones, tablets and desktop windows.
- **Palette swatches** with hex code, RGB or CMYK value, and the share of the image each color covers.
- **One-click copy:** click a swatch to copy its hex code, click the value line to copy the RGB/CMYK text, or copy every hex code at once.
- **RGB, CMYK, HSL or OKLCH** for the value shown under each swatch (one drop-down).
- **Share and export:** copy the palette as a HEX list, CSS variables, JSON or Android `colors.xml`; save it as a **GIMP / Resprite `.gpl`** palette file (in Resprite: *Menu ▸ Palette ▸ Import RPL/GPL file*); or share/save a PNG image of the palette.
- **Sane limits for busy images:** choose the maximum number of colors (2–48, default 10). Many near-identical shades of one color are merged into a single average tone, with an adjustable **merge strength**.
- **Deterministic:** the same image always produces the same palette.
- **Single-file builds** for Linux and Windows, no installer and no runtime to install. Android builds as a self-contained APK.

Supported formats: **PNG, JPG/JPEG, WebP.** Files are validated by their actual content, not just the extension. **On Android** the system decoder additionally converts **HEIC/HEIF** (Android 9+), **AVIF** (Android 12+), GIF and BMP images to PNG first, and the result goes through the same validation.

## Download

Grab the latest build from the [Releases](../../releases) page:

| Platform | File | Run |
|---|---|---|
| Windows 11 (x64) | `PaletteMaker-win-x64.zip` | Unzip, run `PaletteMaker.exe` |
| Linux (x64) | `PaletteMaker-linux-x64.tar.gz` | `tar xzf PaletteMaker-linux-x64.tar.gz && ./PaletteMaker` |
| Android 8.0+ (arm64) | `PaletteMaker-android-arm64.apk` | Open the file on your phone and allow installing from this source |

Each release also lists SHA-256 checksums (`SHA256SUMS.txt`). Verify your download with `sha256sum -c SHA256SUMS.txt` (Linux) or `Get-FileHash` (PowerShell).

**Android note:** the APK is signed with a generic debug key, not a release key, so Play Protect may warn about an unknown developer, and a later release signed with a different key cannot update it in place (uninstall first). The app requests no permissions.

**Windows note:** the executable is not code-signed, so SmartScreen may warn you. Click *More info → Run anyway*. On first start the single-file app unpacks its native libraries to a temp folder, which can take a moment.

## How the palette is extracted

1. The image is decoded and shrunk to at most 256 px on its long side. Pixels with alpha below 50% are ignored.
2. Pixels are bucketed into a 5-bit-per-channel RGB histogram.
3. Buckets are clustered with weighted **k-means++ in the OKLab color space**, which is perceptually uniform, so "close" means close to the eye.
4. Clusters covering under 1% of the image are dropped (JPEG noise, edge blending). With a high *max colors* setting (above 24) this limit shrinks so that real colors are not discarded.
5. Clusters closer together than a threshold set by *merge strength* are merged using a pixel-weighted average. Lightness differences count less than hue differences, so light and dark tones of one color collapse together while different colors stay apart.
6. If more than *max colors* remain, the closest pairs keep merging until the limit is met. To see many colors in a busy image, lower the merge strength.

CMYK values use the standard profile-less formula (`K = 1 − max(R,G,B)`), so they are approximate and not suitable for color-accurate print work.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build                                       # build everything
dotnet test                                        # run the unit tests
dotnet run --project src/PaletteMaker              # start the app
dotnet run --project src/PaletteMaker -- photo.jpg # start with an image loaded
```

Self-contained single-file release builds (the Windows `.exe` can be built from Linux):

```bash
dotnet publish src/PaletteMaker -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o dist/win-x64
# use -r linux-x64 -o dist/linux-x64 for Linux
```

### Android

The Android app needs the .NET `android` workload, an Android SDK and a JDK. It is deliberately left out of `PaletteMaker.slnx`, so the commands above keep working without any of them.

```bash
dotnet workload install android

# One-time: download the Android SDK components and a JDK into directories of your choice (absolute paths)
dotnet build src/PaletteMaker.Android -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=$HOME/.local/share/jdk-21 \
  -p:AcceptAndroidSDKLicenses=True

# Build an installable APK (arm64, Android 8.0+)
dotnet publish src/PaletteMaker.Android -c Release -f net10.0-android -o dist/android \
  -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=$HOME/.local/share/jdk-21
```

To avoid repeating the two `-p:` paths, put them in `src/PaletteMaker.Android/PaletteMaker.Android.csproj.user` (git-ignored) as `<AndroidSdkDirectory>` and `<JavaSdkDirectory>` properties.

The default Android build has **no ads, no permissions and no network access**. A separate Google Play variant (`-p:Store=play`, maintainers only) adds an ad banner (Google AdMob, with Google's consent form) and a one-time "remove ads" purchase; it is the only build that needs the internet permission. Its sources live in `src/PaletteMaker.Android/Monetization/` and are not compiled into the default build.

The result is `dist/android/io.github.demol1shed.palettemaker-Signed.apk`, signed with the local debug key: fine for installing on your own phone (`adb install -r <apk>` or copy it over and open it), not for distribution. Use the Release build for that; a Debug APK relies on fast deployment and will not start when copied to a device by hand.

## Project layout

```
src/PaletteMaker.Core/        UI-independent library: image loading, OKLab/CMYK math, palette extraction
src/PaletteMaker.UI/          Shared Avalonia 12 UI (MVVM): the one adaptive screen and its view models
src/PaletteMaker/             Desktop launcher (Linux, Windows)
src/PaletteMaker.Android/     Android launcher: activity, share/open-with handling, icon, Play-only ads/billing (Store=play)
tests/PaletteMaker.Tests/     xUnit tests for the core library
tests/PaletteMaker.UI.Tests/  Headless layout tests; they also write phone/tablet/desktop screenshots
docs/privacy.html             Privacy policy (Turkish + English) for the Play listing
store/                        Play listing texts, data-safety answers and generated graphics
```

Built with C#, .NET 10, [Avalonia UI](https://avaloniaui.net/) and [SkiaSharp](https://github.com/mono/SkiaSharp).

## Security notes

Images are decoded by Skia's native codecs. To limit abuse, files over 100 MB or over about 100 megapixels are rejected before decoding, and only PNG/JPEG/WebP content is accepted (Android converts other formats to PNG with the system decoder first, within the same size limits). The app makes no network connections, and the Android build requests no permissions. Please report vulnerabilities privately through GitHub's *Security → Report a vulnerability* rather than a public issue.

## License

Copyright (C) 2026 demol1shed

PaletteMaker is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public License v3.0** (see [`LICENSE`](LICENSE)). It is distributed in the hope that it will be useful, but **without any warranty**.

You are welcome to build it yourself (see *Build from source*); the default build has no ads. The Google Play version is built from this same code with `-p:Store=play` and additionally bundles Google's proprietary ad, consent and billing libraries, which are not part of this repository and are not covered by the GPL. The app name and icon are not licensed for use by derived works.
