# FEATURE-4BB7 — Linux installer & uninstaller

**Status: DONE** · Single phase · Branch: `feature/feature-4bb7-linux-installer` (cut from
`feature/2026-09-29-linux-installer-release`)
Plan: `docs/plan/FEATURE-4BB7.md` · Roadmap: `docs/roadmap.md`

## Summary

Enigma Markdown Editor now installs on Linux the way Enigma.GitClient does. `packaging/linux/install.sh`
is GitClient's installer adapted to this application. From a checkout it builds a self-contained
Release publish. It then swaps the publish into `$XDG_DATA_HOME/enigma-markdown-editor` from a staging
directory, symlinks `$XDG_BIN_HOME/enigma-markdown-editor` to the apphost, installs six hicolor icons,
and writes the desktop entry from `enigma-markdown-editor.desktop.in`. `packaging/linux/uninstall.sh`
removes exactly those four things and leaves `~/.config/Enigma.MarkdownEditor` alone.

**The release tarball keeps shipping, and the same two scripts serve it.** The tarball is now
`packaging/linux` as it stands in the repository plus `app/`, which is the self-contained publish.
When `install.sh` finds `app/Enigma.MarkdownEditor.Desktop` beside itself, it installs that directory
instead of building, so inside the unpacked tarball it needs no `dotnet` at all.
`pack-linux.ps1` changed only its entry list, its required-files check and its doc comment. The ustar
writer is untouched.

**Upgrading from 1.x cleans up after itself.** The old tarball installer put the application in
`~/.local/opt/enigma-markdown-editor`. Its desktop entry and 256 px icon sat at the paths the new
installer overwrites, so after an upgrade the old application directory would have been a ~125 MB
copy that nothing launches. `install.sh` removes that directory once the new install is in place, and
so does `uninstall.sh`. Both treat it as ours only when it holds the apphost; a directory there
without one is left alone.

## Files/modules touched

**Created**

- `packaging/linux/uninstall.sh` — mode 755.
- `packaging/linux/icons/enigma-markdown-editor-{16,32,48,64,128}.png` — 16, 32 and 48 are the
  `Assets/app.ico` frames (32-bit BMP) decoded and written as PNG; 64 and 128 are Lanczos resamples of
  the 256 art. All RGBA at their nominal size, checked on a contact sheet.
- `docs/done/FEATURE-4BB7.md` — this document.

**Moved**

- `packaging/linux/enigma-markdown-editor.png` → `packaging/linux/icons/enigma-markdown-editor-256.png`
  (`git mv`, byte-identical; pixel-identical to `app-256.png` and to the ico's 256 frame).
- `packaging/linux/enigma-markdown-editor.desktop` → `…desktop.in`. The entry keeps its content and
  gains `StartupNotify=true`, and the header comment is reworded for the bash substitution. It still
  never spells the placeholder out.

**Modified**

- `packaging/linux/install.sh` — rewritten from POSIX `sh` to GitClient's bash installer; mode 644 →
  755.
- `packaging/linux/pack-linux.ps1` — the new tarball layout (`uninstall.sh`, `.desktop.in`, `icons/`;
  no more `share/`).
- `README.md` — *Installation → Linux* rewritten (tarball and checkout, the four locations, the
  options, the upgrade, the uninstaller); *Repository layout*'s `packaging/linux/` line.
- `docs/RELEASE.md` — §2's first and last checklist items: installing from the bundled `app/`, the
  terminal command, and `./uninstall.sh` instead of "the printed commands".

**Workflow artifacts** — `docs/roadmap.md` (`TODO` → `IN PROGRESS` → `DONE`) and the plan file's
status.

## Deviations & follow-ups

- **Two small, deliberate differences from GitClient's scripts.**
  - Passing `--rid` or `--framework-dependent` together with a prebuilt source (`--from`, or the
    bundled copy) is **refused**. GitClient silently ignores them. This follows the plan.
  - GitClient's `[[ -n ${data_home} ]] || die "XDG_DATA_HOME is set to an empty value."` checks were
    **replaced by a comment**. They can never fire: `${XDG_DATA_HOME:-…}` treats an empty value as
    unset, which is also the XDG spec's rule. The same dead code is in GitClient and Enigma.Tasks, and
    is worth the same one-line cleanup there.
- **That dead check cost a mistake during verification, now undone.** A test run with
  `XDG_BIN_HOME=` (empty) expected a refusal. Instead the script fell back to the defaults and
  installed into the **real** `~/.local/share`, `~/.local/bin`, the applications directory and the
  hicolor theme.
  - None of those paths existed before this run; that was checked at planning time.
  - It was removed at once with this dev's own `uninstall.sh`, and all four locations were confirmed
    gone.
  - The real `~/.config/Enigma.MarkdownEditor` hashes identical before and after the uninstall; the
    installer never writes configuration.
  - The only lasting effect is that the desktop and icon caches (`update-desktop-database`,
    `gtk-update-icon-cache`, `kbuildsycoca6`) were refreshed twice.
  - Every other run used a scratch `HOME` or a scratch XDG root.
- **`pack-linux.ps1` was not executed.** There is no PowerShell on this machine. The change was
  verified by inspection, staying inside the 5.1 subset `build/Common.ps1` documents. The layout it
  writes was then rebuilt by hand in the scratchpad (modes as the packer sets them), round-tripped
  through a real `tar czf` / `tar xzf`, and installed from with no `dotnet` on `PATH`. The real packer
  runs in `docs/RELEASE.md` §2 every release, and that is the first place a mistake in it would show.
- **`--prefix` is gone**, as in GitClient; `XDG_DATA_HOME` is honoured instead. That belongs in the
  1.7.0 release notes under *Changes* (`FEATURE-3D59`).
- **Found, not fixed — pre-existing:** the application crashes at start when its configuration base
  directory does not exist. `Environment.GetFolderPath(ApplicationData)` returns `""` for a missing
  directory, and `ConfigPaths.InSpecialFolder` throws `InvalidOperationException`. This came up only
  because the first `env -i` start pointed `XDG_CONFIG_HOME` at a directory not yet created. A real
  desktop always has `~/.config`, so it is not an installer concern. It could be a small `BUG` item
  (create the base directory, or ask for `SpecialFolderOption.Create`).
- **Scratch-root side effect, not an installation artifact:** `dotnet publish` inherits
  `XDG_DATA_HOME`, so NuGet's and Avalonia's build caches land in whatever data directory the
  installer is pointed at. GitClient's completion doc records the same.
- Empty `hicolor/<N>x<N>/apps` directories are left behind by the uninstaller, as by GitClient's; they
  belong to the icon theme.
- Not verifiable here: running as root is refused by the first check of both scripts, and was not
  exercised.
- **Planning-time slip, recorded for honesty:** before the planning commit, `git checkout --
  docs/roadmap.md` was used to discard this run's own uncommitted edit to the roadmap and re-apply it.
  `vibe-workflow` §5 forbids that on a file the run did not create. It discarded nothing but that
  seconds-old edit.
- **No line-ending noise.** `git diff --check` is clean, and every text file this dev touched is LF
  throughout, as `.gitattributes` requires.

## Build/test evidence

There is nothing to build or test in the scripts themselves. They were verified against scratch roots
in the session scratchpad:

| Check | Result |
|---|---|
| `bash -n` on both scripts | clean; `sudo` appears only in two comments |
| Index modes | both scripts `100755` |
| Install from the checkout, self-contained (scratch XDG root) | 125 MB, 272 files, `libcoreclr.so` bundled; apphost and `createdump` mode 755; the symlink resolves to the executable apphost; 6 icons in their hicolor directories; `Exec="<absolute apphost>"`; no staging or `.previous` directory left |
| `desktop-file-validate` on every written entry | no error, no warning (self-contained, framework-dependent and tarball installs) |
| Installed app started from `env -i` (`HOME`, `USER`, `PATH=/usr/bin:/bin`, the display variables, a scratch `XDG_CONFIG_HOME`) | started. The log reads `Enigma Markdown Editor started.` → `First run: no settings file, showing the welcome dialog.` → first run finished; it shut down cleanly on `SIGTERM` |
| Reinstall (build again) | "Replacing …"; a stale file planted in the old tree is gone |
| `--from` a separately published directory | installs it, "Copying from …" |
| `--framework-dependent` | 46 MB; `Exec=env DOTNET_ROOT="/home/jo/.dotnet" "<apphost>"`, because this machine has neither `/usr/share/dotnet` nor `/etc/dotnet` |
| Tarball layout (`tar czf`/`xzf` round trip), scratch `HOME`, no `dotnet` on `PATH` | installs from the bundled `app/`; the planted 1.x install in `~/.local/opt/enigma-markdown-editor` is removed |
| A directory at the legacy path holding no apphost | left alone by `install.sh` and by `uninstall.sh` |
| `--rid` / `--framework-dependent` with `--from` or inside the tarball | refused, exit 1 |
| `--bogus`, `--from /nonexistent`, `--from` a directory without the apphost, a relative `XDG_DATA_HOME` | each refused with its message, exit 1 |
| `uninstall.sh` | the four locations and a planted 1.x install removed; the scratch `settings.json` byte-identical (sha256); a second run says there is nothing to remove; a symlink pointing elsewhere is left in place |

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **0 Warning(s), 0 Error(s)**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3582 passed, 0 failed, 0
  skipped**. No code changed; no fix cycles were used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `createdump`'s mode | Re-applied to 755 beside the apphost, as the 1.x installer did | Cheap, and it covers an unpack that dropped the bits; `.so` files need no execute bit to be loaded, so GitClient's apphost-only rule plus this one is enough |
| Where the uninstaller is found after a tarball install | "beside this script, and any copy of it will do" | `uninstall.sh` depends only on the XDG paths, so the one in any tarball or checkout works; copying it into the application directory would mix a non-publish file into the tree |
| The closing hint | `enigma-markdown-editor`, with no argument | The application opens nothing given on its command line |
| A prerequisite note, as GitClient's git note | None | The application has no external runtime prerequisite: `gio trash` is optional, with a built-in fallback |
| Roadmap Status column width | Kept at `IN PROGRESS`'s width | That is how the committed table has always been padded; shrinking it would rewrite every row on every status flip |
