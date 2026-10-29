# FEATURE-2877 — Update NuGet packages

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md` · **Branch:** `feature/feature-2877-update-nuget-packages`, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every NuGet package the solution references is on its latest stable version, including the
version-coupled Avalonia set and xunit.v3 4.x, which the 1.9.0 release held back.

## Context & constraints

- **Central Package Management**: all pins are in `Directory.Packages.props`.
- `dotnet list Enigma.MarkdownEditor.slnx package --outdated` at planning time (2026-10-29):

  ```text
  Desktop:        Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent   12.1.2 → 12.1.4
                  Enigma.Avalonia.Desktop                             1.2.0  → 1.3.0
  Desktop tests:  Avalonia.Headless, Avalonia.Headless.XUnit          12.1.2 → 12.1.4
                  xunit.v3                                            3.2.2  → 4.0.2
  Core tests:     xunit.v3                                            3.2.2  → 4.0.2
  Core:           up to date (Markdig 1.5.1, Enigma.Configuration 1.0.0)
  ```

  Already latest: Microsoft.Extensions.Hosting 10.0.14, Avalonia.AvaloniaEdit / AvaloniaEdit.TextMate
  12.0.0, Enigma.Icons.Avalonia 1.0.0, Enigma.Logging.NLog 1.0.0, AvaloniaUI.DiagnosticsSupport
  2.2.3, coverlet.collector 10.1.0.
- `Enigma.Avalonia.Desktop` 1.3.0 depends on Avalonia ≥ 12.1.4. Its release notes describe it as
  backward compatible with 1.2.0: no API change, and no theme key renamed or re-valued.
- `Avalonia.Headless.XUnit` 12.1.4 depends on `xunit.v3.extensibility.core` ≥ **3.2.2**: it was
  built against xunit v3 3.x. xunit.v3 4.0 is a major release, so binary compatibility with the
  headless harness has to be proven by the suite.
- Several comments name the versions. They must stay true:
  - `Directory.Packages.props`: the Avalonia group, and the Headless.XUnit ↔ xunit 3.2.2 note;
  - `Enigma.MarkdownEditor.Desktop.csproj`: "Version-coupled Avalonia set (12.1.2)";
  - the Desktop test csproj: "(12.1.2)".

## Steps

1. `Directory.Packages.props`:
   - the Avalonia set to 12.1.4 (all six packages, Headless included);
   - Enigma.Avalonia.Desktop to 1.3.0;
   - xunit.v3 to 4.0.2.
2. Restore, build Release with `--no-incremental` (zero warnings, no `AVLN`), and run the full suite.
3. If xunit.v3 4.0.2 breaks the build or the suite:
   - fix it if the change is small (an API rename in test code);
   - otherwise, if the headless harness is the cause, pin xunit.v3 back to 3.2.2, explain why in the
     props comment, and record it.
4. Update the version-naming comments listed above.
5. Re-run `dotnet list … package --outdated`. Record the output in the completion doc.

## Acceptance criteria

- `--outdated` reports nothing. The one possible exception is xunit.v3, if it is held back, and the
  completion doc and a props comment then give the reason.
- `dotnet list package` shows the new versions as both requested and resolved.
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green.

## Out of scope

- Prerelease versions, and SDK or `global.json` changes.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| The coupled Avalonia set | Bump all six packages plus Enigma.Avalonia.Desktop together | The props file's rule: the set moves as one. Enigma.Avalonia.Desktop 1.3.0 requires Avalonia 12.1.4 | Bumping some of them |
| xunit.v3 4.x | Attempt it; hold at 3.2.2 only if the headless harness breaks, with the reason recorded | You asked for latest. A broken harness would make the whole Desktop suite unrunnable | Holding it pre-emptively; `VersionOverride` per project (breaks the CPM single-version rule) |
| Prereleases | Stable only | "Latest versions" of a shipping app means stable | Taking previews |
| The spec's "Remove NET8" section | No work: all four projects target `net10.0` alone, and no net8 TFM exists anywhere | Nothing to remove | — |
