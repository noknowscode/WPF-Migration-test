# Migration Plan: EventLook → Uno Platform

**Source:** [github.com/kmaki565/EventLook](https://github.com/kmaki565/EventLook)
**Author:** kmaki565
**License:** MIT
**Complexity:** LOW-MEDIUM (Slot 2)
**Estimated effort:** 6-10 hours
**Target:** Uno Platform Single Project (Uno.Sdk latest stable)

---

## 1. Project Overview

A fast, handy Windows Event Viewer alternative. Displays event log entries in a `DataGrid` with multi-column sort, column-header filtering, time-range filtering, and auto-refresh. Actively maintained — 125+ stars, latest release Feb 2026. Uses `CommunityToolkit.Mvvm` 8.4, `Extended.Wpf.Toolkit`, and `user.config` for settings.

**Why Slot 2:** EventLook introduces two migration patterns that the guide needs early: (1) `DataGrid` — WPF's most common control with no 1:1 WinUI equivalent, requiring `CommunityToolkit.WinUI.Controls.DataGrid`; (2) `Extended.Wpf.Toolkit` — a common third-party WPF UI library with no Uno equivalent, requiring control-by-control replacement. Also surfaces a Windows-only API (`System.Diagnostics.Eventing.Reader`) that forces a clean platform-abstraction pattern.

**Why this matters for the campaign:** Enterprise WPF apps lean heavily on DataGrid and third-party UI libraries. Resolving both in Slot 2 makes the guide immediately useful to real-world teams before they see the LOB hero story (Reservoom) in Slot 3.

---

## 2. Architecture and Dependency Review

### Current Architecture
- **Pattern:** MVVM via `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **Framework:** .NET 8 / WPF, `net8.0-windows10.0.17763.0`
- **Project type:** SDK-style .csproj — modern
- **NuGet:** `CommunityToolkit.Mvvm` 8.4.0, `Extended.Wpf.Toolkit` 4.7.x

### Key Components
| Component | Type | Migration Impact |
|-----------|------|-----------------|
| MainWindow.xaml + VM | Shell, primary DataGrid view | DataGrid swap is the headline change |
| Model/ (EventItem, etc.) | Data records | Zero changes |
| ViewModel/ | CommunityToolkit.Mvvm classes | Near-zero changes |
| EventLogReader / sources | `System.Diagnostics.Eventing.Reader` wrappers | **Windows-only, needs abstraction** |
| AboutBoxWpf (sibling proj) | About dialog | Fold into main app |
| user.config settings | Persistence | Migrate to `IWritableOptions` |
| `Extended.Wpf.Toolkit` controls | Various (watermarks, pickers) | Replace per control |

### Dependency Analysis
- **CommunityToolkit.Mvvm:** already cross-platform — zero changes
- **Extended.Wpf.Toolkit:** WPF-only — must be replaced control-by-control
- **System.Diagnostics.Eventing.Reader:** Windows-only — abstract behind interface with platform impl
- **MSIX packaging project (`EventLookPackage`):** Windows-specific — repurpose for Uno Windows target

---

## 3. UI and Control Inventory

| WPF Control | Count | Uno/WinUI Equivalent | Migration Effort |
|-------------|-------|---------------------|-----------------|
| Window | 1 | Page + Shell | LOW |
| **DataGrid** (with sort + filter) | 1 | **`CommunityToolkit.WinUI.Controls.DataGrid`** | **MEDIUM — the centerpiece** |
| DataGrid column filters | Multiple | Custom `MenuFlyout` on column headers | MED |
| Extended.Wpf.Toolkit: Watermark TextBox | Several | WinUI `TextBox.PlaceholderText` | LOW |
| Extended.Wpf.Toolkit: DateTimePicker | 2 | `CalendarDatePicker` + `TimePicker` composite | MED |
| Extended.Wpf.Toolkit: BusyIndicator | 1 | `ProgressRing` + overlay | LOW |
| ComboBox | Multiple | ComboBox | ZERO |
| MenuBar | Maybe | MenuBar | LOW |
| StatusBar | 1 | Custom Grid (no WinUI StatusBar) | LOW |

### DataGrid Migration Specifics
The `CommunityToolkit.WinUI.Controls.DataGrid` API is ~95% compatible with WPF's DataGrid but differs in:
- Column definitions — same names, slightly different property surface
- Row details templates — works
- Filtering: WPF apps usually use `CollectionViewSource` + `ICollectionView.Filter` — works in WinUI with `AdvancedCollectionView` from CommunityToolkit
- Sort: same concept, different API surface
- Virtualization: enabled by default in both; behaves the same

---

## 4. Platform/API Usage Review

| API/Feature | Used? | Uno Support | Action Required |
|-------------|-------|-------------|----------------|
| `System.Diagnostics.Eventing.Reader` | Yes | **Windows only** | **Abstract behind `IEventSource` interface** |
| MSIX packaging | Yes | N/A — Uno Windows target handles it | Replace `EventLookPackage` project |
| `user.config` settings | Yes | N/A | Migrate to `Uno.Extensions.Configuration` + `IWritableOptions` |
| Clipboard | Likely | Full | Namespace change |
| File export (CSV/TSV) | Maybe | Full (.NET) | None beyond picker |
| Window icon / taskbar | Yes | `AppWindow` | API change |
| Localization resources | Maybe | `.resw` / Uno localization | Minor |

### The Windows-only story

`EventLogReader` / `EventLogQuery` is inherently Windows-specific. Clean pattern:

```csharp
public interface IEventSource
{
    IAsyncEnumerable<EventEntry> ReadAsync(EventQuery query, CancellationToken ct);
}

// WindowsEventSource.cs — uses System.Diagnostics.Eventing.Reader
// MockEventSource.cs / JsonFileEventSource.cs — for WASM/Desktop platforms
```

This is exactly the "platform-specific code behind an interface" pattern the Uno docs recommend, and a recurring lesson for the rest of the series.

---

## 5. Likely Blockers

| Blocker | Severity | Mitigation |
|---------|----------|------------|
| DataGrid gap in WinUI | MED | `CommunityToolkit.WinUI.Controls.DataGrid` — well-maintained |
| Extended.Wpf.Toolkit controls | MED | Replace each control with WinUI/Uno Toolkit equivalent |
| `EventLogReader` Windows-only | MED | Abstract behind `IEventSource` with Windows impl + fallback |
| MSIX packaging project | LOW | Uno Windows target handles MSIX natively |
| user.config → appsettings | LOW | Well-understood pattern (already done in Slot 1) |
| Column-header filter UI | MED | Custom `MenuFlyout` with per-column state |

---

## 6. Migration Strategy

**Approach:** Fresh Uno project + copy code (same pattern as Slot 1)

Even though EventLook is already SDK-style, DataGrid and Extended.Wpf.Toolkit replacements touch so much XAML that a fresh scaffold is faster than in-place conversion.

---

## 7. Phased Implementation

### Phase 0: Setup and Guide Sync (10 min)
1. Copy starter kit
2. Customize `CLAUDE.md`
3. Copy this plan to `docs/MIGRATION-PLAN.md`
4. **Read `migrations/MIGRATION-GUIDE.md`** — Slot 1 patterns should already be documented; apply them
5. Initialize per-project MIGRATION-LOG

### Phase 1: Scaffold + Reuse Slot 1 Patterns (20 min)
1. `dotnet new unoapp -o EventLook.Uno --preset=recommended`
2. Apply namespace mapping from MIGRATION-GUIDE.md (already learned in Slot 1)
3. Apply settings pattern from MIGRATION-GUIDE.md
4. Build

### Phase 2: Model Layer + MVVM (30 min)
1. Copy `Model/` records
2. Copy ViewModels (CommunityToolkit.Mvvm — should port unchanged)
3. Build — **log in guide** any surprises around source generators across TFMs

### Phase 3: Event Source Abstraction (90 min) — **core platform lesson**
1. Define `IEventSource` interface
2. Implement `WindowsEventSource` (wraps original `EventLogReader` code, compiled only on Windows TFM via `#if WINDOWS`)
3. Implement `MockEventSource` returning canned events for WASM/Desktop
4. Register via DI in `App.xaml.cs`
5. **Log:** full pattern in MIGRATION-GUIDE.md — this is the reusable recipe for Windows-only APIs

### Phase 4: DataGrid Migration (120 min) — **core control lesson**
1. Add `CommunityToolkit.WinUI.Controls.DataGrid` NuGet
2. Port `<DataGrid>` XAML — column definitions
3. Port multi-column sort via `AdvancedCollectionView`
4. Rebuild column-header filter UI using `MenuFlyout` + per-column filter state
5. Port time-range filter
6. Port auto-refresh highlighting
7. **Log:** complete DataGrid migration recipe — the section of the guide enterprise readers will most care about

### Phase 5: Extended.Wpf.Toolkit Replacements (60 min)
1. Watermark TextBox → `TextBox.PlaceholderText`
2. DateTimePicker → `CalendarDatePicker` + `TimePicker` composite UserControl
3. BusyIndicator → `ProgressRing` + `Grid` overlay
4. **Log:** Extended.Wpf.Toolkit → WinUI mapping table for the guide

### Phase 6: Settings + Export + Polish (60 min)
1. user.config → `IWritableOptions` (reuse Slot 1 pattern from guide)
2. CSV/TSV export via FileSavePicker (async)
3. Status bar via custom Grid
4. Apply Uno Material theme

### Phase 7: Cross-Platform Test (45 min)
1. Windows target — verify full fidelity with live event log
2. WASM target — verify UI + mock event source
3. Desktop Skia — same
4. Screenshots, before/after

---

## 8. Expected Effort/Risk Areas

| Area | Effort | Risk | Notes |
|------|--------|------|-------|
| Scaffold + reuse Slot 1 patterns | 30 min | NONE | |
| Model + VM | 30 min | LOW | CommunityToolkit.Mvvm ports clean |
| IEventSource abstraction | 90 min | MED | First Windows-only abstraction in the series |
| DataGrid migration | 120 min | MED-HIGH | Most detailed lesson of the project |
| Extended.Wpf.Toolkit replacements | 60 min | MED | Per-control work |
| Settings + export | 30 min | LOW | Reuses Slot 1 pattern |
| Cross-platform testing | 45 min | LOW | |
| **Total** | **~6-8 hrs** | **LOW-MED** | |

---

## 9. Migration Log Checklist

- [ ] Project scaffolded, multi-target build green
- [ ] Slot 1 namespace + settings patterns applied from guide
- [ ] Model + VM port unchanged
- [ ] `IEventSource` abstraction defined
- [ ] Windows impl wraps original EventLogReader code
- [ ] Mock/fallback impl works on WASM + Desktop
- [ ] DataGrid swapped to CommunityToolkit.WinUI — renders rows
- [ ] Multi-column sort working via AdvancedCollectionView
- [ ] Column-header filtering reimplemented via MenuFlyout
- [ ] Time-range filter working
- [ ] Auto-refresh highlighting working
- [ ] Extended.Wpf.Toolkit controls all replaced
- [ ] Settings persisted via IWritableOptions on all platforms
- [ ] CSV/TSV export working
- [ ] Before/after screenshots per platform
- [ ] **MIGRATION-GUIDE.md updated with DataGrid + IEventSource + Extended.Wpf.Toolkit sections**

---

## 10. Contributions to MIGRATION-GUIDE.md

Slot 2 adds these new sections and extends existing ones:

**New:**
1. **DataGrid → CommunityToolkit.WinUI DataGrid:** XAML diff, filtering recipe, sort via AdvancedCollectionView, virtualization notes
2. **Third-party WPF UI library replacement:** Extended.Wpf.Toolkit → WinUI mapping table (reusable reference)
3. **Windows-only API abstraction:** IEventSource pattern, `#if WINDOWS` + platform DI registration
4. **AdvancedCollectionView vs CollectionViewSource:** when to use which

**Extended:**
- Settings migration: add `IWritableOptions` specifics (was generic in Slot 1)
- Build targets: add per-TFM conditional compilation patterns

---

## 11. Campaign Deliverables

1. **"WPF DataGrid → WinUI: The Complete Recipe"** — headline content for DataGrid-anxious enterprise readers
2. **"Extended.Wpf.Toolkit is gone. Here's what replaces it."** — mapping table as standalone reference
3. **"Windows-only APIs don't kill cross-platform"** — IEventSource pattern as a reusable template
4. **Before/after screenshots** across Windows, Web, Desktop
5. **Second cumulative update to MIGRATION-GUIDE.md** — the guide now covers baseline + DataGrid + third-party lib + platform abstraction
