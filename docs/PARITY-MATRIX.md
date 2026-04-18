# Feature Parity Matrix — WpfImageViewer ↔ ImageViewerUno

**Source:** thomas694/WpfImageViewer (.NET Framework 4.7.2, WPF, Windows-only)
**Port:** ImageViewerUno (Uno Platform, .NET 10, three heads: WinAppSDK / Skia Desktop / WebAssembly)
**As of:** 2026-04-17 (Slot 1, v1)

**Legend:**
- ✅ — at full parity (same behaviour, same UX)
- ◐ — partial / degraded — works but with caveats noted
- ⛔ — not yet ported (documented gap)
- 🆕 — new capability gained via the migration
- — — not applicable on this target

---

## 1. Image display

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| Static image (BMP/JPG/PNG/TIF/TIFF) | ✅ | ✅ | ✅ | ◐ | WASM: loads via `StorageFile` → `BitmapImage.SetSourceAsync` when invoked through `FileOpenPicker` only (no arbitrary path access in the sandbox) |
| Animated GIF | ✅ (native `MediaElement` pipeline) | ✅ (ImageSharp frame loop) | ✅ (ImageSharp) | ✅ (ImageSharp) | Port switched from `MediaElement` to a `WriteableBitmap` frame loop for codec consistency |
| Animated WebP | ✅ (custom `AnimatedWebpElement` + libwebp P/Invoke, Windows x86+x64 only) | ✅ (ImageSharp) | ✅ (ImageSharp) | ✅ (ImageSharp) | 🆕 **Animated WebP now works on macOS, Linux, and in-browser** — the source app could only ever do this on Windows |
| Static WebP | ✅ | ✅ | ✅ | ✅ | Same codec path as animated (first frame) |
| `.gifv` extension | ✅ (via `MediaFileServer` HTTP proxy hack) | ◐ | ◐ | ⛔ | Port recognises the extension and loads it as a GIF; the HTTP proxy was deleted (it was a WPF `MediaElement` workaround) |
| Stretch behaviour (Uniform, DownOnly) | ✅ | ✅ | ✅ | ✅ | `Stretch="Uniform"` on the Image element |
| Background colour configurable | ✅ (via `BackgroundColor` setting) | ✅ | ✅ | ✅ | Applied to Page + RootGrid + ImageBorder on Loaded |

## 2. Navigation

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| Launch with file arg → show that file | ✅ | ✅ | ✅ (exe only) | ⛔ | Skia `dotnet run -- "path"` doesn't forward reliably — launch the built `.exe` directly. WASM has no CLI entry point |
| Launch with folder arg → browse folder | ✅ | ✅ | ✅ (exe only) | ⛔ | Same caveat as above |
| Launch with no arg → help overlay | ✅ | ✅ | ✅ | ✅ | Help shown via `ShowHelpOnLoad` logic in `MainPage_Loaded` |
| Left / Right → prev / next image | ✅ | ✅ | ✅ | ✅ | `KeyDown` handler on Page |
| Home / End → first / last image | ✅ | ✅ | ✅ | ✅ | |
| File list filtered by `IncludedFileExtensions` | ✅ | ✅ | ✅ | — | WASM has no `Directory.EnumerateFiles` against arbitrary paths |
| Natural-sort ordering of file list | ✅ (via `shlwapi.dll StrCmpLogicalW`) | ✅ | ✅ | ✅ | 🆕 Port uses a managed regex comparer — works everywhere |

## 3. Zoom & pan

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| Mouse wheel zoom | ✅ | ✅ | ✅ | ✅ | `PointerWheelChanged` → `Zoom(delta)` |
| `+` / `−` keyboard zoom | ✅ | ✅ | ✅ | ✅ | `VirtualKey.Add` / `Subtract` + OEM codes |
| `Space` to reset zoom | ✅ | ✅ | ✅ | ✅ | |
| Drag to pan when zoomed | ✅ | ✅ | ✅ | ✅ | `PointerPressed/Moved/Released` + pointer capture |
| Alt + arrows → keyboard pan | ✅ | ✅ | ✅ | ◐ | WASM: Alt detection requires `KeyStatus.IsMenuKeyDown` which is `NotImplemented` there → Alt+arrow becomes a no-op on WASM |
| Max pan calculated from scaled bounds | ✅ | ✅ | ✅ | ✅ | Reproduces source algorithm in `SetMaxPanValues()` |
| Zoom min / max / step configurable | ✅ | ✅ | ✅ | ✅ | Settings-driven |

## 4. Slideshow

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| `Space` toggle start/stop | ✅ | ✅ | ✅ | ✅ | Guarded by non-zoom mode |
| `PageUp` / `PageDown` → duration 1–30 sec | ✅ | ✅ | ✅ | ✅ | |
| `Esc` stops slideshow (before closing app) | ✅ | ✅ | ✅ | ✅ | Two-level Esc: 1st stops slideshow, 2nd exits |
| Duration from `ImageDurationSeconds` setting | ✅ | ✅ | ✅ | ✅ | |
| Auto-stop on end-of-list | ✅ | ✅ | ✅ | ✅ | `StopSlideshow(2)` when `_currentFileIndex >= Count - 1` |
| `DispatcherTimer`-driven | ✅ | ✅ | ✅ | ✅ | Identical pattern, different namespace |

## 5. Window / presentation

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| Fullscreen | ✅ (`WindowStyle=None + WindowState=Maximized`) | ✅ (`AppWindow.SetPresenter(FullScreen)`) | ✅ | ◐ | WASM: uses full browser tab; no OS-level fullscreen presenter (browser-fullscreen API not wired in v1) |
| Application title | ✅ (from settings) | ✅ | ✅ | ✅ | |
| Close on lost focus | ✅ | ⛔ | ⛔ | ⛔ | Not implemented in port (WASM has no focus-loss concept; Skia focus events unreliable). Documented future-scope |
| `Esc` closes app | ✅ (Window.Close) | ✅ (`Application.Current.Exit()`) | ✅ | ◐ | WASM: `Exit()` is `NotImplemented` — browser tabs can't self-close. Esc just stops slideshow on WASM |
| Window as dialog (3 overloaded constructors) | ✅ (`MainWindow.ShowDialog()`) | ◐ | ◐ | ◐ | Constructor signatures ported verbatim onto `MainPage`; no direct `.ShowDialog()` equivalent — `ContentDialog` wrapper is the idiomatic path, documented but not shipped in v1 |
| Alt+Tab title set | ✅ | ✅ | ✅ | — | Browser tab title = page title on WASM |

## 6. Overlays

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| Filename / status overlay at bottom | ✅ | ✅ | ✅ | ✅ | `TextBlock` (was `Label`) |
| Help overlay (H / I / F1) | ✅ | ✅ | ✅ | ✅ | `Border` + `TextBlock` instead of `Label` with effects |
| Message fadeout after N seconds | ✅ | ✅ | ✅ | ✅ | `DispatcherTimer`-driven; `MsgFadeoutSeconds` setting |
| `DropShadowEffect` on filename text | ✅ | ⛔ | ⛔ | ⛔ | Dropped for v1 simplicity; WinUI/Uno supports it but under different APIs. Polish candidate |
| Message text colour configurable | ✅ | ✅ | ✅ | ✅ | Resolved via `typeof(Colors).GetProperty(name)` reflection |

## 7. Mouse integrations (platform-specific)

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| Double-click → file picker | ✅ (`OpenFileDialog`) | ✅ (`FileOpenPicker`) | ✅ | ✅ | Picker in WinAppSDK requires `InitializeWithWindow(hwnd)`, gated with `#if WINDOWS` |
| Middle-click → reveal in Explorer | ✅ | ✅ | ⛔ (non-Windows Desktop) | ⛔ | Gated with `OperatingSystem.IsWindows()` — silently no-ops on macOS/Linux/WASM |
| Right-click → copy dir path to clipboard | ✅ (`Clipboard.SetText`) | ✅ (`DataPackage`) | ✅ | ✅ | WinUI/Uno `Clipboard` API differs totally; ported to `DataPackage` |
| Right double-click → copy file path | ✅ | ⛔ | ⛔ | ⛔ | Port does not distinguish single vs double right-click. Polish candidate |

## 8. Settings & configuration

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| 12 configurable settings (title, bg, msg color, zoom, slideshow duration, etc.) | ✅ (`Properties.Settings` / `user.config`) | ✅ (`appsettings.json` + `IOptions<T>`) | ✅ | ✅ | Full key-for-key parity |
| Per-user runtime persistence | ✅ (`user.config` auto-managed) | ⛔ | ⛔ | ⛔ | `IWritableOptions<T>` available (via `Uno.Extensions.Configuration`) but not wired in v1. Polish candidate |
| First-run migration from existing `user.config` | — | ⛔ | ⛔ | ⛔ | Not implemented; clean-install semantics for v1 |

## 9. Codec (the big one)

| Feature | WPF source | Uno WinAppSDK | Uno Skia Desktop | Uno WASM | Delta notes |
|---|---|---|---|---|---|
| WebP decode | ✅ (libwebp P/Invoke, Windows-only) | ✅ (ImageSharp) | ✅ (ImageSharp) | ✅ (ImageSharp) | 🆕 Cross-platform |
| GIF decode | ✅ (`MediaElement` native) | ✅ (ImageSharp) | ✅ (ImageSharp) | ✅ (ImageSharp) | Port uses ImageSharp for codec consistency |
| PNG / JPG / BMP / TIFF decode | ✅ (WPF `BitmapImage`) | ✅ (WinUI `BitmapImage`) | ✅ | ✅ | Direct port |
| GDI interop (`System.Drawing.Bitmap` + `DeleteObject`) | ✅ (WPF-Windows only) | — | — | — | Eliminated — not needed with managed codec |

## 10. Campaign-relevant gains from the migration

| Capability | WPF baseline | Uno port |
|---|---|---|
| 🆕 Runs on macOS | ❌ | ✅ (Skia Desktop) |
| 🆕 Runs on Linux | ❌ | ✅ (Skia Desktop, X11 / framebuffer) |
| 🆕 Runs in a web browser | ❌ | ✅ (WebAssembly) |
| 🆕 Animated WebP on non-Windows | ❌ | ✅ |
| 🆕 Ship-ready multi-target single project | ❌ | ✅ |
| 🆕 Material Design 3 theming | ❌ | ✅ (Uno.Material, template-provided) |
| 🆕 Configuration via `appsettings.json` | ❌ | ✅ |
| 🆕 DI host (`IHost` + `IOptions<T>`) | ❌ | ✅ |

## 11. Gaps tracked for polish / v2

Priority-ordered list of features that did NOT make v1 parity. Each is documented in `docs/MIGRATION-LOG.md` with the decision rationale.

1. **Writable settings** — `IWritableOptions<ImageViewerSettings>` not wired. User can't adjust `ImageDurationSeconds` and persist it. ~2 hours.
2. **Close on lost focus** — WPF feature dropped because Skia Desktop / WASM focus events are unreliable. Could implement on WinAppSDK + Skia-Windows only with a `#if` gate. ~1 hour.
3. **Right double-click → copy file path** — port treats all right-clicks the same. ~30 min.
4. **DropShadowEffect on overlays** — purely visual polish. ~1 hour.
5. **Browser-native fullscreen on WASM** — JS interop to `document.documentElement.requestFullscreen()`. ~2 hours.
6. **`ContentDialog` wrapper for embed-as-dialog usage** — Option B from the initial-design discussion. Only needed if another Uno app wants to embed the viewer modally. ~3 hours.
7. **First-run migration from existing `user.config`** — only matters for users upgrading from the WPF app on Windows. `#if WINDOWS` gate + `System.Configuration` one-shot read. ~2 hours.
8. **`.gifv` HTTP proxy** — deliberately not ported. The source used `HttpListener` to feed bytes into `MediaElement`; WinUI's `MediaPlayerElement` accepts streams directly and we went through `Image` anyway. **Recommend staying dropped.**

Total polish effort: **~11 hours**. Everything on this list is additive; nothing blocks v1 release.
