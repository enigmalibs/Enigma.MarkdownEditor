# FEATURE-F36D — Publish scripts for Windows and Linux

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md` · **Branch:** `feature/feature-f36d-publish-scripts`, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

One-command publish scripts on both platforms. Each runs the exact command given in the spec and
exits with `dotnet`'s exit code:

- Windows: `packaging/windows/publish.ps1` and `packaging/windows/publish.bat`, both running
  `dotnet publish <Desktop project> -c Release -r win-x64 -p:PublishReadyToRun=true`;
- Linux: `packaging/linux/publish.sh`, next to `install.sh` and `uninstall.sh`, running
  `dotnet publish <Desktop project> -c Release -r linux-x64 -p:PublishReadyToRun=true`.

This is the same layout as Enigma.GitClient's (`packaging/windows/publish.ps1`, its `FEATURE-0D4E`).

## Context & constraints

- `build/publish.ps1` already exists. It is the **release pipeline**:
  - self-contained;
  - mandatory `-Rid`;
  - output in `artifacts/publish/<rid>`;
  - feeds the MSI (Enigma.Msi) and `packaging/linux/pack-linux.ps1`.

  It runs a different command, so it does not meet the spec, and it must stay as it is because
  `docs/RELEASE.md` §2–§5 and the MSI profiles rely on it.
- `packaging/linux/install.sh` and `uninstall.sh` exist (`set -euo pipefail`, `script_dir`,
  `repo_root`, mode `100755`).
- `.gitattributes` keeps `*.bat` CRLF and `*.sh` LF.
- There is no packaging test in the suite. `build/` and `packaging/` are verified by running them
  (see `docs/RELEASE.md`).
- `pack-linux.ps1` copies `packaging/linux` into the tarball. Check whether it globs `*.sh`. If it
  does, `publish.sh` would ship in the tarball, where it is meaningless (there is no source to
  publish), so exclude it explicitly.

## Steps

1. `packaging/windows/publish.ps1`:
   - `#Requires -Version 5.1` and a comment-based help header, in GitClient's shape: the command,
     where the output lands, and the `-ExecutionPolicy Bypass` example;
   - the header points at `build/publish.ps1` for release artifacts;
   - the project is resolved from `$PSScriptRoot`;
   - then the command and `exit $LASTEXITCODE`.
2. `packaging/windows/publish.bat`:
   - `@echo off`, `setlocal`;
   - the project is resolved from `%~dp0`;
   - then the command and `exit /b %ERRORLEVEL%`;
   - a short header comment;
   - CRLF.
3. `packaging/linux/publish.sh`:
   - `#!/usr/bin/env bash`, `set -euo pipefail`;
   - the project is resolved from the script's directory;
   - the command via `exec`;
   - a header comment in `install.sh`'s style;
   - staged with mode `100755` (`git add --chmod=+x`).
4. `pack-linux.ps1`: if it would pick up `publish.sh`, exclude it explicitly.
5. Docs:
   - `README.md`: name the three scripts and where their output lands, next to the existing
     build/install documentation;
   - `docs/RELEASE.md`: one line telling these scripts apart from `build/publish.ps1`.
6. Verify:
   - run `publish.bat` and `publish.ps1` (win-x64) from another directory;
   - run `publish.sh` through Git Bash (linux-x64, cross-compiled ReadyToRun);
   - each exits 0 and leaves `src/Enigma.MarkdownEditor.Desktop/bin/Release/net10.0/<rid>/publish/`
     with the apphost (and `app.ico`, from `FEATURE-7CE4`).

## Acceptance criteria

- The three scripts run from any directory, run exactly the spec's command, and exit with `dotnet`'s
  exit code.
- `publish.sh` is `100755` in the index, and `publish.bat` is CRLF in the working tree.
- `build/publish.ps1` is unchanged.
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green.

## Out of scope

- Self-contained or single-file publishing, other RIDs, and changes to the release pipeline.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where the scripts go | `packaging/windows/` and `packaging/linux/` | GitClient's layout: one place per platform across Enigma apps | Repo root; `build/` (name clash with the release `publish.ps1`) |
| The existing `build/publish.ps1` | Kept unchanged; the new header and RELEASE.md tell the two apart | The MSI and the tarball need its self-contained output | Rewriting it to the spec's command (breaks the release) |
| `publish.bat` | Calls `dotnet` itself | No dependency on PowerShell's execution policy | Wrapping `publish.ps1` |
| Arguments | None accepted | The spec fixes the command | Passing `%*` / `"$@"` through |
| Self-contained | Not set (the spec's command, verbatim) | Exactly what was asked | Adding `--self-contained` |
| Tests | Run all three scripts; no unit test | No packaging test infrastructure exists here; running is the real proof | Adding a packaging test project |
| The spec's "Linux packaging" section (install/uninstall scripts) | No work: they exist (`FEATURE-4BB7`) and do more than GitClient's (tarball mode, legacy cleanup) | "If not done": it is done | Re-copying GitClient's scripts over them |
