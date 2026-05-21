# Migration Log — ImageViewerUno

**Source:** WpfImageViewer (https://github.com/thomas694/WpfImageViewer)
**Source license:** MIT — author: thomas694; third-party attribution for libwebp + WebP.cs wrapper (José M. Piñeiro)
**Started:** 2026-04-17
**Status:** In progress — Phase 3 (starter-kit config)
**Campaign slot:** 1 of 5 (unOS WPF Migration 5x)

---

## Log Format

Each entry captures:
- **Date/phase** — when it happened
- **What changed** — specific files, controls, patterns
- **Why** — the migration reason or blocker that triggered the change
- **Gotcha** — anything surprising or non-obvious
- **Pattern** — if this is a reusable migration pattern, tag it for promotion to `MIGRATION-GUIDE.md`

---

## Phase 0: Source Analysis (2026-04-17)

- Cloned `thomas694/WpfImageViewer` to `%TEMP%/wpfiv-source`
- Confirmed **legacy non-SDK csproj**: `ToolsVersion="15.0"`, `TargetFrameworkVersion=v4.7.2`, explicit `PresentationCore`/`WindowsBase`/`PresentationFramework` references, single `PackageReference` for `Microsoft.Xaml.Behaviors.Wpf 1.1.31`.
- **File inventory:** 8 C# files (~3925 LoC total, but 2397 of those are the `WebP.cs` P/Invoke wrapper that will be deleted entirely), 1 XAML view (`MainWindow.xaml`, 98 LoC), 1 converter resource dict, 1 `App.config` with 12 user-scope `Properties.Settings`.

**Discrepancies from the migration plan worth flagging (logged for guide section updates):**

1. **Plan said "XML config" → reality is `Properties.Settings` + `user.config`.** Standard .NET `ApplicationSettingsBase`, not a hand-rolled XML file. Guide Section 8 recipe should cover this exact pattern (it's what Slot 2/EventLook will also hit).
2. **Plan missed a second P/Invoke.** `NaturalStringComparer` uses `shlwapi.dll StrCmpLogicalW` — Windows-only. Adds a small second native-dep replacement recipe (managed regex natural sort).
3. **`MediaFileServer` surprise.** An `HttpListener`-based local proxy that feeds bytes to WPF `MediaElement` via `http://localhost/...`. Purely WPF-`MediaElement`-specific and not needed in WinUI (MediaPlayerElement takes streams directly). `HttpListener` doesn't exist on WASM. **Action: delete entirely, don't port.**
4. **`AnimatedWebpElement` uses three Windows-only surfaces**, not just libwebp: `System.Drawing.Bitmap`, `gdi32.dll DeleteObject`, `Imaging.CreateBitmapSourceFromHBitmap`. Worth a line in the guide: "native deps travel in packs."
5. **XAML has `VisualStateManager` + `ObjectAnimationUsingKeyFrames` driving `Visibility`.** Uno's VSM supports this — near-mechanical port.
6. **No `KeyBinding`/`InputBinding` — all keyboard is `KeyDown` handler in code-behind.** Plan said "KeyBinding → KeyboardAccelerator"; reality is "event handler → KeyboardAccelerator." Actually easier than expected.
7. **Dialog-mode constructors** (`public MainWindow(string folder, ...)`) — source supports being embedded as `ShowDialog()` from another WPF app. **Decision (Slot 1):** port all three constructor signatures verbatim onto `MainPage`; document that the caller-side `.ShowDialog()` pattern has no direct `Page` equivalent and would require a `ContentDialog` wrapper (out of scope for v1). Guide Section 3 should cover this.

**Complexity reassessment:** plan said LOW / 3-4 hrs. Actual shape: LOW-MED / 4-6 hrs. Three separate native/Windows-only surfaces (`libwebp`, `shlwapi`, `gdi32` + `HttpListener`) plus a non-trivial 934-line code-behind add real work a "LOW" rating undersells.

## Phase 1: User kickoff decisions (2026-04-17)

- **Project name:** `ImageViewerUno` (matches target directory; not the `WpfImageViewer.Uno` from the plan)
- **Target heads:** Desktop + WASM + Windows (WinAppSDK) — 3 TFMs. No Android/iOS/macCatalyst.
- **WebP library:** `SixLabors.ImageSharp` (also used for GIF decode — one codec path)
- **Natural-sort replacement:** managed regex comparer (cross-platform, drops `shlwapi.dll`)
- **Dialog constructors:** Option A (port verbatim; document `ContentDialog` as idiomatic follow-up; no wrapper in v1)
- **Pacing:** end-to-end with per-phase checkpoints + commits
- **.NET:** 10.0.200 SDK, Uno.Sdk 6.5.31

## Phase 2: Scaffold (2026-04-17)

- Ran `dotnet new unoapp -o . -n ImageViewerUno -preset recommended -tfm net10.0 -platforms desktop wasm windows -presentation mvux -markup xaml -theme material -di -config -log default -nav blank -id com.unoplatform.imageviewer`
- Layout: `ImageViewerUno.sln` at root, `ImageViewerUno/ImageViewerUno.csproj` in subfolder; Central Package Management via `Directory.Packages.props`
- **TFMs produced:** `net10.0-windows10.0.26100;net10.0-browserwasm;net10.0-desktop` — exactly the 3 heads requested, no mobile. No post-scaffold TFM stripping needed.
- `dotnet build` — **green, 0 errors, 13 warnings:**
  - `NU1903` × 9: `System.Security.Cryptography.Xml 10.0.2` (high-severity advisories GHSA-37gx-xxp4-5rgx, GHSA-w3x6-4m5h-cxqf) and `Tmds.DBus.Protocol 0.21.2` (GHSA-xrw6-gwf8-vvr9) — transitive from the `recommended` preset stack. **Action:** pin overrides in `Directory.Packages.props` during polish phase.
  - `NETSDK1198` × 1: stale `win-AnyCPU.pubxml` publish-profile reference — cosmetic.
- First build: 4:56 (WASM emscripten compile). Subsequent incremental builds fast.
- Git repo initialized (`main` branch), scaffold committed as `194f089`.

## Phase 3: Starter-Kit Config (2026-04-17)

- Copied `.mcp.json` from template (Uno remote MCP + `uno-app` local via `dotnet dnx uno.devserver --mcp-app`)
- Copied `.claude/settings.json` (project permissions + PostToolUse `dotnet format` hook for `.cs` writes) and `.claude/settings.local.json` (empty template with `DOTNET_ENVIRONMENT=Development`)
- Customized `CLAUDE.md` with WpfImageViewer specifics — source repo, original architecture, platform dependencies, target architecture, project structure (matching scaffold-produced folders), control mapping table (tailored to Slot 1 scope rather than generic)
- Copied `migration-plan.md` → `docs/MIGRATION-PLAN.md` (unchanged)
- Initialized this log with Phase 0–3 entries and the pre-flight reassessment

## Phase 4: Simple Helpers (2026-04-17)

Ported two, deleted one.

**Ported:**
- `Services/NaturalStringComparer.cs` — **rewritten from scratch** as a managed regex-based natural comparer (Regex `\d+|\D+` chunker + ordinal numeric compare with leading-zero tolerance). Replaces `shlwapi.dll StrCmpLogicalW` P/Invoke. Works on all three TFMs.
- `Converters/NullImageConverter.cs` — namespace swap (`System.Windows.Data` → `Microsoft.UI.Xaml.Data`) and **signature change**: `IValueConverter.Convert`/`ConvertBack` last param is `string language` in WinUI, not `CultureInfo culture` in WPF. Two-line change once the namespaces line up.

**Not ported (decision logged at Phase 0):**
- `MediaFileServer.cs` — the `HttpListener`-based local proxy was a workaround for WPF `MediaElement` needing a `Uri`. WinUI `MediaPlayerElement` accepts streams directly, and `HttpListener` is unsupported on WASM. Deleted entirely, not ported.

**Pattern for the guide:** `IValueConverter.Convert(value, targetType, parameter, string language)` — the signature change is surprising but mechanical. Add to §2 namespace mapping table.

Build: 0 errors, 13 warnings (unchanged from scaffold).

## Phase 5: MediaViewModel (2026-04-17)

Plain INPC record ported verbatim. Changes: namespace `WpfImageViewer` → `ImageViewerUno.Models`, added nullable annotations on the event and three string props. Enum `VisualStates` moved alongside it.

MVUX refactor (IFeed/IState) explicitly deferred. The model is 4 scalar props — adding MVUX abstraction wouldn't earn its keep. Reassess in polish phase or Slot 2 if the pattern recurs.

Build: 0 errors, 13 warnings.

## Phase 6 + 8 + 9: MainPage, Settings, Keyboard (2026-04-17)

Merged into one commit because the three phases are tightly coupled (settings feed MainPage construction, keyboard is inside MainPage code-behind).

### Settings (§8)
- `Models/ImageViewerSettings.cs` — record with 12 `init` properties matching the source 12 `Properties.Settings` keys, each with a sane default.
- `appsettings.json` — added `ImageViewerSettings` section with the same defaults (duplicated, yes, but the record defaults are the fallback when DI isn't wired and the JSON is the normal path).
- `App.xaml.cs` — added `.Section<ImageViewerSettings>()` to `UseConfiguration`.
- **MainPage pulls settings** via `(Application.Current as App)?.Host?.Services.GetService(typeof(IOptions<ImageViewerSettings>))`. Had to promote `App.Host` + `App.MainWindow` from `protected` to `public` (build error `CS0122` flagged this).

### View (§6)
- `MainPage.xaml` — a direct structural port of `MainWindow.xaml`. Window→Page, VSM preserved, three switchable renderers inside a `ContentControl` (stock `Image`, `MediaPlayerElement`, second `Image` for the animated-WebP path).
- `MainPage.xaml.cs` — all three source constructor overloads ported verbatim (the decision logged at Phase 1). Image loading goes through `StorageFile.GetFileFromPathAsync` + `BitmapImage.SetSourceAsync` with a `file:///` URI fallback. File picker uses `FileOpenPicker` with conditional `WinRT.Interop.InitializeWithWindow.Initialize` on `WINDOWS`. Explorer integration + clipboard gated via inline `OperatingSystem.IsWindows()` check.
- `App.xaml.cs` — added `AppWindow.SetPresenter(FullScreenPresenter)` inside `#if HAS_UNO || WINDOWS` + try/catch for WASM no-op.
- `App.xaml` — registered `NullImageConverter` as a `StaticResource` for reference even though MainPage sets `Image.Source` in code-behind (more robust than string-binding across Uno heads).

### Keyboard (§9)
- Source has no `KeyBinding`/`InputBinding` in XAML — everything goes through `PreviewKeyDown` in code-behind. Migration is mostly a `Key.` → `VirtualKey.` swap plus `Keyboard.Modifiers == ModifierKeys.Alt` → `e.KeyStatus.IsMenuKeyDown`.
- **OemPlus/OemMinus gotcha:** these VirtualKey enum members do NOT exist. Used raw codes `(VirtualKey)187` and `(VirtualKey)189`.

### Gotchas logged (high-value for guide)
1. **`Microsoft.Xaml.Behaviors.WinUI.Managed` is WinAppSDK-only**, NOT Uno-cross-platform. Fix: handled VSM transition in code-behind via `MediaViewModel.PropertyChanged` → `VisualStateManager.GoToState`. Alternative: `CommunityToolkit.WinUI.Behaviors` is genuinely cross-platform.
2. **`Border.ClipToBounds` does not exist in WinUI** — `Border` clips its child by default. Just remove.
3. **`PointerWheelChanged` uses `PointerRoutedEventArgs`**, there is no `PointerWheelRoutedEventArgs`.
4. **`KeyRoutedEventArgs.KeyStatus` is NotImplemented on WASM** — `IsMenuKeyDown` returns false, so Alt+arrow keyboard-pan degrades to a no-op on WASM. Acceptable for v1.
5. **`Application.Exit()` is NotImplemented on WASM** (expected). Wrapped in try/catch.

Build: 0 errors, 19 warnings (3 added for SixLabors.ImageSharp moderate vuln × multi-TFM + 2 Uno0001 NotImplemented warnings on WASM).

## Phase 7: ImageSharp codec replacement (2026-04-17)

The campaign's "native-dep elimination" showcase.

**Deleted entirely** from source:
- `WebP.cs` (2397 LoC)
- `AnimatedWebpElement.cs` (313 LoC)
- `x86/*.dll` + `x64/*.dll` (6 native binaries, ~3 MB)

**Replaced with** (in `ImageViewerUno/Services/`):
- `AnimatedImageDecoder.cs` (37 LoC) — static: `Image.Load<Bgra32>(stream)` → iterate frames → `CopyPixelDataTo(byte[])` → read frame-duration from GIF/WebP metadata (1/100-sec for GIF → ×10 ms; WebP native ms).
- `AnimatedImagePlayer.cs` (77 LoC) — instance per Image control: holds a `WriteableBitmap`, `DispatcherTimer` drives frame advance, pushes BGRA into `PixelBuffer.AsStream()` and calls `Invalidate()`.

MainPage calls the player for `.gif` (when `RunAnimatedGifs=true`) and `.webp`. Still images stay on `BitmapImage.SetSourceAsync`.

**Name collision gotcha:** `SixLabors.ImageSharp.Image` collides with `Microsoft.UI.Xaml.Controls.Image` (pulled via global usings). Fully qualify the ImageSharp side at call sites (`SixLabors.ImageSharp.Image.Load<Bgra32>(stream)`).

**Net:** ~2710 LoC of P/Invoke + custom `Canvas` element → 114 LoC of managed code. 3 MB of native DLLs removed. Gains animated-WebP support on macOS, Linux, WebAssembly — features the source app could never have had on Windows alone.

Build: 0 errors, 19 warnings.

## Phase 10: Cross-platform build verification (2026-04-17)

Per-TFM build results:

| TFM | Errors | Warnings | Notes |
|---|---|---|---|
| `net10.0-windows10.0.26100` (WinAppSDK) | 0 | 5 | 4 transitive NU1903 + 1 `NETSDK1198` stale publish profile |
| `net10.0-desktop` (Skia) | 0 | 4 | 4 transitive NU1903 (Tmds.DBus for Linux) |
| `net10.0-browserwasm` (WASM) | 0 | 4 | 4 transitive NU1903; Uno0001 NotImplemented warnings suppressed for this build, appear in multi-TFM |

Runtime smoke test **deferred to user** — running Uno UI apps in an autonomous agent session would open windows on the user's desktop. Recommended verification commands:

```bash
dotnet run -f net10.0-windows10.0.26100    # WinAppSDK head
dotnet run -f net10.0-desktop              # Skia Desktop head
dotnet run -f net10.0-browserwasm          # WASM — opens in browser
```

## Phase 11: Uno Material theme (2026-04-17)

No additional work needed. The `-theme material` + `-toolkit` flags on the scaffold pulled in `Uno.Material` + `Uno.Toolkit.UI.Material` + `<utum:MaterialToolkitTheme>` in `App.xaml`. The image viewer's intentional black/image-first aesthetic aligns with Material dark semantics — no lightweight styling overrides needed for v1.

## Phase 12: MIGRATION-GUIDE.md seeded (2026-04-17)

Populated all 9 Slot-1 sections of the cumulative guide with concrete recipes, code snippets, decision frameworks, and gotcha tables. Each section now reads end-to-end as standalone reference material suitable for promotion to the eventual `wpf-to-uno-migration` skill.

Key guide contributions from Slot 1:

- §1 **Project Conversion:** the full `dotnet new unoapp` command used, the "bring/update/drop" file checklist, the bottom-up migration order.
- §2 **Namespace & API Mapping:** complete `System.Windows.*` → `Microsoft.UI.Xaml.*` table + 6 documented gotchas (ClipToBounds, PointerWheel, KeyStatus on WASM, etc.).
- §3 **Window → Page & AppWindow:** root-element swap, fullscreen presenter recipe, dialog-mode constructor decision pattern, per-platform window behavior table.
- §4 **Control Replacements:** the `Microsoft.Xaml.Behaviors.WinUI.Managed` anti-pattern plus 2 alternatives.
- §5 **Third-Party WPF Library Replacements:** updated the Xaml.Behaviors.Wpf row with the Slot 1 learning.
- §6 **Platform-Specific API Abstraction:** articulated Level 1 (inline `OperatingSystem.IsWindows()`) vs Level 2 (DI-registered interface) with decision framework.
- §7 **Native Dependencies:** the "native deps travel in packs" rule + decision-framework flowchart + full case studies for both libwebp and shlwapi replacements.
- §8 **Settings & Configuration:** 4-step recipe with code for `Properties.Settings` → `IOptions<T>` + `IWritableOptions<T>` + per-platform storage location table.
- §9 **Input Handling:** 2-column WPF↔Uno translation table, `KeyBinding` vs `KeyboardAccelerator` decision rule, pointer event mapping.
- §17 **Build & Cross-Platform Validation:** known-benign warnings table, 6 common first-time build errors with fixes, commit-cadence pattern.

---

## Recurring Patterns Observed

| Pattern | Description | Frequency in Slot 1 | Reusable? |
|---|---|---|---|
| Namespace swap with ctor/signature nuance | `System.Windows.Data.IValueConverter` → `Microsoft.UI.Xaml.Data.IValueConverter` changes `CultureInfo culture` to `string language` | 1× (NullImageConverter) | Yes — every converter |
| Inline `OperatingSystem.IsWindows()` gate for shell integration | Middle-click Explorer reveal stays inline, non-Windows targets silently lose the feature | 1× | Yes — any single-feature Windows-only call |
| `Application.Current as App → Host → Services` for settings | Service-locator pull of `IOptions<T>` inside code-behind | 1× | Yes — every Page that needs settings |
| Native-dep replacement via ImageSharp | libwebp P/Invoke → managed codec; shared decode + player services | 2× (WebP + animated GIF use same code path) | Yes — blueprint for any raster format |
| Code-behind `PropertyChanged` → `VisualStateManager.GoToState` | Replaces single-action Blend behaviors without adding a package | 1× (VSM-driven renderer switch) | Yes — every simple state-switcher |

---

## Gotchas and Workarounds

| Issue | Workaround | Root Cause | Reported? |
|---|---|---|---|
| Plan underestimated native-dep count (missed `shlwapi` + `gdi32` in addition to `libwebp`) | Document "native deps travel in packs" — audit every `DllImport`, `using System.Drawing`, `using Microsoft.Win32`, `using System.Configuration` in Phase 0 | Migration plan derived from high-level inspection | Yes — MIGRATION-GUIDE §7 |
| `MediaFileServer` `HttpListener` proxy (66 LoC) not documented in source README | Delete entire service, not port | Purely a WPF `MediaElement` workaround; not needed with WinUI streams; `HttpListener` is WASM-incompatible | Yes — MIGRATION-GUIDE §4 |
| `MainWindow` has overloaded constructors for dialog-mode library consumption | Port all three signatures verbatim onto `MainPage`; document that caller-side `.ShowDialog()` has no Page equivalent (use `ContentDialog` if that's needed) | WPF Windows double as top-level containers AND dialog-capable objects; Pages don't | Yes — MIGRATION-GUIDE §3 |
| `Microsoft.Xaml.Behaviors.WinUI.Managed` 3.0.0 supports only `net8.0-windows10.0.17763` — fails on WASM + Desktop with NU1202 | Replace with code-behind `VisualStateManager.GoToState` (chose for Slot 1 due to single-behavior scenario) or `CommunityToolkit.WinUI.Behaviors` for multi-behavior cases | Microsoft's docs position this package as "the" Uno-compatible replacement, but it's only WinAppSDK | Yes — MIGRATION-GUIDE §4, §5 |
| `Border.ClipToBounds` does not exist in WinUI | Remove the attribute — Border clips by default | API divergence | Yes — MIGRATION-GUIDE §2 |
| `PointerWheelChanged` uses `PointerRoutedEventArgs`, not a Wheel-specific subclass | Type the handler parameter as `PointerRoutedEventArgs` | API divergence from WPF `MouseWheelEventArgs` | Yes — MIGRATION-GUIDE §2, §9 |
| `KeyRoutedEventArgs.KeyStatus` is NotImplemented on WASM (`Uno0001` warning) | Alt-modifier detection degrades to false on WASM; Alt+arrow keyboard pan is a no-op there in v1 | Uno WASM limitation | Yes — MIGRATION-GUIDE §2, §9 |
| `Application.Current.Exit()` is NotImplemented on WASM | `try { Application.Current.Exit(); } catch { }` | Browser tab cannot self-terminate | Yes — MIGRATION-GUIDE §3 |
| Scaffold's `App.Host` + `App.MainWindow` are `protected` — inaccessible to Pages | Promote to `public` (or `internal` if same-assembly) | Template default scope | Yes — MIGRATION-GUIDE §17 |
| `VirtualKey.OemPlus` / `OemMinus` do not exist as named enum members | Use raw key codes `(VirtualKey)187` / `(VirtualKey)189` | WinUI VirtualKey enum doesn't expose OEM codes as named constants | Yes — MIGRATION-GUIDE §9 |
| `SixLabors.ImageSharp.Image` collides with `Microsoft.UI.Xaml.Controls.Image` (from global usings) | Fully qualify at call sites: `SixLabors.ImageSharp.Image.Load<T>(stream)` | Template `GlobalUsings.cs` pulls in WinUI controls namespace | Yes — MIGRATION-GUIDE §17 |
| Transitive NU1903 vulns on `System.Security.Cryptography.Xml 10.0.2` + `Tmds.DBus.Protocol 0.21.2` in `unoapp --preset recommended` baseline | Add explicit `<PackageVersion>` overrides in `Directory.Packages.props` during polish phase | Upstream Uno.Sdk 6.5.31 transitive deps | Yes — MIGRATION-GUIDE §17 |

---

## What Was Easy

- Scaffolding. 30-second `dotnet new unoapp` produces a buildable, multi-target project. No post-scaffold TFM stripping needed when `-platforms` is set explicitly.
- `VisualStateManager` + `Storyboard` XAML. Ported verbatim — same syntax, same semantics.
- `MediaViewModel` (INPC, 4 props). Copy, namespace-swap, nullable-annotate, done.
- `NullImageConverter`. Two using-directive swaps + a signature change. Two-minute port.
- Keyboard. `KeyDown` handler pattern survived intact with mechanical `Key.` → `VirtualKey.` swap.
- Fullscreen window. Three lines in `App.xaml.cs` replace the WPF `Window.WindowStyle=None + WindowState=Maximized`.

## What Was Difficult

- Native-dep elimination. Not technically hard, but requires rewriting the decoder end-to-end (37+77 LoC of managed code replacing 2710 LoC of P/Invoke + custom XAML element). Three separate Windows-only surfaces (libwebp, shlwapi, gdi32) had to be tracked down and replaced.
- Settings round-trip. Writing values back on first-run from existing `user.config` was skipped (clean-install semantics for Slot 1). Real-world migrations need this.
- Cross-platform file I/O. `StorageFile.GetFileFromPathAsync` + fallback to `file:///` URI + fallback to `File.ReadAllBytes`. Three layers of fallback needed to cover WinAppSDK / Skia / WASM. The source could just use `Uri`.
- Behaviors package discovery. The "obvious" replacement (Microsoft.Xaml.Behaviors.WinUI.Managed) doesn't work on Uno. Discovering this took a failed build + targeted error diagnosis.

## Where WPF Design Helped

- The source kept the viewmodel INPC-only and thin. A 67-line port was 67 lines in Uno too.
- The source used `VisualStateManager` + `DataTrigger` rather than inline show/hide logic — VSM ported cleanly, and the single trigger was trivial to replace with code-behind PropertyChanged.
- `Properties.Settings` with `[DefaultSettingValueAttribute]` defaults gave us a clear data model to translate directly into the settings record's `init` defaults.
- Code-behind keyboard (not `KeyBinding`) — turned out easier to port than the migration plan assumed.

## Where WPF Design Hurt

- 934-line code-behind in `MainWindow.xaml.cs` (zoom, pan, slideshow, key handling, file nav, message overlay, settings read) is a lot to port. MVUX would have split this into 4-5 cleaner records + view-layer reactions. Not worth refactoring during migration; candidate for v2 polish.
- `MediaFileServer` (the `HttpListener` proxy feeding WPF `MediaElement`) is a Windows-WPF-specific hack. Good example of how WPF-era "workarounds" become cruft in the migrated codebase — identified and dropped entirely rather than ported.
- `System.Drawing.Bitmap` inside `AnimatedWebpElement`. Three different Windows-only APIs (Bitmap, GDI interop, Imaging.CreateBitmapSourceFromHBitmap) all in one custom Canvas element. Classic "WinForms thinking in WPF" — and all three disappeared in the Uno port along with libwebp.
- Two P/Invokes in a tiny 19-line helper (`NaturalStringComparer`). Would have been caught by a more paranoid Phase 0 audit, but drives home the point that "small helper" ≠ "cross-platform-safe."
