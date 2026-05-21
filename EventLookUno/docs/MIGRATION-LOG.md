# Migration Log — EventLookUno

**Source:** EventLook (https://github.com/kmaki565/EventLook)
**Source license:** MIT — author: kmaki565
**Started:** 2026-04-21
**Status:** Phases 0–5 complete; 6–8 deferred as TODOs; 9 (guide writes) in progress
**Campaign slot:** 2 of 5 (unOS WPF Migration 5x)

---

## Log Format

Each entry captures:
- **Date/phase** — when it happened
- **What changed** — specific files, controls, patterns
- **Why** — the migration reason or blocker that triggered the change
- **Gotcha** — anything surprising or non-obvious
- **Pattern** — if this is a reusable migration pattern, tag it for promotion to `MIGRATION-GUIDE.md`

---

## Phase 0: Source Analysis (2026-04-21)

- Cloned `kmaki565/EventLook` to `../EventLook-source/`
- **SDK-style .NET 8 csproj**, `net8.0-windows10.0.17763.0`, `UseWPF=true`
- **File inventory:** 30 C# files (~3500 LoC), 4 XAML views, 10 converters in `View/Converter.cs`

**Discrepancies from the migration plan flagged for guide updates:**

1. **Plan said `[ObservableProperty]`/`[RelayCommand]` source generators → reality is manual `SetProperty`.** Source uses `ObservableObject`/`ObservableRecipient` base classes with hand-written setters.
2. **`EventItem.Record` holds a Windows-only `EventRecord` directly.** Model layer leaks `System.Diagnostics.Eventing.Reader.EventRecord` into the binding surface. Abstracting just the reader behind `IEventSource` isn't enough — must flatten `EventRecord` into a cross-platform POCO (`EventEntry`).
3. **Extended.Wpf.Toolkit usage wider than the plan.** Reality: `CheckComboBox` ×2 (Provider + Level filters, a compound multi-select with "select all"), `DateTimePicker` ×2. No WatermarkTextBox or BusyIndicator used.
4. **`IMultiValueConverter` / `MultiBinding` not supported in WinUI.** Status bar uses two MultiBinding converters; must rewrite as computed VM properties.
5. **`Style.Triggers` / `DataTrigger` not supported in WinUI.** Refresh-button style, DataGrid row `IsNewLoaded` highlight, provider-column truncation.
6. **`ContextMenu` with `RelativeSource FindAncestor`** — `FindAncestor` doesn't exist in WinUI. 12-item context menu needs rebuild.
7. **3 secondary windows** (Detail, LogPicker, Settings) via `IShowWindowService<TViewModel>`. Reimplement as `ContentDialog`.
8. **Window Width/Height `Mode=TwoWay` binding to `Properties.Settings`** — needs `IWritableOptions<T>` with debounced writes.
9. **xUnit `Tests/` project** — not in the plan. Deferred for v1.

**Complexity reassessment:** plan said LOW-MEDIUM / 6-10 hrs. Actual: **MEDIUM / 10-14 hrs**.

## Phase 1: Kickoff Decisions (2026-04-21)

- **Target heads:** Desktop + WASM + Windows (WinAppSDK) — 3 TFMs originally (later dropped to 2; see Phase 5)
- **`EventRecord → EventEntry` POCO flatten:** confirmed
- **Secondary windows:** all 4 (Detail, LogPicker, Settings, About) become `ContentDialog`; `AboutBoxWpf` folded in
- **`EventLookPackage` MSIX wapproj:** dropped
- **`Tests/` project:** dropped for v1
- **Event source strategy:** `WindowsEventSource` live reader + `MockEventSource` (JSON-backed)
- **Smoke-test:** Uno App MCP (`dotnet dnx uno.devserver --mcp-app`)

## Phase 2a: Scaffold (2026-04-21)

- `dotnet new unoapp -o . -n EventLookUno -preset recommended -tfm net10.0 -platforms desktop wasm windows -presentation mvux -markup xaml -theme material -di -config -log default -nav blank -id com.unoplatform.eventlook`
- Baseline: 0 errors, **13 warnings** (9× NU1903 + NETSDK1198). Matches Slot 1 baseline exactly.
- Commit: `bd28f56`

### Starter-kit + CLAUDE.md + MIGRATION-PLAN.md copy done.

### NuGet additions — **first DataGrid gotcha**
- **The plan's `CommunityToolkit.WinUI.Controls.DataGrid` package does not exist.** Microsoft's 8.x Community Toolkit never ported DataGrid to WinUI 3.
- **Correct package: `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid` 7.1.205** — Uno's cross-platform fork. Confirmed via `uno_platform_docs_search` + NuGet search.
- Added: `CommunityToolkit.Mvvm` 8.4.2, `CommunityToolkit.WinUI.Collections` 8.2.251219 (`AdvancedCollectionView`).
- **Vuln pins (Slot 1 recipe reused):** `System.Security.Cryptography.Xml 10.0.6` override + Tmds.DBus audit suppress → 13 warnings down to 1.

## Phase 2b: Models port (2026-04-21)

Ported 12 model files. Namespace + nullability + filter-predicate refactor.

**Key decisions:**
- `Models/EventEntry.cs` — new POCO flattening `EventRecord` fields (RecordId, Id, Level, ProviderName, TimeOfEvent, Message, Xml, LogSourcePath). `WindowsEventSource` adapts; `MockEventSource` produces directly.
- `Models/FilterBase.cs` — **decoupled from `CollectionViewSource`**. Exposes `Predicate<object> Match` for `AdvancedCollectionView.Filter`; filter subclasses fire `FilterUpdated` event to trigger `acv.RefreshFilter()` in the view.
- `Models/ProcessHelper.cs` — cross-platform with inline `OperatingSystem.IsWindows()` gates (Slot 1 §6 Level 1 pattern).
- `Models/EventLookSettings.cs` — 6-key typed record for `IWritableOptions<T>`.

**Gotcha logged:** `[ObservableProperty]` on a field raises **MVVMTK0045** (not AOT-compatible on WinRT/WinUI 3). Fix: use C# 13 partial-property syntax:
```csharp
[ObservableProperty]
public partial bool IsNewLoaded { get; set; }
```
Pattern for the guide: all ObservableProperty declarations must use partial-property form in Uno/WinUI 3 scenarios.

Build: 0 errors, 1 warning.

## Phase 3: IEventSource abstraction (2026-04-21)

- `Services/IEventSource.cs` — interface contract (ReadAsync, SubscribeEvents, IsValidEventLog, GetComputerNameFromEvtx, EnumerateChannelsAsync)
- `Services/WindowsEventSource.cs` — wraps `EventLogReader` + `EventLogQuery` + `EventLogWatcher`. Preserves the `WIN32ERROR_RPC_S_INVALID_BOUND` retry workaround verbatim from the source's `DataService.cs`.
- `Services/MockEventSource.cs` — 150 synthetic events across 9 providers; 5 mock channels for Log Picker. Deterministic (seeded RNG).
- `App.xaml.cs` — promoted `Host` + `MainWindow` to `public` (Slot 1 §17 gotcha). Added `.Section<EventLookSettings>()` to `UseConfiguration`. Conditional `IEventSource` DI registration.
- Commit: `7a4e48e`

**Gotcha for the guide:** `EventLookUno.Models.PathType` clashes with `System.Diagnostics.Eventing.Reader.PathType` by simple name. Fix: alias the Windows type at the service boundary:
```csharp
using WinPathType = System.Diagnostics.Eventing.Reader.PathType;
```
And fully-qualify the Models type at interface-impl method signatures:
```csharp
public bool IsValidEventLog(string path, EventLookUno.Models.PathType type) { ... }
```

## Phase 4: MainViewModel port (2026-04-21)

**Pragmatic scope call:** ported a DataGrid-focused MainViewModel (280 LoC) from the 800-line original. Deferred surface (tagged as explicit `TODO(phase-7/8)`):
- OpenFile → FileOpenPicker
- OpenLogPicker / OpenSettings / OpenDetails ContentDialogs
- CSV export
- Context menu
- Run as admin

What is included:
- `IEventSource` consumption (replacing `IDataService`)
- `AdvancedCollectionView` wiring: live-shaped, sort + filter. `FilteredEvents.Filter = ComposedFilter` (AND of all FilterBase.Match predicates).
- Filter subscription: `filter.FilterUpdated += (_,_) => acv.RefreshFilter()`.
- Time-range computation + `Progress<ProgressInfo>` event reporting.
- Auto-refresh highlight: `IsNewLoaded` POCO field + `DispatcherTimer` sweep every second.
- Computed `StatusText` / `FilterInfoText` replacing WPF `MultiBinding` converters.
- CommunityToolkit.Mvvm source-generator `[RelayCommand]` + partial-property `[ObservableProperty]`.

**Name-clash gotchas (aliased):**
- `EventLookUno.Models.Range` vs `System.Range` → `using TimeRange = EventLookUno.Models.Range;`
- `EventLookUno.Models.SourceFilter` vs `System.Diagnostics.SourceFilter` → `using ProviderFilter = EventLookUno.Models.SourceFilter;`

## Phase 5: DataGrid XAML migration (2026-04-21) — **THE HEADLINE**

### Material finding — **Uno.CommunityToolkit DataGrid is incompatible with the WinAppSDK TFM**

With `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.205` referenced:
- `net10.0-desktop` (Skia): **builds green**
- `net10.0-browserwasm` (Skia): **builds green**
- `net10.0-windows10.0.26100` (WinAppSDK): **build fails** with `MSB3073` (XamlCompiler.exe exit code 1, silent). The Microsoft XamlCompiler rejects the Uno-forked types because they target Uno's UWP-compatibility surface, not WinAppSDK's `Microsoft.UI.Xaml`.

**Decision for Slot 2:** drop the WinAppSDK TFM. Target `net10.0-desktop;net10.0-browserwasm` only. Rationale:
- The source app is Windows-only; by supporting Desktop + WASM we're already ahead.
- Skia Desktop runs on Windows, so Windows users get the full app including live event log via `WindowsEventSource` (now runtime-gated with `OperatingSystem.IsWindows()` instead of compile-time `#if WINDOWS`).
- No currently-published DataGrid package fits the "WinAppSDK-native + cross-Uno-head" intersection. `WinUI.TableView 1.4.1` is WinAppSDK-only (opposite problem).

**Impact:** No user-facing impact on Windows. Developer-facing: the Uno app on Windows uses the Skia host, not WinAppSDK. Solo-dev tooling (VS/VS Code hot-reload, Uno DevServer) supports this.

**Consequence refactor:**
- Removed `#if WINDOWS` guards from `WindowsEventSource.cs`, `ProcessHelper.cs`, and `App.xaml.cs`.
- `[SupportedOSPlatform("windows")]` attribute on the Windows impl class.
- Runtime `OperatingSystem.IsWindows()` branch in `ConfigureServices` to pick `WindowsEventSource` vs `MockEventSource`.

### DataGrid XAML specifics

- XAML namespace: `xmlns:controls="using:CommunityToolkit.WinUI.UI.Controls"`
- `ItemsSource="{Binding FilteredEvents}"` bound to `AdvancedCollectionView`
- `CanUserSortColumns="True"` + column `SortMemberPath` on WPF → **`SortMemberPath` doesn't exist on the Uno fork's `DataGridColumn`**. Columns with a direct `Binding="{Binding X}"` sort by binding path automatically; `DataGridTemplateColumn` (no Binding) needs a header-click handler + explicit `ACV.SortDescriptions` mutation (TODO phase 8).
- `LoadingRow` code-behind event is how WinUI DataGrid exposes per-row styling at render time — replaces WPF `<Style TargetType="DataGridRow"><Style.Triggers><DataTrigger Binding="{Binding IsNewLoaded}" Value="True">…</DataTrigger>` (WinUI styles are setter-only).

### Other gotchas from Phase 5

- **`bool → Visibility` does not auto-convert** in WinUI like WPF. `Visibility="{Binding Bool}"` compiles silently but throws at runtime. Fix: explicit `BoolToVisibilityConverter` in `Page.Resources`, or bind a `Visibility` property on the VM.
- **`Color.FromArgb` lives in `Windows.UI.Color`**, whereas `Microsoft.UI.Colors` is a separate static class of named colors. Need both: `using Microsoft.UI;` + `using Windows.UI;`.
- **WinAppSDK XamlCompiler errors are frequently silent** — exit code 1 with an empty `output.json` `MSBuildLogEntries` log. When stuck, simplify the XAML progressively until the parser accepts it; the package TFM compatibility is almost always the real issue. Don't waste time re-reading your XAML for syntax bugs.

Commit: `6983337`

## Phase 6–8: Deferred (2026-04-22)

Intentionally deferred to a follow-up session to keep Slot 2 focused on the DataGrid + IEventSource recipes. All have explicit `TODO(phase-X)` markers in the code. Deferred surface:

- **Phase 6** — CheckComboBox composite control (Provider + Level filters). Current v1: uses MenuFlyout pattern on column headers instead. Provider/Level filter collections (`SourceFilter`/`LevelFilter`) are populated and bound; just no custom dropdown UI yet.
- **Phase 7** — secondary dialogs (Detail, LogPicker, Settings, About) as `ContentDialog`. Commands stub-disabled in the MenuBar; hitting them is a no-op.
- **Phase 8** — `IWritableOptions<EventLookSettings>` round-trip for WinWidth/Height/ShowsRecordId/etc. CSV export via `FileSavePicker`. Context menu (12 items) via `MenuFlyout`. 10-converter port.

Everything above the DataGrid recipe — i.e. the headline deliverable — is shipped. Remaining phases are polish that doesn't change the guide contributions.

## Phase 9: MIGRATION-GUIDE writes (2026-04-22)

This is the main deliverable. Extending `MIGRATION-GUIDE.md` with Slot 2 contributions — see commit message in the UnoVault repo.

---

## Gotchas Summary (Slot 2)

| Issue | Workaround | Root Cause | In Guide |
|---|---|---|---|
| `CommunityToolkit.WinUI.Controls.DataGrid` does not exist — Microsoft 8.x never ported DataGrid to WinUI 3 | Use `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.205` (Uno fork of WCT 7.x) | Microsoft abandoned DataGrid at WinUI 2 | §4 |
| Uno DataGrid fork incompatible with WinAppSDK TFM (MSB3073 silent exit 1) | Drop WinAppSDK TFM; target Desktop + WASM; use Skia Desktop on Windows + runtime `OperatingSystem.IsWindows()` gating | Uno fork targets Uno's UWP-compat surface, not Microsoft's WinAppSDK | §4, §6, §17 |
| `[ObservableProperty]` on a field → MVVMTK0045 (not AOT-compatible on WinRT/WinUI 3) | C# 13 partial-property syntax: `[ObservableProperty] public partial bool X { get; set; }` | CsWinRT marshalling requires partial-property form | §2, §17 |
| `IMultiValueConverter` / `MultiBinding` don't exist in WinUI | Computed property on ViewModel; raise PropertyChanged when dependencies change | WinUI binding engine doesn't implement the WPF MultiBinding shape | §2 |
| `Style.Triggers` / `DataTrigger` don't exist in WinUI | Styles are setter-only. Use VSM for state-driven, `DataGrid.LoadingRow` for row-level, template-selector for item-level, code-behind for single triggers | WinUI styles have no trigger concept | §2, §4 |
| `RelativeSource FindAncestor` doesn't exist in WinUI | `x:Bind` or `ElementName` binding; pass Page's DataContext explicitly | FindAncestor not implemented | §2 |
| `bool → Visibility` doesn't auto-convert in WinUI | Explicit `BoolToVisibilityConverter` or VM-side Visibility property | WPF had an implicit converter; WinUI doesn't | §2 |
| `SortMemberPath` doesn't exist on the Uno fork's `DataGrid*Column` | Columns with direct `Binding=…` sort by path; `TemplateColumn` needs a header-click handler mutating `ACV.SortDescriptions` | WCT 7.x API shape | §4 |
| `Color.FromArgb` in `Windows.UI`, `Colors` in `Microsoft.UI` | `using Microsoft.UI;` + `using Windows.UI;` | Two separate color surfaces in WinUI 3 | §2 |
| `Models.Range` clashes with `System.Range` (C# 8 indexing); `Models.SourceFilter` clashes with `System.Diagnostics.SourceFilter`; `Models.PathType` clashes with `System.Diagnostics.Eventing.Reader.PathType` | `using TimeRange = EventLookUno.Models.Range;` aliases at top of the offending file | Common short type names clash with BCL surface | §2 |
| `IL2026` — AdvancedCollectionView uses reflection, warns on trimming | Cosmetic for v1; mitigate in Polish with `[DynamicallyAccessedMembers]` on Event types | Known ACV limitation | §17 |
