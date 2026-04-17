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

---

## Recurring Patterns Observed

| Pattern | Description | Frequency | Reusable? |
|---------|-------------|-----------|-----------|
| _(populated during migration phases)_ | | | |

---

## Gotchas and Workarounds

| Issue | Workaround | Root Cause | Reported? |
|-------|-----------|------------|-----------|
| Plan underestimated native-dep count (missed `shlwapi` + `gdi32` in addition to `libwebp`) | Log as "native deps travel in packs" — audit *every* `DllImport` in a WPF project during Phase 0 analysis | Migration plan derived from high-level inspection | Yes — will add to MIGRATION-GUIDE Section 7 |
| `MediaFileServer` `HttpListener` usage not documented in source README | Delete entire service — not needed in WinUI | Purely a WPF `MediaElement` workaround | Yes — will add to MIGRATION-GUIDE Section 4 |
| `MainWindow` has overloaded constructors for dialog-mode library use | Port signatures verbatim; document `.ShowDialog()` caller pattern has no `Page` equivalent | WPF Windows double as top-level containers and dialog-capable objects | Yes — will add to MIGRATION-GUIDE Section 3 |

---

## What Was Easy

_(populated during migration phases)_

## What Was Difficult

_(populated during migration phases)_

## Where WPF Design Helped

_(populated during migration phases)_

## Where WPF Design Hurt

_(populated during migration phases)_
