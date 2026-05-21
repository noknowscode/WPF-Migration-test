# Project Instructions — EventLookUno (WPF → Uno migration)

## Overview

This is a WPF-to-Uno-Platform migration of **EventLook**, originally a fast Windows Event Viewer alternative with multi-column sort, column-header filters, time-range filter, auto-refresh highlighting, and .evtx file import — built on **SDK-style .NET 8 WPF** (`net8.0-windows10.0.17763.0`).

**Source repo:** https://github.com/kmaki565/EventLook
**Source license:** MIT — author: kmaki565
**Migration plan:** See `docs/MIGRATION-PLAN.md`
**Migration log:** See `docs/MIGRATION-LOG.md`
**Campaign role:** Slot 2 of the unOS WPF Migration 5x campaign — extends `MIGRATION-GUIDE.md` with DataGrid, third-party-library, and Windows-only-API recipes.

## Architecture

- **Pattern:** MVUX (adopted from template). Source `MainViewModel` (manual `SetProperty` on `ObservableRecipient`) ports largely unchanged; adopted MVUX incrementally only where it earns its keep.
- **Navigation:** `blank` nav (single `MainPage`). Secondary windows (Detail, LogPicker, Settings, About) reimplemented as `ContentDialog` rather than separate `Window` instances — works on every head including WASM.
- **DI:** `Microsoft.Extensions.DependencyInjection` via `Uno.Extensions.Hosting` (template-provided). `IEventSource` + `IDataService` registered per-TFM.
- **Configuration:** `appsettings.json` + `Uno.Extensions.Configuration` + `IWritableOptions<EventLookSettings>` — replaces `Properties.Settings` / `user.config`.
- **Data:** Read-only — `EventLogReader` wrapper on Windows, mock/JSON-backed source elsewhere.

## Original WPF Architecture

- **Pattern:** MVVM via `CommunityToolkit.Mvvm` (manual `SetProperty` + `Messenger`, **no** source generators). `MainViewModel` (766 LoC) + 3 secondary ViewModels (Detail, LogPicker, Settings).
- **Key libraries:** `CommunityToolkit.Mvvm` 8.4, `Extended.Wpf.Toolkit` 4.7.25104 (DateTimePicker ×2, **CheckComboBox** ×2).
- **Data access:** `System.Diagnostics.Eventing.Reader` — `EventLogReader`, `EventLogQuery`, `EventLogWatcher`, `EventRecord` (all Windows-only).
- **Database:** None. Reads live Windows Event Log channels or `.evtx` files.
- **Platform dependencies (Windows-only):**
  - `System.Diagnostics.Eventing.Reader` — entire event-reader subsystem
  - `ProcessHelper.IsElevated` — WindowsIdentity check
  - `Process.Start("eventvwr.exe")` — Launch Windows Event Viewer
  - `Registry` access (via `Microsoft.Win32`) — some helpers
- **Standard .NET settings:** `Properties.Settings` (`ApplicationSettingsBase`), 6 user-scope keys backed by `user.config`. Window Width/Height **two-way bound** — round-trips on every resize.
- **Secondary projects:** `AboutBoxWpf` (shared UserControl library), `EventLookPackage` (MSIX wapproj), `Tests` (xUnit).

## Target architecture (Uno Platform)

- **Single Project multi-target:** `net10.0-windows10.0.26100;net10.0-browserwasm;net10.0-desktop`
- **DataGrid:** `CommunityToolkit.WinUI.Controls.DataGrid` (NuGet). Replaces WPF's built-in `DataGrid`.
- **Sort + filter:** `AdvancedCollectionView` from `CommunityToolkit.WinUI.Collections`. Replaces `CollectionViewSource.Filter` event + `ICollectionView.SortDescriptions`.
- **`EventRecord` → `EventEntry` POCO**: flattens the Windows-only `EventRecord` fields (RecordId, Id, Level, ProviderName, TimeCreated, Message, Xml) into a cross-platform record. `WindowsEventSource` produces these by adapting `EventRecord` internally; `MockEventSource` produces them directly from canned JSON.
- **`IEventSource` abstraction:** `#if WINDOWS` → `WindowsEventSource`; otherwise `MockEventSource`. Registered via DI in `App.xaml.cs`.
- **CheckComboBox:** custom composite UserControl — `DropDownButton` + `Flyout` hosting a `ListView` of check-boxed items, with selected-items summary text. Used by Provider and Level filters.
- **DateTimePicker:** composite `CalendarDatePicker` + `TimePicker` UserControl.
- **Secondary windows → `ContentDialog`:** Detail, LogPicker, Settings, About. Each dialog binds to its own ViewModel; the MainViewModel opens them via a `IDialogService` rather than `IShowWindowService<TViewModel>`.
- **Multi-binding / `IMultiValueConverter`:** **not supported in WinUI** — the two status-bar `MultiBinding` converters are rewritten as computed properties (`StatusText`, `FilterInfoText`) on `MainViewModel`.
- **`Style.Triggers` / `DataTrigger`:** **not supported in WinUI** — the refresh-button style trigger becomes template-selector-driven; the new-loaded row highlighting uses `DataGrid.LoadingRow` + a background converter; the provider-column truncation uses `x:Bind` on a viewmodel boolean.
- **Keyboard:** `KeyboardAccelerator` on `Page` replaces `Window.InputBindings` (F5/Shift+F5/Ctrl+L/Ctrl+O/Ctrl+S).
- **Settings:** `IWritableOptions<EventLookSettings>` with debounced writes for WinWidth/WinHeight round-trip.

## Project Structure

- `EventLookUno/` — Uno Platform project root (single-project multi-target)
- `EventLookUno/Models/` — `EventEntry`, filter classes (ported), `EventLookSettings` record
- `EventLookUno/Services/` — `IEventSource`, `WindowsEventSource` (Windows-only), `MockEventSource`, `IDialogService`
- `EventLookUno/Presentation/` — `MainPage.xaml`, secondary `ContentDialog`s, ViewModels
- `EventLookUno/Controls/` — custom composite controls (`CheckComboBox`, `DateTimePickerComposite`)
- `EventLookUno/Converters/` — `EventLevelToImageConverter`, `EventLevelToDisplayTextConverter`, etc.
- `EventLookUno/Strings/en/` — Localization resources (`Resources.resw`)
- `EventLookUno/Platforms/` — Desktop + WebAssembly host projects
- `docs/` — `MIGRATION-PLAN.md`, `MIGRATION-LOG.md`, `ARCHITECTURE.md`, `DESIGN-BRIEF.md`, `INTERACTION-SPEC.md`

## Migration Conventions

- Migrate business logic (Models, Services, ViewModels) first — expect minimal changes (namespace + flatten `EventRecord` → `EventEntry`).
- Migrate XAML views last — this is where most changes live (DataGrid, dialogs, CheckComboBox).
- **Flatten `EventItem.Record.X` → `EventEntry.X`** at the model layer, not in the XAML. Every filter predicate and every binding stops referencing `EventRecord` directly.
- Replace `{Binding}` with `x:Bind` where feasible for the new code; keep `{Binding}` in mechanical ports.
- **`CollectionViewSource` + `FilterEventArgs` → `AdvancedCollectionView.Filter = predicate`.** The filter base class contract changes shape — log the diff in MIGRATION-LOG.md.
- **`MultiBinding` → computed property on ViewModel.** WinUI has no `IMultiValueConverter`.
- **`Style.Triggers` / `DataTrigger` → VSM / template-selector / code-behind.** WinUI styles are setter-only.
- **`ContextMenu` / `RelativeSource FindAncestor` → `MenuFlyout` + `x:Bind` or `ElementName` binding.** FindAncestor doesn't exist in WinUI.
- **Search Uno Platform docs via MCP (`uno_platform_search`, `uno_platform_fetch`) before assuming API usage or patterns.**
- **Log every migration decision, gotcha, and workaround in `docs/MIGRATION-LOG.md`.** This log feeds section updates in `MIGRATION-GUIDE.md` at session end.

## Control Mapping Reference (Slot 2 scope)

| WPF Control / API | Uno/WinUI Equivalent | Notes |
|---|---|---|
| `Window` (WinWidth/Height two-way-bound) | `Page` + `AppWindow` size round-trip to `IWritableOptions` | Structural change |
| `DataGrid` (WPF built-in) | `controls:DataGrid` from `CommunityToolkit.WinUI.Controls.DataGrid` | NuGet add; ~95% API parity |
| `DataGridTextColumn` / `DataGridTemplateColumn` | Same names, slightly different property surface | Direct equivalent |
| `CollectionViewSource` + `Filter` event | `AdvancedCollectionView` + `Filter` predicate | From `CommunityToolkit.WinUI.Collections` |
| `DataGrid.ContextMenu` w/ `RelativeSource FindAncestor` | `DataGrid.RowDetailsTemplate` or row-level `FlyoutBase.AttachedFlyout` with `x:Bind` | FindAncestor doesn't exist in WinUI |
| `Style.Triggers` / `DataTrigger` on `DataGridRow` | `DataGrid.LoadingRow` event + row background binding | Styles are setter-only in WinUI |
| `MultiBinding` + `IMultiValueConverter` | Computed property on ViewModel | WinUI has no `IMultiValueConverter` |
| `xctk:DateTimePicker` (Extended.Wpf.Toolkit) | Composite `CalendarDatePicker + TimePicker` UserControl | Custom control |
| `xctk:CheckComboBox` | Composite `DropDownButton + Flyout(ListView)` UserControl | Custom control |
| `Menu` / `MenuItem` (WPF) | `MenuBar` / `MenuBarItem` / `MenuFlyoutItem` | Near-equivalent |
| `StatusBar` / `StatusBarItem` | `Grid` row at bottom of page | No WinUI equivalent |
| `Window.InputBindings` + `KeyBinding` | `Page.KeyboardAccelerators` + `KeyboardAccelerator` | Preferred over code-behind KeyDown |
| `MouseBinding Gesture="LeftDoubleClick"` | `DoubleTapped` event on row | Direct event |
| `AllowDrop` / `PreviewDragOver` / `Drop` | `AllowDrop` + `DragOver` + `Drop` | Cross-platform supported |
| `System.Diagnostics.Eventing.Reader.EventLogReader` | `IEventSource` interface + `WindowsEventSource` impl (`#if WINDOWS`) | Level-2 platform abstraction (see MIGRATION-GUIDE §6) |
| `Process.Start("eventvwr.exe")` | Inline `OperatingSystem.IsWindows()` gate | Level-1 platform abstraction |
| `WindowsIdentity.GetCurrent().IsInRole(Admin)` | `#if WINDOWS` + `OperatingSystem.IsWindows()` gate | Windows-only; returns `false` elsewhere |
| `Properties.Settings.Default.X` / `user.config` | `IWritableOptions<EventLookSettings>` backed by `appsettings.json` | Per Slot 1 recipe, extended for two-way Window size |
| `ContextMenu` + `RelativeSource FindAncestor` | `MenuFlyout` + `x:Bind` or `ElementName` + passing Page's DataContext | WPF-only binding idiom |
| `CommunityToolkit.Mvvm.Messaging.Messenger` | **Unchanged** — works on all Uno heads | Cross-platform |
| `System.Windows.Clipboard` | `Windows.ApplicationModel.DataTransfer.Clipboard` + `DataPackage` | Namespace + API change |
| `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` | `FileOpenPicker` / `FileSavePicker` + `InitializeWithWindow.Initialize` on WinAppSDK | Async picker |

## Key References

Before starting any migration work, read these first:

- `docs/MIGRATION-PLAN.md` — phased migration strategy and control inventory (7 phases, ~10-14 hrs reassessed from 6-10)
- `docs/MIGRATION-LOG.md` — running log of changes, gotchas, and patterns
- `docs/ARCHITECTURE.md` — target system architecture, layers, dependencies
- `docs/DESIGN-BRIEF.md` — design language, spacing, color tokens, component patterns
- `docs/INTERACTION-SPEC.md` — state model, user flows, component states
- **`UnoVault/08-Campaigns/Active/WPF-Migration/migrations/MIGRATION-GUIDE.md`** — cumulative cross-slot guide (this project extends §2, §4, §5, §6, §8)
- Source reference (read-only): `../EventLook-source/`

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

Always run `dotnet build` after changes to confirm the project still compiles. The uno-app MCP server (launched via `dotnet dnx uno.devserver --mcp-app`) is used for automated UI smoke-testing at phase end.

## Known-benign warnings in the `unoapp --preset recommended` baseline

As of Uno.Sdk 6.5.31 on .NET 10.0.200 (Slot 1 baseline, confirmed Slot 2 / 2026-04-21), a clean scaffold produces these warnings. **Do not waste context re-diagnosing them — they are documented and benign:**

| Warning | Count | Source | Mitigation |
|---|---|---|---|
| `NU1903` on `System.Security.Cryptography.Xml 10.0.2` (GHSA-37gx-xxp4-5rgx, GHSA-w3x6-4m5h-cxqf) | 2× per TFM | Transitive (config/serialization stack) | Override to **10.0.6** in `Directory.Packages.props` + explicit `<PackageReference>` in the csproj |
| `NU1903` on `Tmds.DBus.Protocol 0.21.2` (GHSA-xrw6-gwf8-vvr9) | 1× per TFM | Transitive (Skia Desktop Linux) | **No patched version exists** — suppress via `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-xrw6-gwf8-vvr9" />` with a comment explaining why |
| `NETSDK1198` stale `win-AnyCPU.pubxml` publish-profile reference | 1 | Scaffold artifact | Cosmetic; delete reference or ignore |
| `Uno0001` NotImplemented on `KeyRoutedEventArgs.KeyStatus` (WASM only) | 1 if app uses Alt-modifier detection | Uno WASM limitation | Wrap usage in try/catch or degrade feature on WASM |
| `Uno0001` NotImplemented on `Application.Exit()` (WASM only) | 1 if app offers explicit quit | Browser tabs can't self-close | try/catch |

After applying all mitigations above, a clean Slot 2 build should produce **0 errors and ~3 residual warnings** (Uno0001 WASM NotImplementeds + stale publish profile).
