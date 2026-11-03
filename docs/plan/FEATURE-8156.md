# FEATURE-8156 — New icon set without the tile

**Status:** TODO
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md`
**Branch:** `feature/feature-8156-new-icon-set`, cut from the run branch at build time.
**Run:** feature/2026-11-03-icons-msi-release

## Objective

Replace the application icon with the new set in `C:\Dev\EnigmaLibs\Branding\Apps\final2\markdown-editor`
(an `.ico`, ten PNG frames, two SVG masters). Update every place the repository uses it:

- the executable and window icon;
- the About dialog's image;
- the Linux installer's icons;
- the SVG masters, which the repository now carries like every sibling Enigma app.

## Context & constraints

- **The new artwork** is FEATURE-EA38's drawing (the *MD-A2 Outline* mark: an outlined `M↓` box with a
  blue `M` and a green arrow, *Soft* palette `#6A9BF4` / `#63C77D`), **without the dark rounded tile**.
  The background is now transparent. `Branding/Apps/export.py final2` generated it, as the SVG header
  comments say. The small master (16–32 px) is simplified, with heavier marks.
- **The new `.ico`** holds ten 32-bit PNG frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256). Each frame
  is byte for byte the matching `png/enigma-markdown-editor-N.png` (checked while planning).
- **Where the icon lives today** (FEATURE-EA38, FEATURE-7CE4):
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico`:
    - `<ApplicationIcon>`;
    - `MainWindow.axaml` `Icon=`;
    - the `app.ico` copied beside the executable;
    - the MSI's shortcut and Control Panel icon;
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`: the About dialog's `Image`. Today it is
    the `.ico`'s 256 frame byte for byte;
  - `packaging/linux/icons/enigma-markdown-editor-{16,32,48,64,128,256}.png`: what `install.sh`
    installs, `uninstall.sh` removes and `pack-linux.ps1` packs. The three size lists move together.
    Each file is today the `.ico` frame of its size byte for byte.
- **SVG masters.** GitClient, TextEditor, FileExplorer and Launchpad all commit theirs in
  `packaging/icon/`. This repository has none: FEATURE-EA38 left them out before that convention existed.
- **Prose that describes the tile**: only `docs/RELEASE.md` §1's icon check ("The mark is a dark square,
  so on a dark taskbar its rounded edge…"). `RELEASENOTES.md`'s 1.9.0 section is history and stays.
- `AppIconTests` (Desktop tests) checks the `.ico`'s frame set, the About PNG's size and the output
  copy. No test ties the About PNG or the Linux PNGs to the `.ico`. The headless suite has no Skia, so
  only bytes can be checked.
- The source directory is outside the repository and is left in place.

## Steps

1. Copy, byte for byte:
   - `enigma-markdown-editor.ico` → `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico`;
   - `png/enigma-markdown-editor-256.png` → `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`;
   - `enigma-markdown-editor.svg` and `enigma-markdown-editor-small.svg` → `packaging/icon/`;
   - `png/enigma-markdown-editor-{16,32,48,64,128,256}.png` → `packaging/linux/icons/`.
2. `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fixtures/RepositoryRoot.cs`: locates the repository
   root, the first directory above the test output that holds `Enigma.MarkdownEditor.slnx`.
   FEATURE-1E34 reuses it.
3. `AppIconTests`, two new facts:
   - the About image is the `.ico`'s 256 frame, byte for byte;
   - each `packaging/linux/icons/enigma-markdown-editor-N.png` is the `.ico`'s N frame, byte for byte.
4. `docs/RELEASE.md` §1's icon check: describe the tile-less mark, and what to look for on a dark and a
   light taskbar.
5. `README.md` *Repository layout*: list `packaging/icon/`.
6. Release build; full suite.

## Acceptance criteria

- The four destinations hold the new files byte for byte (`cmp`), and no old frame is left.
- `AppIconTests` passes, the two new facts included. The About view's `TheIcon_IsAResourceThatResolves`
  still passes.
- `docs/RELEASE.md` no longer describes a dark square.
- Release build: 0 warnings, no `AVLN`. The full suite shows no new failure against the run's baseline.

## Out of scope

- Changing the sizes the Linux installer installs (still six) or a `scalable/apps` SVG on Linux.
- Rendering an SVG in-app.
- Editing the artwork.
- Release notes and version (FEATURE-FB91).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which files | `app.ico`, `app-256.png`, the six Linux PNGs, and both SVGs | "Update the icons currently used in the app (app icon, image for about box, linux installer, etc.)"; the set has today's shape | Re-packing the `.ico` |
| Commit the SVGs? | Yes, in `packaging/icon/`, header comments verbatim | Every sibling Enigma app carries its masters there. The user delivered them as part of the set. `export.py` writes the comments, so an edit would be lost on the next export | Leaving them out as FEATURE-EA38 did (before the convention) |
| File names | Existing names kept (`app.ico`, `app-256.png`, `enigma-markdown-editor-N.png`) | Every reference stays valid: csproj, views, scripts, the twelve historical MSI profiles | Renaming |
| Extra Linux sizes (20/24/40/96) | Not added | They are frames of the `.ico`. Adding sizes changes three scripts in step, and `pack-linux.ps1` cannot be run end to end here | Extending `ICON_SIZES` |
| Tests | Two new `AppIconTests` facts tying the About PNG and the Linux PNGs to the `.ico` frames | A partial copy is how a set swap fails, and nothing catches it today. Holds for today's and the new set | No test change; decoding images (no Skia in the suite) |
| Locating Linux icons from a test | A shared `Fixtures/RepositoryRoot` helper (walks up to the `.slnx`) | FEATURE-1E34's installer test needs the same root; one helper, not two copies | Copying the PNGs into the test output |
| The source directory | Left in place | It is the Branding repository's output, outside this repository | Deleting it |

## Beyond-the-draft checklist

| Dimension | Resolution |
|---|---|
| Security & auth | N/A: static art |
| Input validation & abuse | N/A: no input |
| Performance & scalability | `app.ico` shrinks (17.5 KB → 9.9 KB); nothing else moves |
| Concurrency | N/A |
| Error handling & resilience | A broken or partial copy fails `AppIconTests` |
| UX edge cases | Transparent mark on dark and light taskbars and on both About themes: a visual check, in `docs/RELEASE.md` §1 |
| Accessibility & i18n | N/A: no text. Contrast of the blue/green mark on both themes is part of the visual check |
| Observability | N/A |
| Data migration & compatibility | None: same paths; settings, workspaces and sessions untouched |
| Deployment & operations | Installer, packer and MSI keep their paths. Windows' icon cache may show the old icon until refreshed (already in RELEASE.md) |
| Licensing & dependencies | The user's own artwork; no dependency |
| Testing | `AppIconTests` (five facts) plus the About resolve test |
| Documentation | `docs/RELEASE.md` §1 icon check; README layout gains `packaging/icon/`; release notes in FEATURE-FB91 |
