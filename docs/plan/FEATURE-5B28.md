# FEATURE-5B28 — Release 1.0.0

**Status: DONE** · Multi-phase (2) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-5b28-phase01-packaging`, `feature/feature-5b28-phase02-release`.

## Objective

First release of Enigma Markdown Editor: reproducible **self-contained** artifacts for Windows (`win-x64`, installed via the house MSI profile / Enigma.Msi + WixSharp) and Linux (`linux-x64`, tar.gz + `.desktop` + `install.sh`), complete user-facing docs, version **1.0.0** tagged.

This is the dotnet-release **app** flow: version bump, RELEASENOTES, README callout, tag, MSI profile — **no NuGet pack/push**. The skill's execution boundary holds: in-repo edits + local GUID generation + local verification only; every outward-facing command (tag, push, publish uploads) is **printed for the user, never run**.

## Context & constraints

- Depends on all other work items being DONE (this is the last roadmap item).
- Publish model (decided): self-contained, no trimming, no single-file requirement (plain publish folder keeps WixSharp harvesting simple).
- Repo hosting (decided): GitHub, private — release artifacts attach to a GitHub Release; no CI (all builds local).
- Cross-platform DoD consequence (no CI): a **manual Linux verification checklist** is part of the release pre-flight.

## Design

- **Publish scripts** `build/publish.ps1` (PowerShell 7+; runs on both OSes, `-Rid win-x64|linux-x64` parameter):
  `dotnet publish src/Enigma.MarkdownEditor.Desktop -c Release -r <rid> --self-contained true -o artifacts/publish/<rid>`
  plus a clean step and a version echo read from the csproj. `artifacts/` is git-ignored.
- **Linux packaging** under `packaging/linux/`: `enigma-markdown-editor.desktop` (Name=Enigma Markdown Editor; Exec/Icon per install layout; Categories=Utility;TextEditor;), a 256 px PNG icon, `install.sh` (copies the publish output to `~/.local/opt/enigma-markdown-editor`, the desktop entry to `~/.local/share/applications`, the icon to `~/.local/share/icons/hicolor/256x256/apps`; prints the uninstall paths), and `pack-linux.ps1` producing `artifacts/EnigmaMarkdownEditor-<version>-linux-x64.tar.gz` with the executable bit preserved on the app binary and `install.sh`.
- **App icon**: replace the PHASE-0F9A placeholder with final art (multi-resolution `.ico` for Windows + the 256 px PNG for Linux). The art itself is user-owned — if no final art is supplied by then, ship the best placeholder and record it as a known issue.
- **README.md** — full app README (repo-only, never packed, so relative `docs/` links are fine): what/why (the ZenNotes replacement story), screenshots (dark + light), feature list, the vim key reference table (the FEATURE-11AA contract), installation (MSI / tar.gz + install.sh), build-from-source, settings/data locations, license. Badge: license only (no NuGet badge — this is an app).
- **`docs/RELEASE.md`** — adapted from the dotnet-release template for a non-packable app: pre-flight (Release build + full test suite + AVLN warning check) → manual **Linux verification checklist** (run under WSLg or a VM: launch, create workspace, tree ops incl. trash, edit/save, vim smoke set, preview + view modes, search, session restore, settings + logs written) → merge to `main` → tag bare `X.Y.Z` → publish scripts → MSI built from the version's `.msipkg.json` profile in Enigma.Msi.Desktop → tar.gz packaging → GitHub Release with both artifacts → post-verify (install MSI on a clean Windows, install.sh on Linux, version in the *Apps & features* entry and the exe's `ProductVersion` — **not** in an About box or the title bar, which this app does not have; corrected during PHASE01).
- **MSI profile** — `msiProfiles/Enigma.MarkdownEditor.Desktop.1.0.0.msipkg.json`, in Enigma.Msi's own **`.msipkg.json`** format.

  > **Corrected during PHASE01** (2026-08-28, user decision). This bullet previously specified `msiProfiles/Enigma.MarkdownEditor.msiprofile.1.0.0.json` with the flat schema from the `dotnet-release` v4 skill. Nothing reads that format: the real Enigma.Msi tool takes `<AppName>.<version>.msipkg.json` with a nested schema, which is what its own repo commits (`Enigma.Msi.Desktop.1.0.0.msipkg.json`). The field set below is the real one. See `docs/done/FEATURE-5B28-PHASE01.md` → *Deviations & follow-ups*.

  - `schemaVersion` 1 · `appName` `Enigma.MarkdownEditor.Desktop` · `version` 1.0.0 · `manufacturer` from `Directory.Build.props` `<Authors>` · `scope` PerMachine · `compression` High.
  - `upgradeCode`: generated **once** now (`[guid]::NewGuid()` — run locally, never fabricated) and reused verbatim by every future release; `productId`: new GUID for this version.
  - `install`: `{ installPath: "%ProgramFiles%\\Enigma Markdown Editor", releasePath: "artifacts\\publish\\win-x64" }`. The release path is the self-contained publish folder rather than `bin\Release\<tfm>`, because that is what actually ships — record it in the profile-generation note and the completion doc.
  - `output`: `{ outputPath: "artifacts", msiFilename: "EnigmaMarkdownEditor" }` · `controlPanel`: `{ productIcon: <the .ico under Assets> }`.
  - `shortcuts`: `%Desktop%` + `%ProgramMenu%`, both `targetPath` `[INSTALLDIR]\Enigma.MarkdownEditor.Desktop.exe`. Confirm with the user: `scope`, `installPath`, `compression`, `outputPath`.
  - **Building it** is done by opening the profile in the installed **Enigma.Msi.Desktop** application and building there (user decision, PHASE01). This repository takes no dependency on Enigma.Msi, so there is no `Enigma.Msi.Worker.exe` in its build output to invoke; `docs/RELEASE.md` §4 documents the GUI path.
- **Version & notes**: `<Version>1.0.0</Version>` in both `src/*` csproj files; `RELEASENOTES.md` first-release variant (Feature overview · Compatibility: Windows x64 + Linux x64, self-contained .NET 10 · Version); README what's-new callout.

## Phases

### PHASE01 — Packaging, docs & scripts — **Status: DONE**

Steps: publish script → Linux packaging folder (desktop entry, icon, install.sh, pack script) → final (or best-placeholder) app icon wired for both OSes → full README → `docs/RELEASE.md` with the Linux verification checklist → `.gitignore` covers `artifacts/`.

Acceptance criteria:
- `build/publish.ps1 -Rid win-x64` output launches on Windows; `-Rid linux-x64` output packs into a tarball whose layout and exec bits are verified (archive inspected; full run happens on Linux in PHASE02 pre-flight).
- README complete (screenshot slots may carry placeholders until final captures); RELEASE.md actionable top-to-bottom.
- Zero-warning Release build; full test suite green.

### PHASE02 — Version, MSI profile & runbook — **Status: DONE**

Steps: version bump to 1.0.0 (both csproj) → RELEASENOTES.md first-release section → README callout → MSI profile generated (GUIDs via a local generator; confirm the four confirm-fields with the user) → pre-flight: Release build + `dotnet test --solution` + AVLN check + the manual Linux checklist executed and recorded → **print** the runbook (merge, `git tag 1.0.0`, `git push origin 1.0.0`, publish scripts, the MSI built from the profile in Enigma.Msi.Desktop, tarball, GitHub Release upload, post-verify) — user runs it.

Acceptance criteria:
- Versions/notes/callout consistent everywhere; profile JSON valid with fresh `productId` + the persistent `upgradeCode`.
- Pre-flight evidence (build/tests/Linux checklist) recorded in the completion doc.
- Runbook printed; **no** tag/push/upload executed by the dev; no secrets echoed.

## Dependencies

All of FEATURE-0F9A, FEATURE-0DE1, FEATURE-3C44, FEATURE-11AA, FEATURE-6054, FEATURE-40F5 DONE.

## Out of scope

NuGet packaging (app, not library) · auto-update mechanism (backlog) · `.md` file association in the MSI (backlog) · macOS artifacts (not a target) · CI (decided: none).
