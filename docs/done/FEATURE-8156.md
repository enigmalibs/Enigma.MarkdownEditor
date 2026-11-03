# FEATURE-8156 — New icon set without the tile

**Status: DONE** · single phase · Branch: `feature/feature-8156-new-icon-set` (cut from
`feature/2026-11-03-icons-msi-release`)
Plan: `docs/plan/FEATURE-8156.md` · Roadmap: `docs/roadmap.md`

## Summary

The application carries the new icon set from `Branding/Apps/final2/markdown-editor`. It is the same
*MD-A2 Outline* mark as before (blue outlined `M↓` box, green arrow) **without the dark rounded tile**:
the background is now transparent. Every place the repository uses the icon has the new bytes, under
its existing name:

- `Assets/app.ico`: the `.exe`, the window, `app.ico` beside the executable, the MSI;
- `Assets/app-256.png`: the About dialog;
- the six Linux launcher sizes.

The two SVG masters now live in `packaging/icon/`, as in every sibling Enigma app.

`AppIconTests` gains two checks. The About PNG must be the `.ico`'s 256 frame, and each Linux PNG must
be the `.ico` frame of its size, byte for byte. A set swapped in one place but not another now fails
the suite.

## Files/modules touched

- Created:
  - `packaging/icon/enigma-markdown-editor.svg`, `packaging/icon/enigma-markdown-editor-small.svg`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/RepositoryRoot.cs`
  - `docs/done/FEATURE-8156.md`
- Modified (binary, same names):
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico` (17,509 → 9,877 bytes; ten PNG frames, 16–256)
  - `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`
  - `packaging/linux/icons/enigma-markdown-editor-{16,32,48,64,128,256}.png`
- Modified (text):
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/AppIconTests.cs`
  - `docs/RELEASE.md`, `README.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-8156.md`: statuses.

## Deviations & follow-ups

- **`RepositoryRoot` sits at the test project root**, beside `UiThread.cs`, not in `Fixtures/` as the
  plan said. `Fixtures/` is the markdown corpus the csproj copies to the output (`Fixtures/**/*.md`),
  and the suite's C# helpers live at the root.
- **Line endings, recommendation only:** the exported SVGs are CRLF. `.gitattributes`
  (`* text=auto eol=lf`) stores them as LF, so a checkout differs from the Branding export in line
  endings only. The `.ico` and PNGs are binary and byte for byte. Nothing to change.
- **The visual check is still open**: the transparent mark on dark and light taskbars and in both
  About themes. `docs/RELEASE.md` §1 now describes it.

## Build/test evidence

- **Run baseline** (run branch at `9742183`, before this dev):
  - `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
    0 Error(s)**, no `AVLN`;
  - `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3648 passed, 0 failed, 0
    skipped**.
  A clean baseline, so the strict rule applies to every dev of this run.
- This dev, same commands: **0 Warning(s), 0 Error(s)**, `grep -ci AVLN` **0**; **3655 passed, 0
  failed, 0 skipped**. That is the baseline plus `TheAboutImage_IsTheIconsLargestFrame` and six
  `TheLinuxIcon_IsTheIconsFrameOfItsSize` rows.
- `cmp` of every destination against its source in the new set: all identical.
- While planning, a script checked the new `.ico`: reserved 0, type 1, ten 32-bit PNG frames
  (16, 20, 24, 32, 40, 48, 64, 96, 128, 256), each byte for byte the matching `png/` file.
- **Mutation check:** with the previous 48 px Linux PNG restored, `TheLinuxIcon_IsTheIconsFrameOfItsSize(size: 48)`
  failed and nothing else did. With the new file back, all ten `AppIconTests` passed.
- Fix budget: 0 cycles used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Where `RepositoryRoot` lives | Test project root | See *Deviations* |
| Frame lookup in the tests | A `Frame(icon, size)` helper reading the `ICONDIRENTRY` | The two new facts compare against the same frames the first fact validates |
| Linux sizes under test | `[InlineData]` 16 32 48 64 128 256 | The sizes `install.sh`'s `ICON_SIZES` installs; a row per size names the stale file in the failure |

## Documentation sweep

- `docs/RELEASE.md` §1, the icon check: it described "a dark square" with a "rounded edge". It now
  describes the tile-less mark on both taskbars, the 16 px frames and both About themes, and what
  `AppIconTests` now pins.
- `README.md` *Repository layout*: `packaging/icon/` listed.
