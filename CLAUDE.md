# CLAUDE.md

Guidance for Claude Code (claude.ai/code) in this repository.

Architecture, layer responsibilities, algorithms and architectural constraints live in `ARCHITECTURE.md`. Read it before changing code and do not violate it.

Stack: C# / .NET 10 + Avalonia 12; Linux, Windows 11 and Android. Write only inside this project directory (and the NuGet cache), per `~/CLAUDE.md`. The one exception is the user-approved Android toolchain: the `android` workload in `~/.dotnet`, the Android SDK in `~/Android/Sdk`, the JDK in `~/.local/share/jdk-21`.

## Build / test / run

```bash
dotnet build                                      # whole solution (PaletteMaker.slnx; Android excluded)
dotnet test                                       # unit + UI layout tests
dotnet test --filter "FullyQualifiedName~Cmyk"    # a single test/group
dotnet run --project src/PaletteMaker -- <image>  # optional first argument: file to load at startup

# Single-file, self-contained publish (Windows .exe can be built from Linux)
dotnet publish src/PaletteMaker -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o dist/win-x64      # for linux-x64 change -r and -o

# Android APK (arm64, signed with the debug key; only for installing on your own device)
dotnet publish src/PaletteMaker.Android -c Release -f net10.0-android -o dist/android
```

- Outputs are ~100 MB. Keep trimming off on desktop. `dist/` and `bin/obj` are git-ignored.
- Install Release builds on a device; a Debug APK does not open when copied by hand.

## Test rules

- Core tests are xunit v2 (`tests/PaletteMaker.Tests`); UI tests are xunit **v3** (`tests/PaletteMaker.UI.Tests`).
- A test that creates an Avalonia object (e.g. `SwatchViewModel` -> `SolidColorBrush`) must be `[AvaloniaFact]` / `[AvaloniaTheory]`. A plain `[Fact]` breaks later headless tests with "different thread owns".
- A hidden tab's template is not applied until it is shown; show the tab before `Find` in a test.
- Tests check invariants, not table values. New behavior needs a new test.
- Culture-sensitive code is tested under tr-TR. `LocalizationTests` must stay green (same key set in both `.resx` files, no unused or missing keys).
- Verify UI changes with the headless screenshots (`bin/.../screenshots/`) instead of guessing.

## Code style

- MVVM with CommunityToolkit.Mvvm source generators and compiled bindings.
- UI strings go through `{loc:T Key}` in XAML and `Loc.Get` / `Loc.Format` in code; never hard-code user-visible text. A `"Key.Android"` variant must exist in **both** `Strings.resx` and `Strings.tr.resx`.
- Produce copied or exported text with `InvariantCulture`; compare extensions with `OrdinalIgnoreCase`.
- Core never localizes: a new `ImageLoadError` kind needs a case in `MainViewModel.ErrorText` and both `.resx` files.
- Layout-dependent values (margin, column count, height) go in styles, never as local values on an element (they override `.narrow`).
- Every interactive element needs `AutomationProperties.Name`.

## Red lines

- Keep SkiaSharp at the same version as Avalonia.Skia (currently 3.119.4); a managed/native mismatch crashes at runtime.
- Never give `ShellView` or `MainView` a local `Padding` (safe-area padding is applied by the framework/shell).
- The default Android build must stay ad-free and permissionless; Play-only code stays behind `Store=play`.
- Never commit or print signing keys or passwords (`~/.local/share/palettemaker-signing/`); AdMob IDs in the repo are Google's TEST IDs.
- Do not edit `PaletteMaker.Android.csproj.user` or add the Android project to `PaletteMaker.slnx`.
- Anything marked "not tried on a device" in `ARCHITECTURE.md` must not be claimed as verified.

## Git workflow

Keep the commit history clean. Prefer small, descriptive commits at logical points over one huge commit: one commit is one logical change, and refactors, behavior changes and tests go in separate commits, so a reviewer can digest the code step by step.

- Never commit or push directly to `main`. Work on a topic branch named `<type>/<short-description>` (e.g. `docs/architecture-split`), push it, open a PR with `gh pr create` (a Summary and a Test plan), and merge through the PR once CI passes. Delete the branch after merging.
- Merge with rebase-merge so the history stays linear and the separate commits survive; do not squash unrelated commits together.
- Commit messages and PR descriptions are written in English.

### Commit messages

Conventional Commits 1.0, written in English:

- Subject: `<type>(<optional-scope>): <imperative summary>`. Imperative mood ("add", not "added"), lowercase after the colon, at most 72 chars, no trailing period. The scope names the module or area (e.g. `ui`, `parser`).
- Types: `feat`, `fix`, `perf`, `refactor`, `docs`, `test`, `build`, `ci`, `chore`, `style`, `revert`.
- Body (after a blank line, wrapped at ~72 cols): explains what and why, not how. Mandatory beyond trivial one-liners.
- Breaking change: `!` after the type/scope plus a `BREAKING CHANGE:` footer. Reference issues in footers (`Fixes #123`, `Refs #123`).
- Every commit is one logical change, builds, and passes the tests. Do not leave WIP or fixup commits in a PR; amend them before pushing.
- Never commit secrets, build output, or unrelated formatting churn.
- The PR title follows the same format; the PR body has a Summary (what and why) and a Test plan.

### Releases

- Versioning: SemVer 2.0 `MAJOR.MINOR.PATCH`. A `feat` bumps MINOR, a `fix`/`perf` bumps PATCH, a breaking change (`!` / `BREAKING CHANGE:`) bumps MAJOR. While below 1.0.0, breaking changes bump MINOR. Pre-releases use `-rc.1`, `-beta.1` suffixes.
- Tags: annotated and prefixed with `v` (`git tag -a v1.2.0 -m "v1.2.0"`), created only on the default branch after the release PR is merged. Never move or delete a published tag; ship a new patch instead.
- Changelog: keep `CHANGELOG.md` in Keep a Changelog format (`Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`, newest first, `Unreleased` on top), derived from the Conventional Commits since the last tag.
- Release PR: a dedicated PR titled `chore(release): vX.Y.Z` that only bumps the version (where the project defines one) and moves `Unreleased` into the new version section.
- GitHub Release: `gh release create vX.Y.Z --title "vX.Y.Z" --notes-file <notes>`, notes taken from the changelog section; mark pre-releases with `--prerelease`.
- Tagging, pushing tags and publishing releases are outward-facing: do them only when the user explicitly asks.
