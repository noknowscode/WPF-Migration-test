# Project Instructions — ImageViewerUno (WPF → Uno migration)

## Overview

This is a WPF-to-Uno-Platform migration of **WpfImageViewer**, originally a fullscreen image viewer with animated GIF/WebP support, slideshow, and keyboard shortcuts, built with **.NET Framework 4.7.2 + WPF** (legacy non-SDK csproj).

**Source repo:** https://github.com/thomas694/WpfImageViewer
**Source license:** MIT (preserve upstream LICENSE + 3RD PARTY attribution for libwebp and the WebP.cs P/Invoke wrapper by José M. Piñeiro)
**Migration plan:** See `docs/MIGRATION-PLAN.md`
**Migration log:** See `docs/MIGRATION-LOG.md`
**Campaign role:** Slot 1 of the unOS WPF Migration 5x campaign — seeds the cumulative `MIGRATION-GUIDE.md` at `UnoVault/08-Campaigns/Active/WPF-Migration/migrations/`.

## Architecture

- **Pattern:** MVUX (adopted from template). Source `MediaViewModel` (simple INPC with 4 properties) is retained with namespace updates. Larger refactors toward `IFeed`/`IState` are optional and documented inline.
- **Navigation:** `blank` nav (single `MainPage`). No regions needed for this single-window app.
- **DI:** `Microsoft.Extensions.DependencyInjection` via `Uno.Extensions.Hosting` (template-provided)
- **Configuration:** `appsettings.json` + `Uno.Extensions.Configuration` + `IWritableOptions<ImageViewerSettings>` — replaces the source app's `Properties.Settings` / `user.config`
- **Data:** None (file-system enumeration only)

## Original WPF Architecture

- **Pattern:** Primarily code-behind in `MainWindow.xaml.cs` (934 LoC) + lightweight `MediaViewModel` (INPC, 4 props) + `MediaFileServer` (HttpListener-based local proxy feeding `MediaElement`)
- **Key libraries:** `Microsoft.Xaml.Behaviors.Wpf` 1.1.31 (single `PackageReference`)
- **Data access:** None
- **Database:** None
- **Platform dependencies (Windows-only):**
  - `libwebp.dll` / `libwebpdecoder.dll` / `libwebpdemux.dll` — P/Invoke for WebP decode (x86 + x64 binaries)
  - `shlwapi.dll StrCmpLogicalW` — P/Invoke for natural-string comparison in `NaturalStringComparer`
  - `gdi32.dll DeleteObject` + `System.Drawing.Bitmap.GetHbitmap` + `Imaging.CreateBitmapSourceFromHBitmap` — in `AnimatedWebpElement` for WPF interop
  - `HttpListener` — local file server for feeding `MediaElement`
- **Standard .NET settings:** `Properties.Settings` (`ApplicationSettingsBase`), 12 user-scope settings backed by `user.config`

## Target architecture (Uno Platform)

- **Single Project multi-target:** `net10.0-windows10.0.26100;net10.0-browserwasm;net10.0-desktop`
- **WebP/GIF decode:** `SixLabors.ImageSharp` (managed, runs on WASM) — replaces all native WebP P/Invoke and `System.Drawing`
- **Natural sort:** managed regex-based comparer — replaces `shlwapi.dll StrCmpLogicalW`
- **Media playback:** stock WinUI `Image` with `WriteableBitmap` frames; `MediaFileServer` removed (no WASM `HttpListener`; WinUI `MediaPlayerElement` accepts streams directly)
- **Fullscreen:** `AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)`
- **Keyboard:** `KeyboardAccelerator` replaces `KeyDown` handlers in code-behind (simpler than `KeyBinding`)
- **Behaviors:** `Microsoft.Xaml.Behaviors.WinUI.Managed` (drop-in-ish replacement for `Xaml.Behaviors.Wpf`)

## Project Structure

- `ImageViewerUno/` — Uno Platform project root (single-project multi-target)
- `ImageViewerUno/Models/` — MVUX feed/state records + migrated models (`MediaViewModel`, settings records)
- `ImageViewerUno/Presentation/` — Pages and ViewModels (`MainPage.xaml`, ported from `MainWindow.xaml`)
- `ImageViewerUno/Services/` — Service interfaces and implementations
- `ImageViewerUno/Strings/en/` — Localization resources (`Resources.resw`)
- `ImageViewerUno/Platforms/` — Desktop + WebAssembly host projects
- `ImageViewerUno/Styles/` — Uno Material theme overrides
- `docs/` — `MIGRATION-PLAN.md`, `MIGRATION-LOG.md`, `ARCHITECTURE.md`, `DESIGN-BRIEF.md`, `INTERACTION-SPEC.md`

## Migration Conventions

- Migrate business logic (Models, Services, ViewModels) first — expect zero or minimal changes (namespace + using updates only).
- Migrate XAML views last — this is where most changes occur.
- Replace `{Binding}` with `x:Bind` where feasible. Keep `{Binding}` if the migration is mechanical and the source used `{Binding}`.
- Replace WPF-only controls with WinUI equivalents per the control mapping table below.
- Replace `Xaml.Behaviors.Wpf` with `Microsoft.Xaml.Behaviors.WinUI.Managed` (API-compatible).
- Keep XAML lean — use Lightweight Styling and theme resources over inline values where it doesn't change visual result.
- **Search Uno Platform docs via MCP (`uno_platform_search`, `uno_platform_fetch`) before assuming API usage or patterns.**
- **Log every migration decision, gotcha, and workaround in `docs/MIGRATION-LOG.md`.** This log feeds section updates in `MIGRATION-GUIDE.md` at session end.

## Control Mapping Reference (Slot 1 scope)

| WPF Control / API | Uno/WinUI Equivalent | Notes |
|---|---|---|
| `Window` (WindowStyle=None, WindowState=Maximized) | `Page` + `AppWindow.SetPresenter(FullScreen)` | Structural change |
| `Image` (with BitmapImage source) | `Image` (with `BitmapImage` from `ms-appx:` or stream) | Direct equivalent |
| `MediaElement` | `MediaPlayerElement` | For animated GIFs if we keep native path |
| `AnimatedWebpElement` (custom Canvas) | `Image` + ImageSharp decode loop → `WriteableBitmap` | Full rewrite |
| `Label` (with `DropShadowEffect`) | `TextBlock` (with `DropShadowEffect` — supported in Uno) | Near-equivalent |
| `VisualStateManager` + Storyboard | `VisualStateManager` + `Setter`/`Storyboard` | Direct equivalent in WinUI |
| `i:Interaction.Triggers` + `ei:DataTrigger` + `ei:GoToStateAction` | Same XAML via `Microsoft.Xaml.Behaviors.WinUI.Managed` | API-compatible |
| `KeyDown` event handler in code-behind | `KeyboardAccelerator` on `Page` (preferred) or `KeyDown` handler | Accelerator path is cleaner |
| `Properties.Settings.Default.X` / `user.config` | `IWritableOptions<ImageViewerSettings>` backed by `appsettings.json` | Standard Uno.Extensions pattern |
| `HttpListener` (`MediaFileServer`) | Removed — not needed with WinUI `MediaPlayerElement` stream APIs | Delete entirely |
| `shlwapi.dll StrCmpLogicalW` (`NaturalStringComparer`) | Managed regex-based natural comparer | Cross-platform replacement |
| `libwebp.dll` P/Invoke (`WebP.cs`, 2397 LoC) | `SixLabors.ImageSharp` + `SixLabors.ImageSharp.Webp` | Delete entirely, replace with managed decode |
| `System.Drawing.Bitmap` + `gdi32.DeleteObject` + `CreateBitmapSourceFromHBitmap` | `WriteableBitmap.PixelBuffer` pixel copy | WinUI-native path |

## Key References

Before starting any migration work, read these first:

- `docs/MIGRATION-PLAN.md` — phased migration strategy and control inventory (10 phases, ~4-6 hrs)
- `docs/MIGRATION-LOG.md` — running log of changes, gotchas, and patterns
- `docs/ARCHITECTURE.md` — target system architecture, layers, dependencies
- `docs/DESIGN-BRIEF.md` — design language, spacing, color tokens, component patterns
- `docs/INTERACTION-SPEC.md` — state model, user flows, component states
- **`UnoVault/08-Campaigns/Active/WPF-Migration/migrations/MIGRATION-GUIDE.md`** — cumulative cross-slot guide (this project seeds sections 1-9)

## Pre-Review Cleanup

Before submitting code for review, scan for and remove dead code:

- Remove commented-out WPF code blocks that have been replaced.
- Remove unreferenced methods/functions that are safe to delete.
- Remove obviously unreachable or orphaned code from prior refactors.
- Leave functional code, active comments, TODOs, and intentional extension points untouched.
- If usage is uncertain, do not delete — mark with `// REVIEW: possibly unused` instead.
- Keep `// WPF-MIGRATION:` comments that document deliberate migration decisions.

## Verification

```bash
dotnet build
dotnet build -f net10.0-windows10.0.26100
dotnet build -f net10.0-browserwasm
dotnet build -f net10.0-desktop
```

Always run `dotnet build` after changes to confirm the project still compiles. Test on at least two targets (Windows + WASM) before marking a migration phase complete.
