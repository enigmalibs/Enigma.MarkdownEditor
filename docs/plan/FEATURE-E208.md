# FEATURE-E208 — Icon set that fills the frame

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md`
**Branch:** `feature/feature-e208-icon-fills-frame`, cut from the run branch at build time.
**Run:** feature/2026-11-04-icons-release-1-12

## Objective

Replace the application icon with the new set in `C:\Dev\EnigmaLibs\Branding\Apps\final3\markdown-editor`
(an `.ico`, ten PNG frames, two SVG masters). Update every place the repository uses it: the taskbar and
window icon, the `.exe`, the About dialog's image, the MSI's shortcuts and Control Panel icon, and the
Linux installer's icons.

## Context & constraints

- **The new artwork** is FEATURE-8156's tile-less *MD-A2 Outline* mark (blue outlined `M↓` box, green
  arrow, *Soft* palette `#6A9BF4` / `#63C77D`), **scaled by 1.1852 and centred so it fills the frame**:
  its longest side spans 16 to 240 of the 256-unit view box. The small master (16–32 px) is redrawn on
  the same live area, its straight edges on the 16-unit grid. `Branding/Apps/export.py final3` generated
  the set, as the SVG header comments say.
- **The new `.ico`** holds ten 32-bit PNG frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256). Each frame
  is byte for byte the matching `png/enigma-markdown-editor-N.png` (checked while planning).
- **Where the icon lives** (FEATURE-EA38, FEATURE-7CE4, FEATURE-8156):
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico`: `<ApplicationIcon>`, `MainWindow.axaml` and
    `StartupErrorWindow.axaml` `Icon=`, the `app.ico` copied beside the executable, and through it
    `buildmsi.ps1`'s shortcut and Control Panel icon;
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`: the About dialog's `Image`;
  - `packaging/linux/icons/enigma-markdown-editor-{16,32,48,64,128,256}.png`: what `install.sh`
    installs, `uninstall.sh` removes and `pack-linux.ps1` packs;
  - `packaging/icon/enigma-markdown-editor.svg` and `-small.svg`: the masters.
- `AppIconTests` already ties the About PNG and each Linux PNG to the `.ico`'s frame of its size, byte
  for byte, and checks the copy beside the executable. A partial swap fails it.
- **Prose describing the mark**: `docs/RELEASE.md` §1's icon check. It describes the tile-less mark,
  which still holds; it gains the frame-filling size and this item's ID.
- The source directory is outside the repository and is left in place.

## Steps

1. Copy, byte for byte:
   - `enigma-markdown-editor.ico` → `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico`;
   - `png/enigma-markdown-editor-256.png` → `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`;
   - `enigma-markdown-editor.svg` and `enigma-markdown-editor-small.svg` → `packaging/icon/`;
   - `png/enigma-markdown-editor-{16,32,48,64,128,256}.png` → `packaging/linux/icons/`.
2. `docs/RELEASE.md` §1's icon check: say the mark now fills the frame (so it reads larger at the same
   size), and add `FEATURE-E208` to what is left open.
3. Release build; full suite.

## Acceptance criteria

- Every destination holds its new file byte for byte (`cmp`); no previous-set frame is left.
- `AppIconTests` passes (ten facts/rows); `AboutView`'s resolve test still passes.
- Release build: 0 warnings, no `AVLN`. The full suite shows no new failure against the run's baseline.

## Out of scope

- Changing the Linux installer's sizes, adding a `scalable/apps` SVG, or rendering an SVG in-app.
- Editing the artwork.
- Release notes and version (FEATURE-721C).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which files | `app.ico`, `app-256.png`, the six Linux PNGs, both SVGs | "taskbar/window, about, installers, etc."; the set has FEATURE-8156's shape, and the MSI takes its icon from `app.ico` | Re-packing the `.ico` |
| File names | Existing names kept | Every reference (csproj, views, scripts, `buildmsi.ps1`) stays valid | Renaming |
| New tests | None | `AppIconTests` already pins every copy to the `.ico`; a new fact would add nothing a swap can break | A test pinning the frame-filling geometry (needs image decoding; no Skia in the suite) |
| SVG header comments | Verbatim | `export.py` writes them; an edit would be lost on the next export | Rewording |
| Extra Linux sizes (20/24/40/96) | Not added | Three scripts would move in step for no request | Extending `ICON_SIZES` |
| The source directory | Left in place | The Branding repository's output | Deleting it |

## Beyond-the-draft checklist

| Dimension | Resolution |
|---|---|
| Security & auth | N/A: static art |
| Input validation & abuse | N/A: no input |
| Performance & scalability | `app.ico` 9.9 KB → 12.7 KB; nothing else moves |
| Concurrency | N/A |
| Error handling & resilience | A broken or partial copy fails `AppIconTests` |
| UX edge cases | The larger mark on dark and light taskbars, at 16 px, and on both About themes: the RELEASE.md §1 visual check |
| Accessibility & i18n | N/A: no text; contrast is part of the visual check |
| Observability | N/A |
| Data migration & compatibility | None: same paths; settings, workspaces and sessions untouched |
| Deployment & operations | Installer, packer and MSI keep their paths. Windows' icon cache may show the old icon until refreshed (already in RELEASE.md) |
| Licensing & dependencies | The user's own artwork; no dependency |
| Testing | `AppIconTests` (unchanged, ten facts/rows) plus the About resolve test |
| Documentation | `docs/RELEASE.md` §1; release notes in FEATURE-721C |
