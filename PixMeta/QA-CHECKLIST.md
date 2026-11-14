# PixMeta final QA checklist (Windows, v1.0.0)

Build with **Build EXE.bat**, then start `publish\win-x64\PixMeta.exe`.

- [ ] Open PNG, JPG, WEBP, GIF via Open button; open from Explorer via Open with; overlays appear.
- [ ] Browse a folder with Left/Right: stops at both ends (no wraparound).
- [ ] Wheel zoom follows cursor; left-drag pans; middle click resets Fit; fullscreen toggle works.
- [ ] Thumbnail strip updates when navigating and after opening a new image added to the folder.
- [ ] Immediately switch between two uncached images: never display stale preview/metadata from the previous file.
- [ ] Open a multi-frame GIF / animated WEBP and play for several minutes; RAM use stabilizes; pause, step and switch files.
- [ ] Open a high-resolution JPG, zoom toward 100%, then immediately move to another image.
- [ ] Verify Forge/A1111 PNG, JPG EXIF, WEBP, native ComfyUI PNG, PromptLora and PreviewPlus metadata.
- [ ] Test PreviewPlus PNG/JPG/WEBP both with workflow JSON ON and OFF.
- [ ] Copy Prompt, Negative, Parameters; close PixMeta; paste into Notepad (clipboard persists).
- [ ] Check Info panel, Esc behavior, minimize, and opening as Windows default viewer.
- [ ] Run PixMeta with a file path containing spaces and Cyrillic characters.

No general-purpose file editing is performed. PixMeta is a viewer.

## Installer integration QA (stable release regression checklist)

- [ ] GitHub Actions Windows build passes and produces `PixMeta-Setup-v1.0.0.exe`.
- [ ] On a clean Windows 10/11 account, run the Setup.exe. No .NET SDK or admin required.
- [ ] Start Menu shortcut launches PixMeta. Desktop shortcut is created if selected.
- [ ] Double-click a PNG/JPG/WEBP/GIF in Explorer after choosing PixMeta in Open with → Always use.
- [ ] Files with spaces and Cyrillic paths open correctly.
- [ ] In Windows Settings → Apps → Default apps, PixMeta appears as an option; setup does NOT silently hijack current associations.
- [ ] Uncheck 'Show PixMeta in Open with' during install and verify that association entries are optional.
- [ ] Reinstall over the same version and test updating over an older installed version: no duplicate shortcuts/extra uninstall entries.
- [ ] Uninstall from Windows Installed apps. App files, PixMeta shortcuts and its Open with registrations are removed; image files are NOT deleted.
- [ ] Run the portable `PixMeta.exe` independently and open an image; the self-contained build does not need a globally installed .NET runtime.

- [ ] Confirm all installation task labels, finish-page options and uninstall shortcut appear in Russian on Russian Windows (English on English Windows).
