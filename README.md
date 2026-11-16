# 🎨 PaletteMaker

**Drop in an image, get its color palette.** A small, fast desktop app for Linux and Windows 11 that pulls the dominant colors out of any PNG, JPEG or WebP image and shows each one as a swatch with a copy-ready hex code, switchable between **RGB** and **CMYK**.

> The app's interface is currently in Turkish.

## Features

- **Drag & drop** an image onto the window, or click to pick a file. You can also pass a path on the command line.
- **Palette swatches** with hex code, RGB or CMYK value, and the share of the image each color covers.
- **One-click copy:** click a swatch to copy its hex code, click the value line to copy the RGB/CMYK text, or copy every hex code at once.
- **RGB ⇄ CMYK toggle** for the value shown under each swatch.
- **Sane limits for busy images:** choose the maximum number of colors (2–24, default 10). Many near-identical shades of one color are merged into a single average tone, with an adjustable **merge strength**.
- **Deterministic:** the same image always produces the same palette.
- **Single-file builds** for Linux and Windows, no installer and no runtime to install.

Supported formats: **PNG, JPG/JPEG, WebP.** Files are validated by their actual content, not just the extension.

## Download

Grab the latest build from the [Releases](../../releases) page:

| Platform | File | Run |
|---|---|---|
| Windows 11 (x64) | `PaletteMaker-win-x64.zip` | Unzip, run `PaletteMaker.exe` |
| Linux (x64) | `PaletteMaker-linux-x64.tar.gz` | `tar xzf PaletteMaker-linux-x64.tar.gz && ./PaletteMaker` |

Each release also lists SHA-256 checksums (`SHA256SUMS.txt`). Verify your download with `sha256sum -c SHA256SUMS.txt` (Linux) or `Get-FileHash` (PowerShell).

**Windows note:** the executable is not code-signed, so SmartScreen may warn you. Click *More info → Run anyway*. On first start the single-file app unpacks its native libraries to a temp folder, which can take a moment.

## How the palette is extracted

1. The image is decoded and shrunk to at most 256 px on its long side. Pixels with alpha below 50% are ignored.
2. Pixels are bucketed into a 5-bit-per-channel RGB histogram.
3. Buckets are clustered with weighted **k-means++ in the OKLab color space**, which is perceptually uniform, so "close" means close to the eye.
4. Clusters covering under 1% of the image are dropped (JPEG noise, edge blending).
5. Clusters closer together than a threshold set by *merge strength* are merged using a pixel-weighted average. Lightness differences count less than hue differences, so light and dark tones of one color collapse together while different colors stay apart.
6. If more than *max colors* remain, the closest pairs keep merging until the limit is met.

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

## Project layout

```
src/PaletteMaker.Core/   UI-independent library: image loading, OKLab/CMYK math, palette extraction
src/PaletteMaker/        Avalonia 12 desktop app (MVVM)
tests/PaletteMaker.Tests/  xUnit tests
```

Built with C#, .NET 10, [Avalonia UI](https://avaloniaui.net/) and [SkiaSharp](https://github.com/mono/SkiaSharp).

## Security notes

Images are decoded by Skia's native codecs. To limit abuse, files over 100 MB or over about 100 megapixels are rejected before decoding, and only PNG/JPEG/WebP content is accepted. The app makes no network connections. Please report vulnerabilities privately through GitHub's *Security → Report a vulnerability* rather than a public issue.

## License

No license has been chosen yet. Until one is added, all rights are reserved by the author.
