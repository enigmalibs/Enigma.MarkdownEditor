# FEATURE-1E34 — Windows MSI build script

**Status:** IN PROGRESS
**Type:** FEATURE · two phases · Roadmap: `docs/roadmap.md`
**Branch:** one per phase, see below
**Run:** feature/2026-11-03-icons-msi-release

## Objective

One command builds the Windows installer, as in Enigma.GitClient (FEATURE-EAB7) and Enigma.TextEditor
(FEATURE-37C9). `packaging/windows/buildmsi.ps1` (and a `buildmsi.bat` twin):

1. cleans the repository;
2. publishes the release;
3. builds `artifacts/Enigma.MarkdownEditor.X.Y.Z.msi` with Enigma.Msi.

The product is "Enigma Markdown Editor", with its own upgrade code and the version from the csproj.
The repository carries a copy of Enigma.Msi's worker CLI.

## Context & constraints

- **Today's MSI** comes from a committed profile per version (`msiProfiles/`, 1.0.0 … 1.10.0). It is
  opened in Enigma.Msi.Desktop from the repository root, by hand (`docs/RELEASE.md` §4). Its fields:
  - `upgradeCode` `a7b550b9-1338-458f-bf2b-66dcf1d25380`, the same in all twelve profiles;
  - `appName` `Enigma.MarkdownEditor.Desktop`, `manufacturer` "Josué Clément", `PerMachine`;
  - `installPath` `%ProgramFiles%\Enigma Markdown Editor`;
  - `releasePath` `artifacts\publish\win-x64`: the **self-contained** publish of `build/publish.ps1`;
  - `compression` `High`, output `artifacts\EnigmaMarkdownEditor.msi`;
  - Control Panel and shortcut icon `Assets\app.ico`;
  - two "Enigma Markdown Editor" shortcuts (`%Desktop%`, `%ProgramMenu%`) on
    `[INSTALLDIR]\Enigma.MarkdownEditor.Desktop.exe`.
  No product with that upgrade code is installed on this machine (Windows Installer
  `RelatedProducts`, read-only).
- **The two publishes.**
  - `build/publish.ps1 -Rid win-x64` is the release publish: self-contained, into
    `artifacts/publish/win-x64`. It throws on failure and checks the apphost exists. The MSI and the
    tarball are built from it. README: "self-contained, nothing to install alongside it".
  - `packaging/windows/publish.ps1` is the local, framework-dependent ReadyToRun publish into `bin/`.
    `docs/RELEASE.md` says nothing in the release reads it.
  - `app.ico` lands beside the executable in either publish (FEATURE-7CE4).
- **Siblings.** GitClient and TextEditor vendor Enigma.Msi `1.7.0` (`a7a1bfb`, WixSharp_wix4 2.14.2):
  20 binaries in `packaging/windows/Enigma.Msi.Worker/` plus a `README.md`. `.wixsharp/` is git-ignored.
  - TextEditor's `buildmsi.ps1` reads the Desktop csproj's `<Version>` and stops when Core's differs.
    That is this repository's layout too.
  - The CLI is `Enigma.Msi.Worker.exe build <profile>`, with exit codes `0` built, `1` invalid or
    failed, `2` bad invocation.
  - Enigma.Msi here is clean at tag `1.7.0`; its `bin/Release/net472/` is the built worker.
- **Tools here:** `wix` 7 on the `PATH` (`~/.dotnet/tools`), .NET SDK 10.0.401, Windows PowerShell 5.1,
  no `pwsh`.
- `.gitignore` ignores `artifacts/`, `*.pdb`, `[Bb]in/`, `x64/`. The vendored folder must match none of
  them, because `git clean -dxf` deletes ignored files. `.gitattributes`: `*.dll`/`*.exe` binary, `*.bat`
  CRLF, everything else LF.
- Windows PowerShell 5.1 reads a BOM-less script as ANSI, and `.editorconfig` says UTF-8 without BOM.
  So the `.ps1` stays ASCII.
- **The run may not run `git clean` in this repository.** The script is verified end to end in a
  scratch clone.
- The full suite (Core and Desktop) runs on this machine, as in every earlier release of this repo.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Upgrade code | `a7b550b9-1338-458f-bf2b-66dcf1d25380` | "Its own UpgradeCode": the one every shipped profile (1.0.0–1.10.0) carries, so 1.11.0 upgrades them | A new GUID (would install beside earlier versions); a sibling's code |
| How to use Enigma.Msi | The 1.7.0 worker CLI, committed in `packaging/windows/Enigma.Msi.Worker/` | "Exactly like GitClient" | A net472 builder project in the solution |
| Where the copy comes from | GitClient's committed copy, checked byte for byte against Enigma.Msi 1.7.0's build output | The files the sibling runs already reviewed; no write into another repository | Rebuilding Enigma.Msi |
| Which files | The 20 binaries; no `*.pdb`, `Enigma.Msi.xml` or `.wixsharp/` | As GitClient | Everything in `bin/` |
| `.wixsharp/` | Git-ignored | WixSharp rewrites it; `git clean -dxf` resets it | Committing it |
| **The publish step** | `buildmsi.ps1` runs `build/publish.ps1 -Rid win-x64` (self-contained, `artifacts/publish/win-x64`) | Every MSI of this app shipped that payload, and the README promises nothing to install alongside it. It is this repository's one release publish, as `publish.ps1` is GitClient's | `packaging/windows/publish.ps1` (framework-dependent: the 1.11.0 MSI would not start on a machine without the .NET 10 Desktop Runtime) |
| Version source | The Desktop csproj's `<Version>`; stop when Core's differs | As TextEditor; About shows Core's version | Core's only; a new `Directory.Build.props` version |
| Profile | Generated per build into `artifacts/Enigma.MarkdownEditor.X.Y.Z.msipkg.json`, with absolute paths and a new `productId` | As the siblings: nothing to edit per release; the file stays as the build's record | Another committed profile per version |
| `msiProfiles/` | Kept untouched, as the record of 1.0.0–1.10.0; no new file | They document what shipped; deleting history buys nothing | Deleting the folder |
| Names | Product and shortcuts "Enigma Markdown Editor" (`AppInfo.DisplayName`); manufacturer "Josué Clément"; MSI `Enigma.MarkdownEditor.X.Y.Z.msi` | The user's spec; the siblings' `Enigma.<App>.X.Y.Z` file name | `appName` `Enigma.MarkdownEditor.Desktop`; `EnigmaMarkdownEditor.msi` |
| Install path, scope | `%ProgramFiles%\Enigma Markdown Editor`, `PerMachine` | As every earlier MSI and the siblings | `%ProgramFiles64%` |
| Compression | `None` | As GitClient and TextEditor, for build speed | `High` (the old profiles') |
| Control Panel and shortcut icon | The publish folder's `app.ico` | As the siblings; the file is beside the exe since FEATURE-7CE4 | `src\…\Assets\app.ico` |
| `.bat` twin | Runs `buildmsi.ps1` with `-ExecutionPolicy Bypass`, passing its exit code on | As the siblings and `publish.bat` | — |
| Prerequisites | `git`, `dotnet`, `wix` and the worker checked before anything is cleaned | Fails fast | Letting the worker fail after a full publish |
| Tests | `WindowsInstallerIdentityTests` (Desktop tests): `$UpgradeCode`, `$ProductName` = `AppInfo.DisplayName`, `$Assembly` = the app's assembly name | The identity facts a typo would silently break; TextEditor's shape | No test |
| Verification | The whole script in a scratch clone of the dev's tree, then the MSI's tables read through the Windows Installer API; nothing installed | The run may not `git clean` the user's repository; installing is outward-facing | Running it in the repository |
| Phases | Two: vendor the worker, then the script | Where a binary comes from and whether a script is right are separate reviews | One phase |

## PHASE01 — Vendor the Enigma.Msi worker CLI

**Branch:** `feature/feature-1e34-phase01-vendor-msi-worker`
**Status:** DONE

### Steps

1. Copy GitClient's 20 tracked binaries from `packaging/windows/Enigma.Msi.Worker/` into the same path
   here. Then `cmp` each against `C:\Dev\EnigmaLibs\Enigma.Msi\src\Enigma.Msi.Worker\bin\Release\net472\`.
   The `.exe.config` may differ only in line endings.
2. `packaging/windows/Enigma.Msi.Worker/README.md`: GitClient's, with this product's MSI name, and a note
   that GitClient and TextEditor vendor the same 20 files.
3. `.gitignore`: `packaging/windows/Enigma.Msi.Worker/.wixsharp/`.
4. Check that `git status --porcelain --ignored` lists every vendored file as untracked and none as
   ignored. Run the worker with no arguments: usage, exit code 2.

### Acceptance criteria

- Every vendored binary is identical to Enigma.Msi 1.7.0's build output (`cmp`).
- No vendored file is git-ignored; `.wixsharp/` is.
- `Enigma.Msi.Worker.exe` with no arguments prints its usage and exits 2.
- Release build: 0 warnings. The full suite shows no new failure.

## PHASE02 — The buildmsi script

**Branch:** `feature/feature-1e34-phase02-buildmsi-script`
**Status:** TODO

### Steps

1. `packaging/windows/buildmsi.ps1`: TextEditor's, adapted. ASCII, `Set-StrictMode`,
   `$ErrorActionPreference = 'Stop'`, comment-based help, paths resolved from its own location.
   1. Constants: `$UpgradeCode`, `$ProductName = 'Enigma Markdown Editor'`, `$Manufacturer`
      (`[char]0x00E9`), `$Assembly = 'Enigma.MarkdownEditor.Desktop'`.
   2. Check that `git`, `dotnet` and `wix` are on the `PATH` and that the worker exists.
   3. Read the Desktop and Core csproj `<Version>`s. Each must be `^\d+\.\d+\.\d+$`, and they must agree.
   4. `git -C <root> clean -dxf`. Stop on a non-zero exit code.
   5. Run `build/publish.ps1 -Rid win-x64`. A thrown error becomes `buildmsi: the publish failed: …`.
      Check that the exe and `app.ico` are in `artifacts/publish/win-x64`.
   6. Write `artifacts/Enigma.MarkdownEditor.X.Y.Z.msipkg.json` (UTF-8 without BOM, absolute paths, a
      new `productId`) with:
      - `PerMachine`, `%ProgramFiles%\Enigma Markdown Editor`, `compression` `None`;
      - `controlPanel.productIcon` and both shortcuts' icon: the publish folder's `app.ico`;
      - `%Desktop%` and `%ProgramMenu%` shortcuts named "Enigma Markdown Editor", targeting
        `[INSTALLDIR]\Enigma.MarkdownEditor.Desktop.exe`.
   7. Run `Enigma.Msi.Worker.exe build <profile>` and exit with its code. On success, print the MSI's
      path and the elapsed time.
2. `packaging/windows/buildmsi.bat` (CRLF): runs `buildmsi.ps1` through `powershell -NoProfile
   -ExecutionPolicy Bypass -File`, and passes its exit code on.
3. `tests/Enigma.MarkdownEditor.Desktop.UnitTests/WindowsInstallerIdentityTests.cs`: the three pins.
   It uses FEATURE-8156's `RepositoryRoot`.
4. Docs:
   - `README.md` *Installation › Windows* and *Build from source*: the installer and how to build it,
     with the `git clean -dxf` warning. *Repository layout*: `packaging/windows/` and `msiProfiles/`
     (history).
   - `docs/RELEASE.md`: the artifact table and §4 *The MSI* become `buildmsi`. The profile clone rule
     becomes *The Windows installer's identity*: the upgrade code is fixed in the script, and the product
     code is new per build. Also the §1 profile check and §5's attachment name.
   - The Desktop csproj's `<Version>` comment no longer names a profile to bump.
5. Verify in a scratch clone: commit the dev's tree there, run `buildmsi.bat`, then read the MSI's
   `Property`, `Directory`, `Shortcut`, `Upgrade`, `Icon` and `Media` tables. Also run with `wix` off the
   `PATH`: it must stop before cleaning.

### Acceptance criteria

- In the scratch clone, `buildmsi.bat` exits 0 and leaves `artifacts/Enigma.MarkdownEditor.<version>.msi`
  and its `.msipkg.json`. After it, `git status --porcelain` is empty.
- The MSI's tables show:
  - `ProductName` "Enigma Markdown Editor", `Manufacturer` "Josué Clément", `ProductVersion` = the csproj
    version;
  - `UpgradeCode` `{A7B550B9-1338-458F-BF2B-66DCF1D25380}`, `ALLUSERS` 1, `ARPPRODUCTICON` set;
  - `INSTALLDIR` "Enigma Markdown Editor" under `ProgramFilesFolder`;
  - two "Enigma Markdown Editor" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
    `[INSTALLDIR]\Enigma.MarkdownEditor.Desktop.exe`, each with an icon;
  - the files of a self-contained publish (the .NET runtime, e.g. `coreclr.dll`, among them).
- The profile says `"compression": "None"`.
- A missing `wix` stops the script before `git clean`, with the install command in the message.
- Release build: 0 warnings. The full suite, with `WindowsInstallerIdentityTests`, shows no new failure.

## Out of scope

- Installing, launching or uninstalling the MSI (outward-facing; the user's).
- Signing the MSI; a 64-bit package (Enigma.Msi has no platform setting).
- Changing Enigma.Msi, `build/publish.ps1` or the Linux packaging.
- Deleting or editing the historical `msiProfiles/`.
- A `-NoClean` switch or other options nobody asked for.
