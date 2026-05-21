# Architecture

## Original Architecture (WPF)

- **Pattern:** Primarily code-behind in `MainWindow.xaml.cs` (934 LoC), with a lightweight `MediaViewModel` (INPC with 4 properties) and a `MediaFileServer` service for feeding the WPF `MediaElement` over `http://localhost`.
- **Layers:**
  - *UI:* `MainWindow.xaml` + `MainWindow.xaml.cs` — single fullscreen `Window` hosting three switchable media renderers inside a `UserControl` (stock `Image`, `MediaElement`, custom `AnimatedWebpElement`) selected via `VisualStateManager` + Blend `DataTrigger` bound to `MediaViewModel.CurrentVisualState`.
  - *ViewModel:* `MediaViewModel.cs` — 4 INPC properties (`FilenameAnim`, `FilenameImage`, `FilenameMedia`, `CurrentVisualState: VisualStates enum`).
  - *Services:* `MediaFileServer.cs` (HttpListener proxy, Windows-only); `NaturalStringComparer.cs` (P/Invoke to `shlwapi.dll`).
  - *Codec:* `AnimatedWebpElement.cs` (custom `Canvas` subclass for animated WebP) + `WebP.cs` (~2400 LoC P/Invoke wrapper around `libwebp.dll`).
  - *Settings:* `Properties.Settings` auto-generated class (12 user-scope keys) backed by `App.config` + `user.config`.
- **Key dependencies:**
  - `Microsoft.Xaml.Behaviors.Wpf` 1.1.31 (single `PackageReference`) — `DataTrigger` + `GoToStateAction` in XAML
  - Native DLLs shipped: `libwebp.dll`, `libwebpdecoder.dll`, `libwebpdemux.dll` (x86 + x64)
- **Platform coupling (Windows-only):** `libwebp` P/Invoke, `shlwapi.dll StrCmpLogicalW`, `gdi32.dll DeleteObject`, `System.Drawing.Bitmap` + `Imaging.CreateBitmapSourceFromHBitmap`, `HttpListener`.
- **Consumption modes:**
  1. Standalone `.exe` launched directly (reads settings from `user.config`)
  2. Library reference — another WPF app calls `new WpfImageViewer.MainWindow(...).ShowDialog()` using one of three overloaded constructors

## Target Architecture (Uno Platform)

- **Single Project multi-target** (`Uno.Sdk`):
  - `net10.0-windows10.0.26100` — WinAppSDK (native WinUI 3)
  - `net10.0-browserwasm` — WebAssembly
  - `net10.0-desktop` — Skia Desktop (Windows, macOS, Linux)
- **Pattern:** Template-provided MVUX scaffold. Source `MediaViewModel` retained (small enough that an MVUX rewrite adds no value for v1; reassess in polish phase).
- **Codec:** `SixLabors.ImageSharp` + `SixLabors.ImageSharp.Webp` — managed, cross-platform. Decoded frames piped into `WriteableBitmap` instances bound to a stock WinUI `Image` control. Replaces `AnimatedWebpElement`, `WebP.cs`, all native libwebp DLLs, and the `System.Drawing` + GDI interop.
- **Natural sort:** managed regex-based `IComparer<string>` — replaces `shlwapi.dll StrCmpLogicalW`.
- **Fullscreen:** `AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)` from `App.xaml.cs` on Windows/Desktop; browser-fullscreen API on WASM via standard WinUI path.
- **Keyboard:** `KeyboardAccelerator` collection on `MainPage`. Source's `KeyDown` event handlers → declarative accelerators.
- **Behaviors:** `Microsoft.Xaml.Behaviors.WinUI.Managed` (drop-in namespace-compatible replacement for the Blend behaviors).
- **Settings:** `appsettings.json` loaded by `Uno.Extensions.Configuration` into `IWritableOptions<ImageViewerSettings>`. First-run write-through preserves user edits.
- **MediaFileServer:** **removed** — WinUI `MediaPlayerElement` accepts `MediaSource.CreateFromStream`; no need for an HTTP shim. (Also, `HttpListener` doesn't run on WASM.)

## Layer Mapping

| Layer | WPF Original | Uno Migration | Changes |
|---|---|---|---|
| UI | `MainWindow.xaml` + code-behind | `MainPage.xaml` + code-behind | `Window` → `Page`, `local:AnimatedWebpElement` → `Image` + `WriteableBitmap` binding |
| ViewModel | `MediaViewModel.cs` (INPC) | `MediaViewModel.cs` (INPC, unchanged) | Namespace only |
| Services | `MediaFileServer`, `NaturalStringComparer` | `NaturalStringComparer` (managed), `ImageSharpDecoder` | `MediaFileServer` deleted; NaturalStringComparer rewritten managed |
| Codec | `WebP.cs` (P/Invoke) + `AnimatedWebpElement.cs` | `ImageSharpDecoder.cs` (managed) | Full rewrite — 2400+ LoC of P/Invoke deleted |
| Settings | `Properties.Settings` / `user.config` | `IWritableOptions<ImageViewerSettings>` / `appsettings.json` | Same 12 keys, different plumbing |
| Platform entry | `App.xaml` `StartupUri` | `App.xaml.cs` + per-platform `Platforms/Desktop/Program.cs`, `Platforms/WebAssembly/Program.cs` | Uno host pattern |

## Dependencies

### Retained (cross-platform / framework)

- `System.IO` — file enumeration (unchanged)
- `System.Threading` / `System.Threading.Tasks` — timers, async (unchanged)
- `MediaViewModel` public API

### Replaced

| Original | Replacement | Why |
|---|---|---|
| `Microsoft.Xaml.Behaviors.Wpf` 1.1.31 | `Microsoft.Xaml.Behaviors.WinUI.Managed` | WPF package not Uno-compatible; WinUI.Managed is API-compatible |
| `libwebp.dll` + `WebP.cs` | `SixLabors.ImageSharp` + `SixLabors.ImageSharp.Webp` | Native P/Invoke, Windows-only; ImageSharp is managed + cross-platform |
| `System.Drawing.Bitmap` + `gdi32.dll` | `WriteableBitmap.PixelBuffer` pixel copy | `System.Drawing.Common` is Windows-only on modern .NET |
| `shlwapi.dll StrCmpLogicalW` | Managed regex-based natural comparer | P/Invoke Windows-only |
| `System.Configuration` / `Properties.Settings` | `Microsoft.Extensions.Configuration` + `IWritableOptions<T>` | `System.Configuration` not supported in cross-platform / WASM |

### Removed

- Native `libwebp*.dll` binaries (x86 + x64) — 6 files deleted, ~1.5 MB
- `WebP.cs` (2397 LoC)
- `AnimatedWebpElement.cs` (313 LoC)
- `MediaFileServer.cs` (66 LoC) — `HttpListener` proxy not needed
- `App.config`, `Properties/Settings.settings`, `Properties/Settings.Designer.cs`, `Properties/AssemblyInfo.cs`, `Properties/app.manifest` — handled by Uno.Sdk / `Package.appxmanifest`

### Added

- `SixLabors.ImageSharp` + `SixLabors.ImageSharp.Webp` — managed codec
- `Microsoft.Xaml.Behaviors.WinUI.Managed` — behaviors
- `Uno.Material` — theme (template-added)
- `Uno.Extensions.Configuration`, `Uno.Extensions.Hosting`, `Uno.Extensions.Logging` — template-added host stack
- (Polish phase) Direct overrides in `Directory.Packages.props` for `System.Security.Cryptography.Xml` and `Tmds.DBus.Protocol` to clear NU1903 warnings
