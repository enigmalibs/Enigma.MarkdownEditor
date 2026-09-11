# FEATURE-5B28-PHASE02 — Version, MSI profile & runbook

**Status: DONE** · Branch: `feature/feature-5b28-phase02-release` · Plan: `docs/plan/FEATURE-5B28.md`

> **The release is not ready to tag yet.** `docs/RELEASE.md` §2, the manual Linux verification, is
> still outstanding: the mechanical half was executed here (see *Build/test evidence*), the half that
> needs a working Linux desktop and a person at the keyboard was not, by your decision. §2 is a
> required pre-flight gate, and this phase does not discharge it.

## Summary

Everything that has to be *in the repository* before `1.0.0` can be cut is now in it, and everything
that has to happen *outside* it is written down as a runbook you execute.

**The version.** `<Version>1.0.0</Version>` is set in both `src/*` project files. That single value is
what the whole release hangs off: the assemblies carry it, `build/Common.ps1` reads it to name the
tarball, and the MSI profile mirrors it. Until this phase the scripts fell back to `0.0.0-dev` with a
warning, which is why PHASE01's verification tarball was named that way; the artifacts now come out
as `1.0.0`.

Each `<Version>` element carries a comment saying what it is *for*, because both files already ended
with a comment reading "No Version attributes either — versions live in Directory.Packages.props",
which after this change reads as a contradiction. It never meant this: it is about `PackageReference`
version *attributes* under Central Package Management, which is an unrelated thing that happens to
share a word. Both comments now say which of the two they mean.

**`RELEASENOTES.md`**, which existed as an empty file, is now the first-release section in the house
first-release shape — positioning paragraph, *Feature overview*, *Compatibility*, *Version*. The
feature overview goes a level deeper than the README's bullets, and the compatibility section records
what a reader actually needs to know before downloading: both artifacts self-contained, what they are
built on, how each platform installs, and where the per-user data goes.

**The README** gained the what's-new callout the pre-flight checks for, directly under the intro.

**The MSI profile** — `msiProfiles/Enigma.MarkdownEditor.Desktop.1.0.0.msipkg.json`, the first one, so
both GUIDs were generated (locally, with `[guid]::NewGuid()`; never hand-written). The `upgradeCode`
is the application's permanent identity and every future version's profile must carry it verbatim;
`productId` is this version's alone. It is not a file written from a reading of the format: it was
validated against the real `Enigma.Msi` assembly, which reported it valid on the model rules, valid
on the environment rules, and byte-identical to what that application itself would write — so opening
and saving it produces no diff. See *Build/test evidence*.

**Two corrections to `docs/RELEASE.md`**, both found by actually performing its steps rather than
reading them:

- §4 now says to **launch Enigma.Msi.Desktop from the repository root**, with the command. Every path
  in the profile is relative, and Enigma.Msi resolves relative paths against its own working
  directory — `MsiPackageValidator` calls `Directory.Exists` on the value verbatim, and
  `MsiBuildService` runs its worker in a fresh temp directory. Started from its start-menu shortcut
  it fails validation with five errors. Measured, not inferred: the verification harness runs the
  same profile from a foreign working directory and prints them. The paths stay relative on purpose —
  the profile is committed and cloned by every future release, so it must not carry a checkout path.
- §6's version check asked for `ProductVersion` to read `X.Y.Z`. It reads
  `1.0.0+bdafcd0000a7e69387c3b4d2d87f23356956bc3f` — the SDK appends the source revision to the
  informational version — so the check as written would have failed on a correct build. It now asks
  for `FileVersion` and `ProductVersion` together and says which part to read.

**One defect fixed in the shipped Linux artifact**, found by installing it: `install.sh`'s
substitution is global, so it rewrote the placeholder inside the `.desktop` file's own comment header
as well as on the `Exec` line. Every installed entry carried a comment asserting that `install.sh`
substitutes the absolute path it had just written. The template's comment no longer names the token,
and says why it must not. Verified by repacking and reinstalling. This was outside the phase's stated
scope and was fixed on your decision rather than silently.

## Files/modules touched

### Created

| File | Notes |
|---|---|
| `msiProfiles/Enigma.MarkdownEditor.Desktop.1.0.0.msipkg.json` | The first profile. `schemaVersion` 1, `scope` PerMachine, `installPath` `%ProgramFiles%\Enigma Markdown Editor`, `releasePath` `artifacts\publish\win-x64`, `compression` High, `outputPath` `artifacts`, `msiFilename` `EnigmaMarkdownEditor`, both shortcuts named **Enigma Markdown Editor** — your four confirmations plus the shortcut label. |
| `docs/done/FEATURE-5B28-PHASE02.md` | This file. |

### Modified

| File | Change |
|---|---|
| `RELEASENOTES.md` | Was an empty file. Now the `1.0.0` first-release section. |
| `README.md` | The what's-new callout, under the intro; `msiProfiles/` added to the *Repository layout* tree by the documentation freshness sweep. |
| `src/Enigma.MarkdownEditor.Desktop/…csproj` | `<Version>1.0.0</Version>`; the trailing CPM comment disambiguated. |
| `src/Enigma.MarkdownEditor.Core/…csproj` | Same. |
| `docs/RELEASE.md` | §4 working-directory requirement; §6 version-check correction. |
| `packaging/linux/enigma-markdown-editor.desktop` | Comment header reworded so the global substitution cannot rewrite it. |
| `docs/roadmap.md` | `PHASE02` → `IN PROGRESS`, then `PHASE02` and `FEATURE-5B28` → `DONE`. |
| `docs/plan/FEATURE-5B28.md` | Same status transitions. |

### Deliberately unchanged

- **The app icon** stays the placeholder, as in PHASE01 and by the plan's own fallback.
- **The README screenshots** are still empty slots. `docs/RELEASE.md` §1 carries the checkbox; the
  image block is commented out with its final paths, so dropping the captures in needs no edit.

## Deviations & follow-ups

- **The manual Linux verification is only half done, and the half that is missing is the important
  half.** Your decision, after the environment turned out not to support it. What was executed:
  unpack on ext4, `install.sh` end to end, the modes the installed files actually carry, the desktop
  entry's substitution, and what the application host is. What was **not**: that the application
  runs. The WSL image is a base Arch install — no `libfontconfig.so.1` and no X11 client library at
  all, `fc-list` reports zero fonts — so the process aborts inside SkiaSharp before Avalonia
  initialises, and installing the libraries needs a sudo password. The full failure is recorded
  below. Nothing about it implicates the artifact; it is the environment. **Before tagging, run
  `docs/RELEASE.md` §2 on a real Linux desktop.** On an Arch box the packages are
  `fontconfig libx11 libxext libxrandr libxcursor libxi libxrender libice libsm libglvnd noto-fonts`.
- **The `.desktop` comment fix is outside PHASE02's scope.** PHASE02 is version, profile and runbook;
  this is a PHASE01 deliverable. It was fixed here on your decision because it would otherwise have
  shipped in 1.0.0. Cosmetic only — nothing parses a `.desktop` comment.
- **The `.desktop` substitution is still global.** The reword removes the collision, but
  `sed "s|@EXEC@|…|g"` would rewrite any future second occurrence too. Anchoring it to the `Exec`
  line (`s|^Exec=@EXEC@|Exec=…|`) would make that structural rather than a matter of remembering.
  Not done — one fix per decision.
- **100 MB of the 227 MB win-x64 publish is native debug symbols.** `libSkiaSharp.pdb` is 80.1 MB and
  `libHarfBuzzSharp.pdb` 19.9 MB; the two managed `.pdb` files together are 0.1 MB. They ship because
  the native packages declare them as ordinary native assets, and the MSI will package them. The
  sibling repo already solved this — `Enigma.Msi.Desktop.csproj` has a
  `TrimNativeSymbolsFromPublish` target that drops native `.pdb` from `ResolvedFileToPublish` and
  keeps the managed ones, taking its own installed footprint from ~150 MB to ~49 MB. Worth copying,
  but it changes what ships and belongs to its own work item, not to a version bump.
- **No About box and no version in the window title.** Carried over from PHASE01; `RELEASE.md` §6
  works around it by checking the file version and the ARP entry. An About dialog would make the
  installed version directly observable, which is what a support request usually needs first.
- **No new automated tests.** This phase's deliverables are a version number, two markdown documents,
  a JSON profile and a comment. The xUnit suite has no harness for any of them, and the plan's
  acceptance criteria call for none. They were verified by execution instead — the profile against
  the real `Enigma.Msi` reader and validator, the packaging on Linux, the version on the built and
  published binaries. The existing 1722 tests still pass.
- **The profile validator harness is a throwaway.** It lives in the session scratchpad, references
  the sibling repo's already-built `Enigma.Msi.dll`, and is not committed. Nothing in this repository
  depends on Enigma.Msi, which is the point. If the profile format ever needs a standing check, that
  is a work item of its own.
- **Line endings: recommendation only, no action taken.** Unchanged from PHASE01 — this working tree
  is CRLF while the repository stores LF and `.gitattributes` already marks everything `eol=lf`, so
  git normalizes and the diffs here are clean. The three files rewritten wholesale by the patch
  scripts (`README.md`, `docs/RELEASE.md`, the `.desktop`) kept whatever endings they had. A
  `git add --renormalize .` on a clean tree would reconcile the checkout; that is yours to run.
- **No sub-agents were used.** Every piece of this phase turns on the same two facts — the version
  string and the profile's path semantics — so splitting it would have meant writing that context
  down more times than doing the work.

## Build/test evidence

**Build** — Release, zero warnings, and the AVLN check from the same build:

```
dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal
  Build succeeded.  0 Warning(s)  0 Error(s)

grep -i AVLN <that build's output>
  (no output)
```

**Tests** — full suite, Release configuration:

```
dotnet test --solution Enigma.MarkdownEditor.slnx -c Release
  Test run summary: Passed!  total: 1722  failed: 0  succeeded: 1722  skipped: 0
```

**The version reached the binaries** — read off the built and the published output, not off the
csproj:

| Binary | FileVersion | ProductVersion |
|---|---|---|
| `bin/Release/net10.0/Enigma.MarkdownEditor.Core.dll` | 1.0.0.0 | 1.0.0+bdafcd0… |
| `bin/Release/net10.0/Enigma.MarkdownEditor.Desktop.dll` | 1.0.0.0 | 1.0.0+bdafcd0… |
| `artifacts/publish/win-x64/Enigma.MarkdownEditor.Desktop.exe` | 1.0.0.0 | 1.0.0+bdafcd0… |

The `+<sha>` suffix is the SDK appending the source revision; it is why `RELEASE.md` §6 was corrected.

**The artifacts carry the version in their names** — both republished from the bumped csproj:

```
build/publish.ps1 -Rid win-x64     276 files, 227.3 MB
build/publish.ps1 -Rid linux-x64   272 files, 123.6 MB
packaging/linux/pack-linux.ps1     artifacts/EnigmaMarkdownEditor-1.0.0-linux-x64.tar.gz
                                   280 entries, 20 executable, 123.6 MB -> 49.7 MB
```

**The MSI profile, through the real `Enigma.Msi`** — a throwaway console app in the scratchpad
referencing the sibling repo's built assembly, so the reader and the validator are the ones that will
actually open the file:

| Check | Result |
|---|---|
| `MsiPackageJson.Deserialize` | Parsed; every field read back as written |
| `MsiPackageValidator.Validate` (model rules) | **valid** |
| `MsiPackageValidator.ValidateEnvironment`, cwd = repo root | **valid** — `releasePath`, `outputPath` and all three icon references resolve |
| `MsiPackageValidator.ValidateEnvironment`, cwd = `%TEMP%` | 5 errors — `install.releasePath`, `output.outputPath`, `controlPanel.productIcon`, `shortcuts[0].iconPath`, `shortcuts[1].iconPath` all "does not exist" |
| `MsiPackageJson.Serialize` vs. the file on disk | **byte-identical** — a save from Enigma.Msi.Desktop produces no diff |
| `productId` / `upgradeCode` | Both non-empty GUIDs, distinct, generated by `[guid]::NewGuid()` |

The foreign-working-directory row is the evidence behind the new §4 paragraph.

**Linux, the mechanical half** — the real `1.0.0` tarball, copied into WSL2 (Arch, glibc 2.44),
unpacked and installed on **ext4** with `HOME` redirected to a scratch directory. This closes the item
PHASE01 explicitly could not close: PHASE01 could verify the archive's recorded modes, but not that
the files come out executable on a real filesystem.

| Check | Result |
|---|---|
| Unpack | One top-level directory, `app/`, `share/`, `install.sh`, `LICENSE.md` |
| Modes as unpacked | `install.sh` 755 · `Enigma.MarkdownEditor.Desktop` 755 · `createdump` 755 · `LICENSE.md` 644 · 0 of 17 `.so` files not 755 |
| `install.sh` | Exit 0; printed the install and uninstall paths |
| Installed | 272 files under `.local/opt/enigma-markdown-editor` |
| Application host, installed | `-rwxr-xr-x 755`, executable — **on ext4, not an emulated mode** |
| Desktop entry, installed | 644, `Exec=` substituted to the absolute path, 0 placeholders left |
| Icon, installed | 644, in `hicolor/256x256/apps` |
| The application host itself | `ELF 64-bit LSB pie executable, x86-64 … dynamically linked … stripped`; `ldd` reports no missing library |
| Desktop entry comment header | Correct after the fix; re-verified by repacking and reinstalling |

The scratch directory was removed afterwards; nothing was left in the WSL home.

**Linux, the half that was not done.** The application does not start in this WSL image:

```
Unhandled exception. System.TypeInitializationException: The type initializer for
'SkiaSharp.SKImageInfo' threw an exception.
 ---> System.DllNotFoundException: Unable to load shared library 'libSkiaSharp' or one of its
      dependencies.
      libfontconfig.so.1: cannot open shared object file: No such file or directory
   at Avalonia.Skia.SkiaPlatform.Initialize(SkiaOptions options)
   at Enigma.MarkdownEditor.Desktop.Program.Main(String[] args)
```

Absent from the image: `libfontconfig.so.1`, `libX11.so.6`, `libXext.so.6`, `libXrandr.so.2`,
`libXcursor.so.1`, `libXi.so.6`, `libXrender.so.1`, `libICE.so.6`, `libSM.so.6`, `libGL.so.1`;
`fc-list` reports 0 fonts. WSLg itself is present (`DISPLAY=:0`, `WAYLAND_DISPLAY=wayland-0`), so it is
the distribution that is bare, not the display. Consequently **none** of `RELEASE.md` §2's application
checks were performed: launch, desktop-menu launch, workspace, tree operations and trash, edit and
save, the vim smoke set, preview and view modes, search, session restore, and the per-user folder. All
of them remain outstanding pre-flight work.
