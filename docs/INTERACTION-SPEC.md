# Interaction & State Spec

## State Model

| Property | Type | Default | Description | Origin |
|---|---|---|---|---|
| `FilenameImage` | `string?` | `null` | Path to current still image (bmp/jpg/png/tif). Empty/null hides the `Image` renderer. | WPF `MediaViewModel` |
| `FilenameAnim` | `string?` | `null` | Path to current animated WebP. Empty/null hides the `AnimatedWebpElement` renderer. | WPF `MediaViewModel` |
| `FilenameMedia` | `string?` | `null` | Path to current animated GIF/GIFV routed through `MediaElement`/`MediaPlayerElement`. Empty/null hides it. | WPF `MediaViewModel` |
| `CurrentVisualState` | `VisualStates` enum | `ShowImage` | One of `ShowImage`, `ShowAnim`, `ShowMedia` — drives VSM to switch renderers. | WPF `MediaViewModel` |
| `_mode` | `Modi` flags | `Normal` | Current interaction mode (flags: `Normal | Zoom | Slideshow | HelpShown`). In code-behind. | WPF `MainWindow` |
| `_isSlideshowRunning` | `bool` | `false` | Timer-driven auto-advance active. | WPF |
| `_currentFileIndex` | `int` | `0` | Index into the sorted file list for the current directory. | WPF |
| `_fileList` | `IEnumerable<string>` | empty | Natural-sorted paths of supported files in the working directory. | WPF |
| `_kv`, `_origin`, `_start`, `_maxX`, `_maxY`, `_mediaWidth`, `_mediaHeight` | various | — | Pan/zoom transform state. | WPF |

Uno adds no new state properties in v1. In polish phase, consider promoting `_mode` + `_isSlideshowRunning` to an MVUX `IState<AppMode>` record.

## User Flows

### Primary flow: standalone launch
1. User double-clicks an image or launches with a file arg (`args[1]` points to an image)
2. `App` reads settings from `appsettings.json` via `IWritableOptions<ImageViewerSettings>`
3. `MainPage` is created with the current directory; fullscreen presenter applied to `AppWindow`
4. `MediaFileServer.EnumerateDirectory` reads files filtered by `IncludedFileExtensions`, sorted naturally
5. Current image displayed; VSM switches to `ShowImage` (still), `ShowAnim` (WebP), or `ShowMedia` (GIF/MP4-like)
6. Keyboard/mouse drives navigation, zoom, pan, slideshow

### Flow changes from WPF

| WPF | Uno | Why |
|---|---|---|
| `App.xaml` `StartupUri="MainWindow.xaml"` | `App.xaml.cs` `OnLaunched` creates `Window`, installs `Frame`, navigates to `MainPage` | Uno hosts use `IHostBuilder` + `Window`+`Frame` pattern; no `StartupUri` |
| `Window.WindowStyle="None" WindowState="Maximized"` | `AppWindow.SetPresenter(FullScreen)` in `OnLaunched` | WPF + Uno window APIs diverge; presenter model is WinUI-native |
| `Window.Loaded` event | `Page.Loaded` event | Direct equivalent |
| `Window.Close()` | `AppWindow.Destroy()` or `Application.Current.Exit()` | Close semantics differ subtly — `Exit()` terminates the process; `Destroy()` disposes the window |
| `new WpfImageViewer.MainWindow(folder, ...).ShowDialog()` from host app | **No direct equivalent.** Preserve constructors on `MainPage`; host would navigate to it from their own `Window.Frame`. Out-of-scope wrapper: `ContentDialog`. | `Page` ≠ dialog-capable `Window` in WinUI |

### Secondary flow: dialog-mode embedding (future scope)

The source supports `new MainWindow(...).ShowDialog()` as a library. v1 preserves the three constructor overloads on `MainPage` but does **not** ship a `ContentDialog` wrapper. Documented in `docs/MIGRATION-LOG.md` as a v2 candidate for host apps that want the modal-dialog ergonomics on Uno.

## Component State Matrix

### Media renderer (the `UserControl` hosting 3 switchable children)

| VisualState | `SwitchableImage.Visibility` | `SwitchableMediaElement.Visibility` (or equivalent) | `SwitchableAnim.Visibility` | Trigger |
|---|---|---|---|---|
| `ShowImage` | Visible | Collapsed | Collapsed | File extension is bmp/jpeg/jpg/png/tif/tiff, OR gif/webp with `RunAnimatedGifs=false` |
| `ShowMedia` | Collapsed | Visible | Collapsed | File is gif/gifv AND `RunAnimatedGifs=true` |
| `ShowAnim` | Collapsed | Collapsed | Visible | File is animated webp AND `RunAnimatedGifs=true` |

In Uno v1: renderer-switching logic preserved via same `VisualStateManager`, but the `ShowAnim` state's `Image` is now fed by a `WriteableBitmap` frame loop (codec layer) rather than a custom `AnimatedWebpElement`.

### Status labels

| State | `Label1` visibility | `Label2` visibility | Trigger |
|---|---|---|---|
| Default | Hidden | Hidden | — |
| Filename shown | Visible for `MsgFadeoutSeconds` | Hidden | File loaded, mouse moved, mode changed |
| Help shown | Hidden | Visible | `H` / `I` / `F1` key |
| Slideshow indicator | Visible | Hidden | `Space` (start/pause), `PageUp`/`PageDown` (duration) |

## Animation Inventory

| Animation | Duration | Easing | Implementation (WPF) | Uno target |
|---|---|---|---|---|
| VSM renderer switch | 0 ms (instant) | — | `DiscreteObjectKeyFrame` at `KeyTime="0"` | Same — `ObjectAnimationUsingKeyFrames` is supported in Uno VSM |
| Status label fade (optional v2) | ~400 ms | Ease-out | Not in source | Candidate: `Storyboard` with `DoubleAnimation` on `Opacity` |
| Animated WebP frame loop | per-frame delay from codec | — | `System.Timers.Timer` + `InvalidateVisual` on `Canvas` subclass | Per-frame `DispatcherTimer` updating `WriteableBitmap.PixelBuffer` bound to `Image.Source` |
| Animated GIF frame loop | native `MediaElement` | — | `MediaElement` auto-plays | WinUI `Image` auto-animates GIFs natively, OR feed via ImageSharp loop (same path as WebP) |

## Edge Cases

- **Unsupported file extension** — silently skip in the file list; if arg-provided file is unsupported, show help overlay.
- **Corrupt/unreadable image** — `NullImageConverter` returns `DependencyProperty.UnsetValue`, keeping prior image visible until a new one loads.
- **Zero images in directory** — show help overlay with "no supported files here" hint (not in source, add in v1).
- **Animated WebP decode failure (ImageSharp exception)** — fall back to still-frame rendering (decode first frame only).
- **Window loses focus while slideshow running** — slideshow pauses; resumes on regain (unless `CloseOnLostFocus=true`, which terminates the window).
- **WASM-specific:** no `HttpListener`, no `System.Drawing`, no `shlwapi`. All three must be replaced before WASM build succeeds. Covered in migration plan.
- **Desktop (Linux/macOS) fullscreen:** Skia Desktop has a presenter model slightly different from WinAppSDK. Verify in Phase 7.
- **File system access on WASM:** browser sandboxed. For v1, the WASM build supports `FileOpenPicker`-driven single-image loads only (no directory enumeration). Document this limitation.
