# FEATURE-5B28-PHASE01 — Packaging, docs & scripts

**Status: DONE** · Branch: `feature/feature-5b28-phase01-packaging` · Plan: `docs/plan/FEATURE-5B28.md`

## Summary

The application can now be turned into the two things a user actually installs, from either
operating system, with two commands each.

**`build/publish.ps1 -Rid win-x64|linux-x64`** produces a self-contained, untrimmed publish folder
under `artifacts/publish/<rid>` — the runtime inside it, so neither artifact asks the user to install
anything first. It cleans the previous output unless told not to (a stale file left behind would be
harvested into the installer as if it belonged), stamps the version it read from the csproj, and
fails loudly if the publish finished without producing an application host. Both runtime identifiers
can be published from either OS.

**`packaging/linux/pack-linux.ps1`** wraps the linux-x64 folder into
`artifacts/EnigmaMarkdownEditor-<version>-linux-x64.tar.gz`, together with `install.sh`, the desktop
entry, the icon and `LICENSE.md`. It writes the tar a header block at a time rather than shelling out
to `tar`, and the reason is the mode bits: a Windows publish folder carries no POSIX permissions, so
any tarball built from it on Windows would mark the application host `0644` and hand the user
something they cannot run. Deciding each entry's mode from the file itself makes the archive
identical whichever OS packs it and needs no `tar` binary on either. Text entries are written with LF
endings whatever the checkout produced — which is load-bearing rather than defensive here, because
this working tree *is* CRLF (see *Deviations*), and a CR byte on the shebang line stops `install.sh`
from running at all.

**`install.sh`** installs for one user, under `$HOME`, with no root and no package manager: the
application to `~/.local/opt/enigma-markdown-editor`, the desktop entry and icon to the XDG
directories. It refuses before touching anything if the payload is not beside it, replaces rather
than merges an existing install (an upgrade that copied over the previous one would leave every file
the newer version dropped), substitutes the absolute `Exec` path into the desktop entry — which is
why that file ships as a template, since the format expands no variables and the prefix is only known
at install time — and finishes by printing the exact commands that undo it.

**The docs.** `README.md` is now the application's README rather than a construction notice: what it
is and the stance behind it, the feature list, the complete vim key reference as its own section
(the FEATURE-11AA contract, including what is deliberately *not* in 1.0), the non-vim shortcuts,
installation for both platforms, build-from-source, and where the per-user data lives.
`docs/RELEASE.md` is the runbook — pre-flight, the manual Linux verification checklist that stands in
for the CI this project decided not to have, merge and tag, the artifact builds, the GitHub release,
and a post-release check that only uses things that can actually be observed.

The app icon stays the FEATURE-0F9A placeholder, per your decision; the Linux 256 px PNG is extracted
from that same `.ico`'s 256 px frame, so both platforms show identical art and both replace together.

## Files/modules touched

### Created — `build/`

| File | Notes |
|---|---|
| `Common.ps1` | Dot-sourced context: repo root, project path, assembly/product/package names, version, artifact paths. Exists so the version-reading logic is in one place rather than copied into both scripts. `$PSScriptRoot` inside a function resolves to the file that *defined* it, which is what lets `pack-linux.ps1` dot-source it from two directories away and still get the right repo root. |
| `publish.ps1` | `-Rid win-x64\|linux-x64`, `-Version`, `-NoClean`. Clean → publish → assert the apphost exists → report file count and size. The apphost's extension follows the RID being published, not the OS publishing it. |

### Created — `packaging/linux/`

| File | Notes |
|---|---|
| `pack-linux.ps1` | The ustar writer described above, streamed through `GZipStream` straight to the output file — the publish folder is over 100 MB and buffering it would have been the only large allocation in the build. Handles the ustar `prefix` split for long paths rather than truncating, and emits directory entries before the files in them. |
| `install.sh` | POSIX `sh`, `set -eu`. `--prefix`, `--help`; exit 2 on a bad argument, exit 1 when the payload is missing. |
| `enigma-markdown-editor.desktop` | Template with an `@EXEC@` placeholder. `Categories=Utility;TextEditor;` and `StartupWMClass` so the running window docks onto its own launcher. |
| `enigma-markdown-editor.png` | 256×256 RGBA, extracted from `Assets/app.ico`'s 256 px frame (BGRA bottom-up DIB → PNG). Same art as Windows, from the same source file. |

### Created — `docs/`

| File | Notes |
|---|---|
| `RELEASE.md` | The runbook. Adapted from the dotnet-release template for a non-packable app: no `pack`, no `nuget push`. |
| `done/FEATURE-5B28-PHASE01.md` | This file. |

### Modified

| File | Change |
|---|---|
| `README.md` | Rewritten as the 1.0 application README. The "under construction" status blockquote is gone; the vim reference, installation, data locations and screenshot slots are new. Repository layout and conventions carried over and extended with `build/` and `packaging/`. |
| `docs/roadmap.md` | `FEATURE-5B28` and its `PHASE01` → `IN PROGRESS`, then `PHASE01` → `DONE`. |
| `docs/plan/FEATURE-5B28.md` | Same status transitions, plus the MSI-profile correction folded in by the documentation freshness sweep: the profile bullet rewritten for the real `.msipkg.json` field set, the RELEASE.md and PHASE02 bullets pointed at the Enigma.Msi.Desktop build path, the unachievable About/title post-verify replaced, and a blockquote recording the change. |

### Verified unchanged

- **`.gitignore`** already ignores `artifacts/` (line 66). The plan lists this as a step; it was
  satisfied before the phase began, so nothing was edited.
- **`src/Enigma.MarkdownEditor.Desktop/Assets/app.ico`** — the placeholder is kept deliberately.

## Deviations & follow-ups

- **The scripts target the Windows PowerShell 5.1 language subset, not PowerShell 7+ as the plan
  says** — your decision. `pwsh` is not installed on this machine, so a 7-only script could not have
  been *run* here and the acceptance criterion would have been verified by reading. The subset (no
  ternary, no `??`, no `&&`/`||`, no three-argument `Join-Path`, no `$IsWindows`) runs unchanged on
  PowerShell 7 on both OSes, so this is strictly wider compatibility. The constraint is written down
  at the top of `build/Common.ps1` so it is not lost.
- **The MSI profile format in the plan is stale, and this changes PHASE02.** The plan and
  `dotnet-release` v4 specify `msiProfiles/<AppName>.msiprofile.<version>.json` with a flat schema.
  The real Enigma.Msi tool reads `<AppName>.<version>.msipkg.json` with a nested schema
  (`schemaVersion`, `install{}`, `output{}`, `controlPanel{}`), which is what its own repo commits
  and the only format anything can build. Your decision: use the real one, so PHASE02 writes
  `msiProfiles/Enigma.MarkdownEditor.Desktop.1.0.0.msipkg.json`. **`docs/plan/FEATURE-5B28.md` was
  corrected in this same commit** (documentation freshness sweep): the MSI-profile bullet now carries
  the real field set, the RELEASE.md and PHASE02 bullets name the Enigma.Msi.Desktop build path, and
  a blockquote in the plan records what changed and why.
- **The MSI is built through the Enigma.Msi.Desktop GUI**, per your decision — this repository takes
  no dependency on Enigma.Msi, so there is no worker in its build output to invoke. `RELEASE.md` says
  to open the profile in that application and build it.
- **The plan's post-release check "version in the About/title" is not achievable and was replaced.**
  There is no About box, and `MainWindowViewModel.Title` is `AppInfo.DisplayName` with no version in
  it. `RELEASE.md` checks the *Apps & features* entry and the executable's `ProductVersion` instead,
  and says why. Follow-up: an About dialog, or the version in the window title, would make this
  directly observable.
- **The app icon is still placeholder art** — your decision, and the plan's own fallback. It is a
  generated blue plate with a white "M" and a down arrow. Both platform assets now derive from the
  one `.ico`, so replacing it means regenerating the PNG from the new art as well.
- **The README screenshots are empty slots.** The image block is commented out with its final paths
  (`docs/images/screenshot-dark.png`, `-light.png`) so dropping the captures in needs no README edit,
  and `RELEASE.md`'s pre-flight carries a checkbox for it. No `docs/images/` directory was created —
  git would not track an empty one.
- **The README carries no "ZenNotes replacement story".** The plan asks for it, but nothing in the
  repository records what that story is, and inventing one for a user-facing README is not something
  this phase should do. You chose the intro that states what the application is without naming it.
- **`build/Common.ps1` is a third script the plan does not name.** The plan lists `publish.ps1` and
  `pack-linux.ps1`; both need the same version-and-paths resolution, and a shared 88-line helper beat
  duplicating it. No behaviour outside those two scripts.
- **The desktop entry has no `%F` field code and no `MimeType=`.** `Program.Main` forwards its
  arguments to the Avalonia lifetime and nothing reads them as file paths, so either would advertise
  a capability the application does not have. Follow-up, alongside the `.md` association the plan
  already lists as backlog for the MSI: accept a file argument, then add both.
- **Two real bugs were found by running the deliverables and fixed in place.** `install.sh` had
  `[ -f createdump ] && chmod …` as a bare statement, which under `set -e` aborts the install on any
  publish without that file — now an `if`. And `pack-linux.ps1` marked the application host `0644`,
  because `GetExtension('Enigma.MarkdownEditor.Desktop')` is `'.Desktop'`, so the extensionless-means-
  native rule never matched it; the host is now matched by name. The first tarball built was wrong in
  exactly the way the hand-rolled writer exists to prevent, which is the argument for having verified
  it with an independent reader rather than the writer that produced it.
- **The permission bits `install.sh` applies could not be verified on this machine.** The dry run was
  performed on NTFS through Git Bash, where `chmod` is emulated and the on-disk mode does not change.
  What *is* verified is the archive's own modes, read back with Python's `tarfile`. Confirming the
  installed files are executable belongs to PHASE02's Linux checklist, which covers it.
- **The tarball built during verification is named `0.0.0-dev`**, because `<Version>` is PHASE02's
  step and the scripts warn and fall back until it exists. This is the intended order, not a gap.
- **No new automated tests.** The deliverables are two PowerShell scripts, a shell script, a desktop
  entry and two documents; the xUnit suite has no harness for any of them and the plan's acceptance
  criteria call for none. They were verified by execution instead — see the evidence below. The
  existing 1722 tests still pass.
- **Line endings: recommendation only, no action taken.** Every text file in this working tree is
  CRLF, including files committed long before this phase, while the repository itself stores LF
  (`git show HEAD:docs/roadmap.md` has no CR bytes) and `.gitattributes` already marks everything
  `eol=lf`. The checkout predates that rule. A `git add --renormalize .` on a clean tree would
  reconcile it. Nothing here was changed for it, and the pack script's LF normalization means the
  Linux artifact is correct either way.
- **No sub-agents were used.** The pieces are small and share one context — the same version and path
  vocabulary runs through both scripts and both documents — so splitting them would have cost more in
  interface-writing than it saved.

## Build/test evidence

**Build** — clean, zero warnings, before and after:

```
dotnet build Enigma.MarkdownEditor.slnx -c Release
  Build succeeded.  0 Warning(s)  0 Error(s)
```

**Avalonia XAML diagnostics** — the check `RELEASE.md` documents, run to confirm it behaves as
described:

```
dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN
  (no output)
```

**Tests** — full suite, Release configuration:

```
dotnet test --solution Enigma.MarkdownEditor.slnx -c Release
  total: 1722   failed: 0   succeeded: 1722   skipped: 0
```

**`publish.ps1`** — both runtime identifiers, from Windows:

| RID | Files | Size | Apphost |
|---|---|---|---|
| `win-x64` | 276 | 227.3 MB | `Enigma.MarkdownEditor.Desktop.exe` |
| `linux-x64` | 272 | 123.6 MB | `Enigma.MarkdownEditor.Desktop` |

**The win-x64 output launches** — started from `artifacts/publish/win-x64`, not from a build
directory:

```
main window   : 'Enigma Markdown Editor'
window handle : 49023250
private memory: 68 MB
```

and the application's own log confirms which binary ran, with no warning or error on startup:

```
Content root path: C:\Dev\EnigmaLibs\Enigma.MarkdownEditor\artifacts\publish\win-x64\
Enigma Markdown Editor started.
```

**`pack-linux.ps1`** — 280 entries, 20 executable, 123.6 MB uncompressed → 49.7 MB archive.

**The archive, read back with Python's `tarfile`** — an independent reader, deliberately not the
writer that produced it:

| Check | Result |
|---|---|
| Single top-level directory | `EnigmaMarkdownEditor-0.0.0-dev-linux-x64` |
| `install.sh` | `0755` |
| `app/Enigma.MarkdownEditor.Desktop` | `0755` |
| `app/createdump`, the 18 `*.so` | `0755` |
| `LICENSE.md`, the desktop entry, the icon, every `*.dll` | `0644` |
| `install.sh` bytes | 0 CR, no BOM, shebang `#!/bin/sh` |
| Desktop entry bytes | 0 CR, `@EXEC@` present |
| Icon | valid PNG, 256×256 RGBA, 3482 bytes |
| Nested directory entries | present, e.g. `app/zh-Hans` |
| Ownership | `uid=0 gid=0 root:root` |

**`install.sh`, run end-to-end from the real tarball** with `HOME` redirected to a scratch directory:

| Case | Result |
|---|---|
| `--help` | Usage printed, exit 0 |
| Install | 272 files placed; `Exec=` substituted to the absolute path, no `@EXEC@` left; icon 3482 bytes in the hicolor directory |
| Re-run over an existing install | Previous install removed — a planted stale file did not survive |
| `--nope` | `unknown argument '--nope' (try --help)`, exit 2 |
| Run with the payload missing | Names the missing file and says to run it from the release folder, exit 1 |

**Not verified here, by nature:** that the Linux binary runs on Linux, and that the installed files
carry their executable bit on a real filesystem. Both are `RELEASE.md` §2, which PHASE02 executes.
