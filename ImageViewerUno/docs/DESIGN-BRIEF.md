# Design Brief

## Theme

- **Base:** Uno Material (Material Design 3) — template-applied
- **Color mode:** Dark-first (source app uses a black background by default, full-bleed image on black frame)
- **Primary color:** Uno Material default — not foregrounded, app is image-first
- **Secondary color:** Uno Material default
- **Accent usage:** Status message text color (source default Green, user-configurable via `MsgColor` setting)

## Original vs. Migrated Visual Identity

The source app is visually minimal by design: fullscreen black canvas, a single image stretched `Uniform` with `DownOnly` stretch direction, and two near-invisible `Label` overlays:
- `Label1` (bottom) — filename / status text, with `DropShadowEffect` on the text
- `Label2` (center) — help/hint overlay with `Azure` + 0.6 opacity background

**Migrated target:** keep the same minimalist visual identity. The app is about showing other people's images, not its own chrome. Apply Uno Material only to the zero-or-one place we might add a toolbar (we won't, for v1).

Before/after screenshots: captured in `docs/screenshots/` at end of Phase 7.

## Typography

- Use Uno Material default type scale — the source app only uses text in the two status overlays (`FontSize="14"` on `Label2`, default on `Label1`).
- No custom type hierarchy needed.

## Spacing and Layout

- Follow Material Design 3 spacing tokens for any non-image UI.
- Image canvas is edge-to-edge — no margins, no gutters. Spacing tokens apply only to the status-overlay labels (`Padding="20,20,20,20"` on `Label2` is preserved).
- Layout is inherently fluid (fullscreen image adapts to window size) — no responsive breakpoints needed.

## Component Patterns

- **Image frame** — `Border` + `UserControl` + child `Image`. All mouse/wheel gestures hit `Grid1` (the outermost container); drag + zoom math translates the `Image` via `RenderTransform`.
- **Status overlay** — `TextBlock` (was `Label`) with `DropShadowEffect`, absolutely positioned bottom-center. Fades via `Visibility` + dispatcher timer. Consider a simple `Storyboard` fade-out for v2 polish.
- **Help overlay** — centered `TextBlock` with semi-opaque background, shown/hidden by key press. Content is static — no DataTemplate needed.

No custom controls migrate forward — `AnimatedWebpElement` is deleted in favor of a stock `Image` + managed codec.
