# Migration Plan: WpfImageViewer → Uno Platform

**Source:** [github.com/thomas694/WpfImageViewer](https://github.com/thomas694/WpfImageViewer)
**Author:** thomas694
**License:** MIT
**Complexity:** LOW (Slot 1)
**Estimated effort:** 3-5 hours
**Target:** Uno Platform Single Project (Uno.Sdk latest stable)

---

## 1. Project Overview

A fullscreen WPF-based image viewer with animated GIF/WebP support, slideshow, keyboard/mouse shortcuts, and an XML-file-based configuration. Can be used standalone or embedded as a dialog.

**Why Slot 1:** Small single-window app that exercises three orthogonal patterns every WPF-to-Uno migration encounters: (1) converting a legacy non-SDK .csproj to SDK-style, (2) swapping a Windows-only native codec (libwebp.dll) for a cross-platform alternative, (3) replacing XML-file settings with `Uno.Extensions` configuration. Sets the baseline "what changes, what stays" narrative that the four subsequent migrations build on.

**Why this matters for the campaign:** Most real WPF codebases in the wild are still legacy-csproj + .NET Framework. Leading the series with a project in that exact shape makes the rest of the content feel honest to the target audience.

---

## 2. Architecture and Dependency Review

### Current Architecture
- **Pattern:** Primarily code-behind in MainWindow.xaml.cs, with a `MediaViewModel` and `MediaFileServer` for image sequencing
- **Framework:** .NET Framework 4.7.2, WPF
- **Project type:** Legacy (non-SDK) .csproj, `ToolsVersion="15.0"`
- **NuGet:** `Microsoft.Xaml.Behaviors.Wpf` 1.1.31

### Key Components
| Component | Type | Migration Impact |
|-----------|------|-----------------|
| MainWindow.xaml / .xaml.cs | View + code-behind | Namespace + Window→Page |
| MediaViewModel.cs | ViewModel | Near-zero changes |
| MediaFileServer.cs | Service (file enumeration) | Near-zero changes |
| AnimatedWebpElement.cs | Custom FrameworkElement for WebP | **Rewrite against cross-platform codec** |
| WebP.cs + libwebp.dll (x86/x64) | P/Invoke wrapper around native WebP | **Replace entirely** |
| NaturalStringComparer.cs | Sort helper | Zero changes |
| NullImageConverter.cs | Value converter | Namespace change |
| App.config | Framework config | Replace with appsettings.json |

### Dependency Analysis
- **NuGet:** Xaml.Behaviors.Wpf → swap for `Microsoft.Xaml.Behaviors.WinUI.Managed` (Uno-compatible)
- **Framework APIs:** System.Windows.Media.Imaging (BitmapImage), System.Windows.Controls.Image
- **Platform APIs:** Native `libwebp.dll` via P/Invoke — Windows-only
- **File system:** `System.IO` for directory enumeration — portable

**Verdict:** Two real migration workitems — SDK-style conversion and WebP codec replacement. Everything else is mechanical.

---

## 3. UI and Control Inventory

| WPF Control | Count | Uno/WinUI Equivalent | Migration Effort |
|-------------|-------|---------------------|-----------------|
| Window (fullscreen) | 1 | Page + AppWindow full-screen | LOW |
| Image | 1 | Image | ZERO |
| Custom `AnimatedWebpElement` | 1 | Replace with `Image` + `Microsoft.Toolkit.Uwp.UI.Controls.ImageEx` or `SixLabors.ImageSharp` decode loop | MED |
| MediaElement (for animated GIF) | Maybe | MediaPlayerElement, or `ImageEx` | MED |
| Behaviors (Xaml.Behaviors.Wpf) | Varies | `Microsoft.Xaml.Behaviors.WinUI.Managed` | LOW — API compatible |
| KeyBinding / InputBinding | Multiple | `KeyboardAccelerator` | LOW |

### XAML Namespace Changes
- Default WPF xmlns works in Uno — declarations mostly unchanged
- `xmlns:i="http://schemas.microsoft.com/xaml/behaviors"` (same namespace post-Xaml.Behaviors)
- C# usings: `System.Windows.*` → `Microsoft.UI.Xaml.*`

---

## 4. Platform/API Usage Review

| API/Feature | Used? | Uno Support | Action Required |
|-------------|-------|-------------|----------------|
| BitmapImage (BitmapDecoder) | Yes | Full on Windows/Skia | None |
| Animated GIF rendering | Yes | `ImageEx` or platform fallback | Replace custom logic |
| WebP decoding (native DLL) | Yes | No cross-platform equivalent | Replace with `SixLabors.ImageSharp.Webp` or `Magick.NET` |
| XML config file (Settings.xml) | Yes | N/A | Migrate to `Uno.Extensions.Configuration` |
| Directory enumeration | Yes | Full | None |
| Fullscreen window | Yes | `AppWindow.SetPresenter(FullScreenPresenter)` | API change |
| Mouse wheel / KeyDown | Yes | Full | None |
| Drag-and-drop | Maybe | Full | None |

---

## 5. Likely Blockers

| Blocker | Severity | Mitigation |
|---------|----------|------------|
| Legacy csproj → SDK-style | LOW | Manual csproj rewrite or `dotnet new unoapp` + copy code |
| .NET Framework 4.7.2 → net9 | LOW | TFM bump, review assembly refs |
| libwebp.dll P/Invoke | MEDIUM | Replace with managed `SixLabors.ImageSharp` — runs on WASM too |
| Animated GIF/WebP rendering | MEDIUM | Use `ImageEx` from CommunityToolkit or decode frames manually |
| Fullscreen window | LOW | `AppWindow` API |
| XML config | LOW | Standard `Uno.Extensions.Configuration` swap |

---

## 6. Migration Strategy

**Approach:** Fresh Uno project + copy code file-by-file (not in-place conversion)

Legacy csproj + packages.config style can't be converted cleanly to Uno.Sdk. Cleanest path: `dotnet new unoapp`, copy source files, rewire references. Since the app is small this takes less time than coercing the old csproj.

---

## 7. Phased Implementation

### Phase 0: Starter Kit Setup (10 min)
1. Copy migration starter kit into project root
2. Customize `CLAUDE.md` with WpfImageViewer specifics
3. Drop this plan into `docs/MIGRATION-PLAN.md`
4. Initialize `docs/MIGRATION-LOG.md`
5. **Open `migrations/MIGRATION-GUIDE.md` for cumulative notes** (this is the first project — seed the guide)

### Phase 1: Scaffold Uno Project (15 min)
1. `dotnet new unoapp -o WpfImageViewer.Uno --preset=recommended` (MVUX default per preferences)
2. Verify multi-target build: `dotnet build`
3. Confirm Uno.Sdk version in `global.json`

### Phase 2: Migrate Simple Files First (20 min)
1. Copy `NaturalStringComparer.cs`, `NullImageConverter.cs`, `MediaFileServer.cs`
2. Namespace update
3. Build — expect zero compile errors
4. **Log in MIGRATION-GUIDE.md:** "Pure C# helpers port unchanged" pattern

### Phase 3: ViewModel + Model (20 min)
1. Copy `MediaViewModel.cs`
2. Review INPC pattern — consider MVUX `IFeed`/`IState` upgrade (note in log as optional)
3. Build

### Phase 4: XAML Shell (30 min)
1. Port MainWindow.xaml → MainPage.xaml
2. Replace `<Window>` with `<Page>`; remove Title/Width/Height
3. Re-create fullscreen presenter via `AppWindow` in App.xaml.cs
4. Port keyboard shortcuts to `KeyboardAccelerator`
5. Build for Windows first

### Phase 5: Replace WebP/GIF Rendering (60 min) — **the hard part**
1. Remove `libwebp.dll`, `WebP.cs`, `AnimatedWebpElement.cs`
2. Add `SixLabors.ImageSharp` + `SixLabors.ImageSharp.Webp` NuGet
3. Implement decode loop producing `BitmapImage`/`WriteableBitmap` frames
4. Bind to stock `Image` control
5. **Log:** full before/after diff — this is the campaign's "native dep replacement" showcase

### Phase 6: Settings Migration (20 min)
1. Remove App.config + Settings.xml handling
2. Add `appsettings.json`
3. Use `Uno.Extensions.Configuration` + `IWritableOptions<T>`
4. **Log:** XML-config → appsettings.json recipe for the guide

### Phase 7: Cross-Platform Test + Polish (45 min)
1. Build + run Windows — verify full feature parity
2. Build + run WASM — verify WebP renders (proves the native-dep replacement worked)
3. Build + run Desktop (Skia) — same
4. Capture before/after screenshots
5. Apply Uno Material theme
6. Final MIGRATION-GUIDE.md update

---

## 8. Expected Effort/Risk Areas

| Area | Effort | Risk | Notes |
|------|--------|------|-------|
| Scaffold | 15 min | NONE | Standard template |
| Helpers + VM | 40 min | LOW | Near-mechanical |
| XAML shell | 30 min | LOW | Controls are 1:1 |
| WebP/GIF replacement | 60 min | MED | Real engineering work |
| Settings | 20 min | LOW | Standard pattern |
| Cross-platform testing | 45 min | LOW | |
| **Total** | **~3-4 hrs** | **LOW-MED** | |

---

## 9. Migration Log Checklist

- [ ] Uno project scaffolded, multi-target build green
- [ ] Legacy C# helpers port unchanged (log exact diff)
- [ ] ViewModel ports with namespace changes only
- [ ] MainPage renders on Windows
- [ ] Fullscreen presenter works
- [ ] Keyboard shortcuts wired via KeyboardAccelerator
- [ ] WebP decoding replaced with ImageSharp, renders on Windows
- [ ] WebP decoding renders on WASM (proves cross-platform)
- [ ] Animated GIF renders on all three targets
- [ ] XML settings migrated to appsettings.json + IWritableOptions
- [ ] Material theme applied
- [ ] Before/after screenshots captured per platform
- [ ] **MIGRATION-GUIDE.md seeded with Slot 1 learnings**

---

## 10. Contributions to MIGRATION-GUIDE.md

As the first project, this migration seeds these sections of the cumulative guide:

1. **Legacy csproj → Uno.Sdk:** the fresh-scaffold + copy-files pattern
2. **Namespace mapping table:** `System.Windows.*` → `Microsoft.UI.Xaml.*` cheatsheet
3. **Window → Page conversion:** Title/Width/Height → AppWindow properties
4. **Native Windows deps → managed cross-platform:** libwebp → SixLabors.ImageSharp
5. **XML/App.config → Uno.Extensions.Configuration:** the settings migration recipe
6. **Keyboard shortcuts:** KeyBinding → KeyboardAccelerator
7. **Fullscreen:** FullScreenPresenter pattern

Subsequent projects add to these sections and open new ones.

---

## 11. Campaign Deliverables

1. **"Your legacy WPF .csproj is not a blocker"** — SDK-style conversion walkthrough
2. **"Native Windows deps don't have to be dealbreakers"** — libwebp → ImageSharp story
3. **Before/after screenshots** across Windows, Web, Desktop
4. **First entry in MIGRATION-GUIDE.md** — the seed of the eventual `wpf-to-uno-migration` skill
