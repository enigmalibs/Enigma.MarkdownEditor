# FEATURE-1E34-PHASE01 — Vendor the Enigma.Msi worker CLI

**Item:** FEATURE-1E34 — Windows MSI build script
**Branch:** `feature/feature-1e34-phase01-vendor-msi-worker`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The repository now carries Enigma.Msi's headless worker CLI in `packaging/windows/Enigma.Msi.Worker/`.
PHASE02's `buildmsi.ps1` will run it (`Enigma.Msi.Worker.exe build <profile>`).

- **What it is:** Enigma.Msi `1.7.0` (`a7a1bfb`) built against WixSharp_wix4 2.14.2. These are the 20
  files of its `bin/Release/net472/` output, without the PDBs, the API XML and `.wixsharp/` (3.4 MB).
- **Same as the siblings:** they are the 20 files Enigma.GitClient and Enigma.TextEditor vendor.
- **Its `README.md`** is GitClient's, naming this product's MSI. It gives:
  - the source;
  - the exit codes;
  - the requirements: .NET Framework 4.7.2, the WiX CLI, WiX 7's EULA;
  - the components' licences: Enigma.Msi, WixSharp and .NET are MIT, the WiX 4.0.1 assemblies MS-RL;
  - how to refresh it.
- **`.gitignore`** ignores `packaging/windows/Enigma.Msi.Worker/.wixsharp/`, which WixSharp writes
  beside the worker on its first build.

## Files/modules touched

- Created:
  - `packaging/windows/Enigma.Msi.Worker/`:
    - 20 binaries: `Enigma.Msi.Worker.exe`, `Enigma.Msi.Worker.exe.config`, `Enigma.Msi.dll`;
    - the `WixSharp*`, `WixToolset.*`, `System.*`, `Microsoft.Bcl.AsyncInterfaces.dll` and
      `mbanative.dll` files;
    - `README.md`;
  - `docs/done/FEATURE-1E34-PHASE01.md`.
- Modified:
  - `.gitignore`;
  - `docs/roadmap.md` and `docs/plan/FEATURE-1E34.md`: statuses.

## Deviations & follow-ups

- **None from the plan.**
- `Enigma.Msi.Worker.exe.config` differs from Enigma.Msi's build output only in line endings: the build
  writes CRLF, and `.gitattributes` (`* text=auto eol=lf`) stores LF. .NET Framework reads either. Every
  `.exe`/`.dll` is byte for byte, and `.gitattributes` marks them binary.
- `.gitignore` had no newline at its end; the new block adds one.
- The README names `buildmsi.ps1`, which PHASE02 adds.

## Build/test evidence

- **Baseline:** see `docs/done/FEATURE-8156.md` (0 warnings; 3648 passed).
- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**, `grep -ci AVLN` **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3655 passed, 0 failed, 0 skipped**.
  That is the same count as after FEATURE-8156; this phase adds no test.
- **The copy:**
  - copied from GitClient's 20 tracked files; GitClient's tree is clean for that folder;
  - `cmp` of each against `Enigma.Msi/src/Enigma.Msi.Worker/bin/Release/net472/`: 19 identical;
  - the `.exe.config` is identical once CRLF is stripped;
  - Enigma.Msi's `HEAD` is exactly tag `1.7.0`, with a clean tree;
  - `Enigma.Msi.dll` reads `1.0.0+a7a1bfb0…`, `WixSharp.dll` 2.14.2.0.
- `git status --porcelain --ignored -uall packaging/windows/Enigma.Msi.Worker/`: 21 untracked files
  (20 binaries and the README), none ignored. `git check-ignore -v` matches `.wixsharp/…` to the new
  rule (`.gitignore:369`).
- `Enigma.Msi.Worker.exe` with no arguments printed "No arguments were given." and its two usage forms.
  Exit code **2**.
- Fix budget: 0 cycles used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Copy source | GitClient's working tree, tracked files only (`git ls-files`) | Its `.wixsharp/` exists there but is ignored, so it must not come along |
| README wording | GitClient's, with this product's MSI name and the sibling note | One document for one vendored artifact across the Enigma apps |

## Documentation sweep

- Nothing stale yet: the README and `docs/RELEASE.md` change with the script itself (PHASE02).
