# PixMeta — Windows image viewer with generation metadata

**Release candidate v0.5.19 RC3** — approved PixMeta icon + Windows installer.

PixMeta is a Windows 10/11 desktop image viewer built with C# and Avalonia. It displays PNG, JPG, GIF, WEBP and other image formats and reads generation metadata from Forge Neo, ComfyUI, CivitAI, Grok, and PreviewPlus where present.

## For users

Download **PixMeta-Setup-v0.5.19-RC3.exe** from a successful GitHub Actions run (or a future release). Run it normally. The installer creates Start menu and optional desktop shortcuts using the **approved PixMeta icon**; no .NET SDK or Inno Setup is required by the user. Standard Windows uninstall is supported.

Windows file associations are available through *Open with* and Default Apps. The installer does **not** silently override Windows' default image viewer.

## Build status

The workflow [Build PixMeta Windows installer](.github/workflows/build-windows.yml) automatically builds a self-contained win-x64 PixMeta EXE and Inno Setup installer on pushes to `main`, uploads the installer as an artifact, and smoke-tests install, shortcuts and uninstall on a Windows runner.

**Note:** Source files and the installer script can be validated on other OSes, but an actual Windows installer requires a Windows build. RC3 is not a final v1.0.0 release until tested on a real Windows PC.

## Credits and tech

C# / .NET 8, Avalonia 11, Magick.NET, Inno Setup 6. See [PixMeta/README.md](PixMeta/README.md) for viewer functionality and controls.
