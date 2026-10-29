# FEATURE-7CE4 — App icon in the output directory

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md` · **Branch:** `feature/feature-7ce4-icon-in-output-dir`, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

The build writes the application icon to its output directory as `app.ico`, next to
`Enigma.MarkdownEditor.Desktop(.exe)`. A publish therefore writes it too. Shortcuts, launchers and
installers can then point at a file instead of extracting the icon from the executable. This is the
same baseline as Enigma.GitClient's `FEATURE-5096`.

## Context & constraints

- `src/Enigma.MarkdownEditor.Desktop/Assets/app.ico` is the `ApplicationIcon` and the windows' `Icon`.
- `Assets/**` is an `AvaloniaResource` (embedded), so nothing lands in `bin/` today.
- Items with `CopyToOutputDirectory` are also published. They also flow into the output of
  referencing projects, which here is the Desktop test project.
- The SDK's default `None` glob may already include `Assets/app.ico`. If it does, use `Update`
  rather than `Include`, so the item is not duplicated.
- `AppIconTests` (Desktop tests) pins the `.ico`'s frames and the PNG.

## Steps

1. `Enigma.MarkdownEditor.Desktop.csproj`:
   - copy `Assets/app.ico` to the output root as `app.ico` (`CopyToOutputDirectory="PreserveNewest"`,
     `TargetPath="app.ico"`), with a one-line comment saying why;
   - it stays an `AvaloniaResource` as well.
2. `AppIconTests`: add a test that `app.ico` sits in `AppContext.BaseDirectory` and is byte for byte
   the embedded asset.
3. Verify:
   - a Release build puts `app.ico` in `bin/Release/net10.0/`;
   - a `win-x64` publish puts it in its `publish/` folder;
   - both copies are byte-identical to `Assets/app.ico`.

## Acceptance criteria

- `bin/Release/net10.0/app.ico` and the publish folder's `app.ico` exist, byte for byte
  `Assets/app.ico`.
- `avares://Enigma.MarkdownEditor.Desktop/Assets/app.ico` still resolves (the windows still have
  their icon).
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green, with
  the new test included.

## Out of scope

- The PNG (`app-256.png`), the Linux hicolor PNGs (`install.sh` installs them), and the MSI profile
  (the `.exe` already carries the icon).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which file | `app.ico` only | "The app icon" is the `ApplicationIcon`, the narrowest reading. Linux launchers already get PNGs from `install.sh` | Also copying `app-256.png` |
| Name and place | `app.ico` at the output root | The same name GitClient uses: predictable across Enigma apps | An `Assets/` sub-folder; `Enigma.MarkdownEditor.Desktop.ico` |
| Mechanism | A `None` item with `CopyToOutputDirectory` + `TargetPath` | Standard MSBuild, and it flows to publish with no custom target | A custom `Copy` target after build and publish |
| Test | One Desktop test, plus a manual build and publish check | The copy is pinned where the other icon facts are pinned | No test |
