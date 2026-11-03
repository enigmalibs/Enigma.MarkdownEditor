# FEATURE-1E34-PHASE02 — The buildmsi script

**Item:** FEATURE-1E34 — Windows MSI build script
**Branch:** `feature/feature-1e34-phase02-buildmsi-script`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

`packaging/windows/buildmsi.ps1` builds the Windows installer in one command, from any directory. It
is TextEditor's script, adapted:

1. It checks that `git`, `dotnet`, `wix` and the vendored worker are there, **before** anything is
   cleaned.
2. It reads `<Version>` from the Desktop csproj. It stops unless the version is `X.Y.Z` and the Core
   csproj says the same.
3. `git -C <root> clean -dxf`.
4. `build/publish.ps1 -Rid win-x64`: the **self-contained** release publish into
   `artifacts/publish/win-x64`, the payload every MSI of this app has shipped. Then it checks that the
   exe and `app.ico` are there.
5. It writes `artifacts/Enigma.MarkdownEditor.X.Y.Z.msipkg.json`:
   - absolute paths, a new `productId`;
   - upgrade code `a7b550b9-1338-458f-bf2b-66dcf1d25380`;
   - "Enigma Markdown Editor", "Josué Clément", `PerMachine`,
     `%ProgramFiles%\Enigma Markdown Editor`, `compression` `None`;
   - `app.ico` as the Control Panel and shortcut icon;
   - shortcuts in `%Desktop%` and `%ProgramMenu%`.
6. It runs `Enigma.Msi.Worker.exe build` on that profile, giving `artifacts/Enigma.MarkdownEditor.X.Y.Z.msi`.

Every failure prints one `buildmsi: <reason>` line and exits non-zero, with the tool's exit code when
there is one. `buildmsi.bat` runs it from `cmd` with `-ExecutionPolicy Bypass` and passes the exit code
on. `WindowsInstallerIdentityTests` pins the upgrade code, the product name (`AppInfo.DisplayName`) and
the assembly.

## Files/modules touched

- Created:
  - `packaging/windows/buildmsi.ps1`, `packaging/windows/buildmsi.bat`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/WindowsInstallerIdentityTests.cs`
  - `docs/done/FEATURE-1E34-PHASE02.md`
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj` (the `<Version>` comment only)
  - `README.md`, `docs/RELEASE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-1E34.md`: statuses. FEATURE-1E34 is `DONE`.

## Deviations & follow-ups

- **The install folder is `Program Files (x86)`.** The MSI's template is `Intel;1033`: Enigma.Msi has no
  platform setting, so its package is 32-bit and `ProgramFilesFolder` is `Program Files (x86)` on 64-bit
  Windows. The application inside is the win-x64 publish. Every earlier Enigma.Msi-built MSI had the same
  template. Docs that said `%ProgramFiles%` were corrected:
  - the README's *Installation › Windows*;
  - RELEASE.md §1's `app.ico` check;
  - RELEASE.md §6's version command, now `${env:ProgramFiles(x86)}`. A 64-bit prompt resolves
    `$env:ProgramFiles` to `C:\Program Files`, so the old command found nothing.
- **Order of the release builds.** `buildmsi`'s `git clean -dxf` deletes all of `artifacts/`.
  RELEASE.md §4 now builds the MSI first, then the Linux publish and tarball.
- **The MSI is about 240 MB** (239,485,604 bytes): an uncompressed self-contained payload of 277 files
  (226.8 MB). The old profiles used `High`. `compression` is `None` to match the siblings' faster build.
  It is a one-word change in the script if download size matters more.
- **WixSharp's "source base directory"** is the caller's current directory (it printed this repository's
  path). It resolves relative paths only, and the profile's are absolute. Afterwards
  `git status --porcelain --ignored` in this repository showed nothing beyond the dev's own changes and
  `bin/`/`obj/`.
- **Not run, by design:** installing, launching or uninstalling the MSI.
- **Line endings:** `buildmsi.bat` is CRLF as `.gitattributes` asks; the `.ps1` is LF and ASCII.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-8156.md`.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**, `grep -ci AVLN` **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3658 passed, 0 failed, 0 skipped**:
  3655 plus the three `WindowsInstallerIdentityTests` facts.
- **End to end, in a scratch clone** of this branch, with the dev's tree committed there:
  - **`wix` off the `PATH`:** `buildmsi: wix is not on the PATH: dotnet tool install --global wix.`,
    exit code 1, and an untracked canary file was still there.
  - **Core's version changed to 9.9.9:** `buildmsi: the csprojs disagree on the version: … '1.10.0', …
    '9.9.9'.`, exit code 1, canary still there. The csproj was restored afterwards.
  - **`buildmsi.bat`:**
    - exit code **0** in 10 s wall time (NuGet cache warm);
    - the canary was deleted, so `git clean` ran;
    - the log shows `dotnet publish … -r win-x64 --self-contained true -o …\artifacts\publish\win-x64`,
      277 files;
    - `artifacts/` holds `Enigma.MarkdownEditor.1.10.0.msi` and its `.msipkg.json`, which has
      `"compression": "None"`;
    - `git status --porcelain` was empty afterwards; `.wixsharp/` shows only as ignored.
  - **The MSI's tables**, read through the Windows Installer API:
    - `ProductName` Enigma Markdown Editor, `Manufacturer` Josué Clément, `ProductVersion` 1.10.0;
    - `UpgradeCode` `{A7B550B9-1338-458F-BF2B-66DCF1D25380}`, `ALLUSERS` 1, `ARPPRODUCTICON`
      `app_icon.ico`;
    - `INSTALLDIR` "Enigma Markdown Editor" under `ProgramFilesFolder`;
    - two "Enigma Markdown Editor.lnk" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
      `[INSTALLDIR]\Enigma.MarkdownEditor.Desktop.exe`, icon `IconFile1_app.ico`;
    - `Upgrade`: `VersionMax 1.10.0` (attributes 1) and `VersionMin 1.10.0` (attributes 2);
    - the `File` table names exactly the publish folder's 277 files, `coreclr.dll`, `hostfxr.dll` and
      `System.Private.CoreLib.dll` among them; template `Intel;1033`.
- Fix budget: 0 cycles used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Calling `build/publish.ps1` | `try { & … -Rid win-x64 } catch { Stop-Build … }` | It reports failure by throwing, not by exit code; one `buildmsi:` line keeps the contract |
| `-Version` passed to the publish | No | `build/publish.ps1` reads the same Desktop csproj; the script has just checked it |
| Where the tests read the script | FEATURE-8156's `RepositoryRoot` | One root-finder for the suite |
| Install-path wording | `Program Files (x86)`, from the MSI's template | What the tables showed; `%ProgramFiles%` read wrong for a person on 64-bit Windows |

## Documentation sweep

- `README.md`:
  - a *Windows installer* section under *Build from source*, with the `git clean -dxf` warning;
  - *Installation › Windows*: the real folder and the in-place upgrade;
  - *Repository layout*: `packaging/windows/` and `msiProfiles/` (history).
- `docs/RELEASE.md`:
  - the artifact table names `Enigma.MarkdownEditor.X.Y.Z.msi`, built by `buildmsi`;
  - the repo-root note no longer cites profile paths;
  - §1's profile item says there is none to commit;
  - §4 builds the MSI first. *The MSI* describes `buildmsi`;
  - *The profile clone rule* is now *The Windows installer's identity*;
  - §5 names the MSI by its new file name;
  - §1 and §6 use `Program Files (x86)`.
- `Enigma.MarkdownEditor.Desktop.csproj`: the `<Version>` comment no longer asks for a profile bump.
