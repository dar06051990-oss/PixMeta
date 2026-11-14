# PixMeta v1.0.0 — Windows image viewer

Picasa-inspired image viewer with AI-generation metadata.

**Stable release v1.0.0:** self-contained Windows x64 installer with the approved PixMeta icon, shortcut creation and uninstall support. The previous RC3 was tested on the owner's Windows computer; this release changes only installer captions, versions and documentation, not the viewer logic. Get the installation EXE from [GitHub Releases](https://github.com/dar06051990-oss/PixMeta/releases/latest). The viewer UI itself still uses English; only the installer has been localized.

## v0.4
- image-first dark overlay layout
- collapsible AI Info panel (`I`)
- Picasa-like filmstrip of nearby images
- Fit / 1:1 / zoom controls
- keyboard: Left/Right, Esc, I, F11, Space for animated images
- fullscreen mode
- GIF/animated image controls retained
- Forge/A1111, ComfyUI and Grok metadata support retained
- actual file-format detection retained

Run:
```
dotnet restore
dotnet run
```

## v0.4.4 performance pass
- Image zoom/pan now uses RenderTransform instead of resizing the Image/layout on every wheel tick.
- Static image preview decoding moved off the UI thread.
- Small LRU preview cache preloads neighboring images (±2) for faster navigation.
- Full-resolution decode is deferred until near 100% zoom / 1:1.
- Image stage no longer uses ScrollViewer, preventing wheel/page-scroll contention.

## v0.4.5
- Filmstrip thumbnails decode asynchronously; navigation no longer blocks while 11 thumbnails are regenerated.
- Added bounded thumbnail LRU cache (32 items).
- Full-resolution bitmap is explicitly released when leaving the current image instead of lingering during navigation.
- Stale thumbnail jobs cannot repaint a newer filmstrip generation.


## v0.4.6
- Removed the 1:1 button.
- Preview prefetch now starts immediately after the current preview is shown, before metadata/filmstrip work.
- Direction-aware look-ahead: up to 8 images ahead and 3 behind.
- Preview cache increased to 14 screen-sized images.
- Avalonia preview bitmap construction is moved off the UI thread to reduce navigation stalls.


## v0.4.7
- Two-stage static image loading: fast 1100px navigation preview first, higher-quality preview deferred.
- Neighbor prefetch now prepares fast previews and never wraps around folder ends.
- Previous/Next stop at the first/last image instead of cycling.

## v0.4.11 performance experiment
- Based on v0.4.9 (v0.4.10 resource-thrashing changes discarded).
- Picasa-inspired deferred decode: rapid navigation is debounced for 85 ms, so intermediate images are not decoded at all.
- Removed continuous neighbor-prefetch worker: it could saturate CPU/disk and make foreground navigation slower.
- Uses an already decoded preview/filmstrip thumbnail immediately when available, then replaces it with the screen preview after navigation settles.
- Higher-quality preview is deferred 350 ms; full-resolution decode is reserved for very deep zoom (150%+).
- Added Run PixMeta.bat.

## Windows EXE
Run `Build EXE.bat` once. It publishes a self-contained Windows x64 single-file build to `publish\\win-x64\\PixMeta.exe`.
PixMeta already accepts an image path as its first command-line argument, so the published EXE can be selected from Windows "Open with".

## v0.5.2
- Picasa-style desktop overlay is now the default when an image is opened from Explorer/file associations.
- Borderless fullscreen translucent viewer keeps the desktop visible behind the image.
- The top bar, controls and filmstrip use translucent surfaces in overlay mode.
- The top-right mode button switches between Picasa overlay and the normal windowed viewer.
- Esc closes the Picasa overlay (or closes AI Info first).
- Image loading, zoom-to-cursor and metadata pipeline are unchanged from v0.4.14.


## v0.5.2
- Zoom-to-cursor now samples the live pointer position in stable stage coordinates on every wheel tick.
- Larger image workspace: compact top bar, overlay controls, reduced filmstrip height and image margins.

## v0.5.13 (source patch, not precompiled)

- Correctly follows the KSampler positive and negative conditioning graph, including custom `PromptLora` nodes whose text input is named `prompt`.
- Extracts KSampler seed/steps/sampler/scheduler/CFG/denoise, checkpoint model, and actual image size from native ComfyUI PNG.
- Fixes A1111 parameter-line detection when `Size:` precedes `Steps:`, preventing resolution from appearing in negative prompt.
- Parses Preview Plus JPG and WEBP EXIF UserComment JSON and decodes v0.1's nonstandard `UNICODE\0` + UTF-8 encoding.
- Reads full execution graph from JSON ON JPEG/WEBP to recover prompt if the old saver omitted it in its Forge-style data.
- Layout, zoom, navigation, filmstrip and other UI features are unchanged.

### Build and install

This archive contains the C# sources. On Windows, run `Build EXE.bat` from the `PixMeta` folder with .NET 8 SDK installed. It writes `publish\win-x64\PixMeta.exe`. Back up your installed PixMeta EXE before replacing it.

NOTE: This patch was inspected and matched to the user's 2026-11-05 metadata samples; it has **not** been built or GUI-tested on Windows in this environment (the .NET SDK isn't available here).

An existing JPG or PNG saved by Preview Plus v0.1 with JSON OFF lacks the positive prompt entirely; no metadata reader can reconstruct it without the workflow or another source. Generate a new file with Preview Plus v0.2 for that scenario.

## v0.5.15 (2026-11-06) — Preview Plus JPG source
- Reads the standard EXIF Software (0x0131) field. New images from ComfyUI-PreviewPlus v0.5 are labelled `ComfyUI / Preview Plus` even with JPG workflow JSON OFF.
- Does not change prompt, sampler, metadata parsing or image UI.
- Existing v0.4 Preview Plus JPGs without a Software tag cannot be reliably identified, and may still say A1111 / Forge; regenerate them with Preview Plus v0.5 for the correct label.
- Source only: use `Build EXE.bat` on Windows. This patch was not compiled with the .NET SDK in this environment.


## v0.5.17 changes (2026-11-06)
- Parse `preview_plus_parameters` in PNG even if there is no standard `parameters` chunk (required for Preview Plus v0.6, PNG JSON OFF).
- Mark embedded JPG workflow when Preview Plus v0.6 stores it as JSON inside EXIF ImageDescription; generation information still comes from EXIF UserComment.
- Source labeling remains `ComfyUI / Preview Plus` with EXIF Software marker.
- No UI/layout changes. .NET Windows build is not available in the test environment; build locally with `Build EXE.bat`.

## v0.5.17 change — WEBP from Preview Plus v0.7

- Read EXIF Make/Model text fields from WEBP. The native ComfyUI `workflow:{JSON}` tag in Make is recognized as `Workflow: Embedded`.
- Preview Plus WebP now writes the EXIF Software tag, so PixMeta correctly labels both JSON modes as `ComfyUI / Preview Plus` rather than `A1111 / Forge`.
- No UI or JPEG/PNG handling changed.

## v0.5.19 RC1 (2026-11-15) — Finalization candidate

Based on the last published source ZIP (v0.5.17), plus the v0.5.18 clipboard fix previously verified on Windows.

- Persist copied Prompt / Negative / Parameters / generation data after exit via `Clipboard.FlushAsync()`.
- Dispose decoded frames between animated GIF/WebP frames instead of retaining native bitmaps indefinitely.
- Clear the previous image and its metadata during uncached navigation, preventing a stale picture or copied prompt from appearing for the next file.
- Refresh the current folder list when an image added since launch is selected in the Open dialog.
- Dispose the active load cancellation source when the viewer closes.

This is a **Windows test candidate**, not a confirmed final release: the .NET 8 SDK is not available in the assembly environment. Run `Build EXE.bat` and the checks in `QA-CHECKLIST.md` on Windows before creating a public GitHub release.

## v0.5.19 RC2 — Installed Windows app (2026-11-15)

The **intended end-user product** is `PixMeta-Setup-v0.5.19-RC3.exe` — a normal Windows x64 installer. **Users do not need .NET, an SDK, Visual Studio, or BAT files.**

Installer features:
- Per-user installation without administrative rights into `%LOCALAPPDATA%\Programs\PixMeta`.
- Start Menu shortcut and optional Desktop shortcut.
- Windows Uninstall support through Installed Apps and Start Menu.
- Registers PixMeta under **Open with** for PNG, JPG/JPEG, WEBP, GIF, BMP, TIFF, AVIF, HEIC and APNG; associations are optional during setup.
- The installed app accepts image paths from Windows Explorer, including spaces and Cyrillic characters.
- Windows 10/11 **do not allow installers to silently override the current default image viewer**. Choose PixMeta as the default in Settings → Apps → Default apps (or Open with → Always use this app). The installer offers to open that page after installation.
- Running a later installer with the same AppId upgrades the existing per-user installation.

**Build artifacts:** The `PixMeta/installer/PixMeta.iss` Inno Setup script and `.github/workflows/build-windows.yml` GitHub Actions workflow make `PixMeta-Setup-v0.5.19-RC3.exe` and an optional standalone portable `PixMeta.exe`.

This ZIP contains the installer **source and automatic build recipe**, NOT a precompiled `Setup.exe`. The full Windows packaging must be compiled and tested on a Windows runner. For a developer-only local build, run `Build Setup.bat` with .NET 8 SDK and Inno Setup 6 installed. For actual users, provide the compiled setup executable from GitHub Actions or a release.

## v1.0.0 — Windows stable installer

- Russian and English installer options, completion-page checkboxes and Start menu uninstall shortcut.
- Version 1.0.0 in the installed application and the setup program, with the same AppId for in-place upgrades.
- The viewer code and approved PixMeta icon are identical to the working RC3 release.
- Ready-made installer in [GitHub Releases](https://github.com/dar06051990-oss/PixMeta/releases); source files and developer BAT scripts are not needed by end users.
