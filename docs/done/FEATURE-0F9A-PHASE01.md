# FEATURE-0F9A-PHASE01 — Repo hygiene & solution skeleton

**Status: DONE** · Branch: `feature/feature-0f9a-phase01-bootstrap` · Plan: `docs/plan/FEATURE-0F9A.md`

## Summary

Bootstrapped the repository and the solution skeleton every later work item builds on: repo hygiene
files, the four shared root config files, the MIT licence and release-notes placeholder, the `.slnx`
solution, and the four `net10.0` projects (`Core`, `Desktop`, and one xUnit v3 test project each)
with three passing smoke tests. No Avalonia dependency, UI or application behaviour yet — those are
PHASE02 and PHASE03.

Every `Directory.Packages.props` pin the plan lists was checked against nuget.org before being
written (see *Deviations & follow-ups* for the one that does not resolve).

## Files/modules touched

### Created — repo root

| File | Notes |
|---|---|
| `.gitignore` | Verbatim from the git-repo-hygiene template (Visual Studio / dotnet ignore set + Rider `.idea/`). Verified: `bin/`, `obj/` produce no `git status` entries. |
| `.gitattributes` | Verbatim from the git-repo-hygiene template (`* text=auto eol=lf`, CRLF for `.bat`/`.cmd`, binary rules). |
| `.editorconfig` | Verbatim from the **dotnet-solution-config** template (full C# style + naming + analyzer severities) — not git-repo-hygiene's minimal line-endings-only file. |
| `Directory.Build.props` | From template, placeholders filled `Josué Clément` / `Copyright © 2026 Josué Clément`, mirroring `C:\Dev\EnigmaLibs\Enigma.Configuration` exactly as the plan asks. |
| `Directory.Packages.props` | CPM on; all pins from the plan's *Context & constraints*, grouped Core / Desktop (Avalonia coupled set) / editor / Tests. `System.Buffers` + `PolySharp` deliberately omitted (nothing targets `netstandard2.0`, so they would only raise NU1510). |
| `global.json` | `sdk 10.0.100` + `rollForward: latestFeature` (installed 10.0.400 satisfies it) and `"test": { "runner": "Microsoft.Testing.Platform" }`. |
| `LICENSE.md` | MIT template, 2026, Josué Clément. |
| `RELEASENOTES.md` | 0-byte placeholder, for dotnet-release to fill at FEATURE-5B28. |
| `Enigma.MarkdownEditor.slnx` | `/src/` and `/tests/` solution folders, four projects. |

`README.md` already existed and was kept (not recreated), as the plan directs — it was then rewritten
in the documentation freshness sweep, see *Modified* below.

### Created — projects

| File | Notes |
|---|---|
| `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj` | `net10.0` class library, zero Avalonia dependency. The TFM deviation from the house `netstandard2.0;net8.0;net10.0` default is documented in a csproj comment, per the plan. |
| `src/Enigma.MarkdownEditor.Core/AppInfo.cs` | `AppInfo.DisplayName` = `"Enigma Markdown Editor"`, `AppInfo.AppDataFolderName` = `"Enigma.MarkdownEditor"`. |
| `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj` | `net10.0` `WinExe`, `ProjectReference` → Core. Deliberately minimal: the Avalonia package set arrives in PHASE02. |
| `src/Enigma.MarkdownEditor.Desktop/Program.cs` | Placeholder `Main` (`WinExe` cannot compile without an entry point); PHASE02 replaces the body with `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`. |
| `tests/Enigma.MarkdownEditor.Core.UnitTests/…csproj` + `AppInfoTests.cs` | MTP-native xUnit v3 (`OutputType Exe`, no `Microsoft.NET.Test.Sdk`), `net10.0` mirroring Core's single TFM. Two smoke tests. |
| `tests/Enigma.MarkdownEditor.Desktop.UnitTests/…csproj` + `ProgramSmokeTests.cs` | Same shape, `ProjectReference` → Desktop. One smoke test; PHASE02 adds the `MainWindowViewModel` tests here. |

### Modified

- `docs/roadmap.md` — `FEATURE-0F9A` → `IN PROGRESS`; `PHASE01` → `DONE`. Whole table re-padded (the `Status` column grew to 11 characters for `IN PROGRESS`).
- `docs/plan/FEATURE-0F9A.md` — item status → `IN PROGRESS`; PHASE01 heading → `DONE`.
- `README.md` — documentation freshness sweep (you chose README only): replaced the one-line stub
  with product identity, an under-construction status note pointing at the roadmap, the
  `src`/`tests`/`docs` layout, the SDK requirement, the build/test commands (including the MTP
  `--solution` form), the zero-warning bar and the CPM / `Directory.Build.props` conventions.

## Deviations & follow-ups

1. **`Enigma.Configuration` is not resolvable — PHASE03 blocker.** The package has no versions on
   nuget.org (`v3-flatcontainer` returns 404) and only `nuget.org` + the VS offline folder are
   registered as feeds. Its siblings (`Enigma.Logging.NLog`, `Enigma.Avalonia.Desktop`,
   `Enigma.Icons.Avalonia`, all 1.0.0) are published. Surfaced before building; **your decision was
   to pin `1.0.0` anyway** — an unreferenced `<PackageVersion>` is never restored, so PHASE01 builds
   clean. The pin carries a `NOTE:` comment. **PHASE03 (settings persistence) cannot restore until
   the package is published or a local feed is added.**
2. **`GenerateDocumentationFile` omitted on Core** (the house library template sets it `true`). Same
   rationale the plan gives for the TFM deviation: the library is app-internal and never packed, so
   it ships no XML doc file — and under `TreatWarningsAsErrors` enabling it would demand an XML
   comment on every public member of the vim engine, workspace services and markdown pipeline
   (CS1591) to keep the zero-warning bar. Documented in a csproj comment; one line to reverse.
   Consequence: build-time IDE0005 (unnecessary usings) stays at `suggestion`, exactly as the
   `.editorconfig` comment anticipates for app/test projects.
3. **`AppInfo` added to Core.** The plan does not enumerate PHASE01 source files, but the acceptance
   criteria require smoke tests, which need something real to assert against. `AppInfo` seeds the two
   identity values the plan's *Product identity* section already fixes, and PHASE03 needs
   `AppDataFolderName` for the `ConfigPaths` / log-folder paths.
4. **Placeholder `Program.Main` in Desktop.** `OutputType WinExe` does not compile without an entry
   point, and the plan keeps the Desktop csproj minimal this phase. It prints a "not yet implemented"
   line and consumes `args`; PHASE02 overwrites it.
5. **Fixture copy-glob `ItemGroup` dropped** from both test csprojs — neither suite loads fixtures by
   relative path. The template instructs keeping only the extensions actually used.
6. **`coverlet.collector` is inert under MTP.** Both the plan and the house test template list it, so
   it is referenced and pinned (6.0.4) — but it is a VSTest data collector and collects nothing under
   the Microsoft Testing Platform. Follow-up if coverage is ever wanted:
   `Microsoft.Testing.Extensions.CodeCoverage` instead.
7. **Line endings (recommendation only, no action taken).** Every file in the working tree is
   currently CRLF while `.editorconfig` specifies `end_of_line = lf`. `.gitattributes`
   (`* text=auto eol=lf`) normalizes text to LF in the index on commit, and the following checkout
   brings the working tree to LF, so no separate renormalize pass is expected for these new files.
   If you want it applied explicitly, the git-repo-hygiene command set — **not run here** — is:

   ```
   git add --renormalize .
   git status
   git commit -m "Normalize all line endings to LF"
   ```

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 3   failed: 0   succeeded: 3   skipped: 0
```

Acceptance criteria, one by one:

| Criterion | Result |
|---|---|
| `dotnet build … -c Release` succeeds with zero warnings | Met — 0 warnings, 0 errors. |
| `dotnet test --solution …` passes (≥ 2 smoke tests) | Met — 3 tests, all passing (2 Core + 1 Desktop). |
| All root artifacts exist | Met — the nine root files listed above, plus the pre-existing `README.md`. |
| No `Version=` attributes in any csproj (CPM) | Met — grep over `**/*.csproj` returns only explanatory comments, no attributes. |
| No `LangVersion`/`Nullable`/`ImplicitUsings`/`TreatWarningsAsErrors` repeated in csproj files | Met — same grep; they live only in `Directory.Build.props`. |

Note on the test run: `dotnet test` builds and runs the `Debug` configuration; the zero-warning
`Release` build is the separate command above, so both configurations are covered.
