# FEATURE-E208 — Icon set that fills the frame

**Status: DONE** · single phase · Branch: `feature/feature-e208-icon-fills-frame` (cut from
`feature/2026-11-04-icons-release-1-12`)
Plan: `docs/plan/FEATURE-E208.md` · Roadmap: `docs/roadmap.md`

## Summary

The application carries the icon set from `Branding/Apps/final3/markdown-editor`. It is FEATURE-8156's
tile-less *MD-A2 Outline* mark, **scaled by 1.1852 so it fills its frame**: its longest side spans 16 to
240 of 256 units. The 16–32 px frames come from a redrawn small master on the same live area. Every place
the repository uses the icon has the new bytes, under its existing name:

- `Assets/app.ico`: the `.exe`, the window, `app.ico` beside the executable, and through it the MSI's
  shortcuts and Control Panel icon (`buildmsi.ps1` reads the published `app.ico`);
- `Assets/app-256.png`: the About dialog;
- the six Linux launcher sizes;
- the two SVG masters in `packaging/icon/`.

No code changed. `AppIconTests` already pins the About PNG and every Linux PNG to the `.ico`'s frames,
so the swap is checked as a whole.

## Files/modules touched

- Created: `docs/done/FEATURE-E208.md`
- Modified (binary, same names):
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico` (9,877 → 12,743 bytes; ten PNG frames, 16–256)
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`
  - `packaging/linux/icons/enigma-markdown-editor-{16,32,48,64,128,256}.png`
- Modified (text):
  - `packaging/icon/enigma-markdown-editor.svg`, `packaging/icon/enigma-markdown-editor-small.svg`
  - `docs/RELEASE.md` (§1 icon check)
  - `docs/roadmap.md`, `docs/plan/FEATURE-E208.md`: statuses.

## Deviations & follow-ups

- None against the plan.
- **Line endings, recommendation only:** the exported SVGs are CRLF; `.gitattributes`
  (`* text=auto eol=lf`) stores them as LF, so a checkout differs from the Branding export in line
  endings only, as with FEATURE-8156. The `.ico` and PNGs are binary and byte for byte.
- **The visual check is still open**: the larger mark on dark and light taskbars, at 16 px, and in both
  About themes (`docs/RELEASE.md` §1).

## Build/test evidence

- **Run baseline** (run branch `feature/2026-11-04-icons-release-1-12` at `4e8310e`, before this dev):
  - `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
    0 Error(s)**, `grep -ci AVLN` **0**;
  - `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3658 passed, 0 failed, 0
    skipped**.
  A clean baseline, so the strict rule applies to every dev of this run.
- This dev, same build: **0 Warning(s), 0 Error(s)**, `AVLN` **0**. Suite (`--no-build` on that build):
  **3658 passed, 0 failed, 0 skipped**, `AppIconTests`' ten facts/rows included.
- `cmp` of every destination against its source in `final3`: all identical. The test output's
  `app.ico` is the new file byte for byte.
- While planning, a script read the new `.ico`: reserved 0, type 1, ten 32-bit PNG frames (16, 20, 24,
  32, 40, 48, 64, 96, 128, 256), each byte for byte the matching `png/` file.
- Fix budget: 0 cycles used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| RELEASE.md wording on size | "fills its frame (16 to 240 of 256 units), so it reads larger than 1.11.0's" | States the geometry the SVG header gives, without claiming how it compares to other apps' icons, which nothing here measured |

## Documentation sweep

- `docs/RELEASE.md` §1, the icon check: says the mark now fills its frame, and adds `FEATURE-E208` to
  what is left open.
- `README.md` *Repository layout* (`packaging/icon/`) is still correct; the release notes and callout
  are FEATURE-721C's.
