> **Moved to uno-lab.** This project now lives in the `WPF-Migration-test/` folder of [noknowscode/uno-lab](https://github.com/noknowscode/uno-lab/tree/main/WPF-Migration-test) (private). This repository is archived and kept for its history.

# WPF-Migration-test

Parent repo hosting test projects from the **unOS WPF → Uno Platform migration campaign**.

Each subfolder is a self-contained Uno Platform project derived from a real WPF source app. Every slot exercises a different set of migration recipes (DataGrid, third-party controls, Windows-only APIs, custom rendering, P/Invoke replacement, etc.) and contributes findings back to the cumulative `MIGRATION-GUIDE.md`.

## Layout

```
WPF-Migration-test/
├── ImageViewerUno/   Slot 1 — image viewer migration (libwebp P/Invoke → SixLabors.ImageSharp, codec abstraction, Skia Desktop)
└── EventLookUno/     Slot 2 — Windows Event Viewer alternative (DataGrid, CheckComboBox, EventLogReader, IEventSource abstraction)
```

Each slot is self-contained: its own `.sln`, `Directory.Build.props`, `docs/`, `appsettings.json`, etc. There is no shared MSBuild config at the parent level — open each subfolder as its own solution.

## Working in a slot

```bash
cd ImageViewerUno   # or EventLookUno
dotnet build
dotnet build -f net10.0-windows10.0.26100
dotnet build -f net10.0-browserwasm
dotnet build -f net10.0-desktop
```

See each slot's `CLAUDE.md` and `docs/MIGRATION-LOG.md` for slot-specific architecture, conventions, and migration findings.
