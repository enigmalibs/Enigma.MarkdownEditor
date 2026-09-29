# FEATURE-4BB7 — Linux installer & uninstaller

**Status: TODO** · Single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-4bb7-linux-installer`, cut from the run branch at build time.
**Run:** feature/2026-09-29-linux-installer-release

## Objective

Install Enigma Markdown Editor on Linux the way Enigma.GitClient is installed
(`Enigma.GitClient/packaging/linux/`, its `FEATURE-B4C0`): one bash command that builds the
application from this checkout — or takes an already-built copy — and installs it for the current
user into the XDG directories, with the application in `~/.local/share`, a launcher symlink in
`~/.local/bin`, a desktop entry and its icon in six hicolor sizes; and an `uninstall.sh` that takes
exactly that away again and never touches the user's settings.

The release tarball keeps shipping, re-laid out around the new installer, so the **same two scripts**
work from a checkout and from the unpacked tarball.

## Context & constraints

Established by reading both repositories, not assumed.

- **The reference** is GitClient's `packaging/linux/`: `install.sh` (bash, `set -euo pipefail`,
  refuses root, validates `XDG_DATA_HOME` / `XDG_BIN_HOME` as non-empty absolute paths, `--from DIR`,
  `--framework-dependent`, `--rid RID`, `--help`; publishes Release, self-contained by default, into a
  staging directory beside the install directory and swaps it in with a rename, restoring the previous
  install if the swap fails; symlinks the apphost; installs six icons; writes the desktop entry from a
  `.desktop.in` template by substituting `@EXEC@` in bash rather than sed, the launcher quoted; pins
  `DOTNET_ROOT` in `Exec` when a framework-dependent tree's runtime is not where an apphost looks;
  refreshes the desktop, icon and KDE caches best effort; notes when the bin directory is off `PATH`),
  `uninstall.sh` (removes exactly those four things, refuses to remove a directory not named after the
  app id, leaves a symlink alone that points elsewhere, names the configuration directory as left
  untouched), `enigma-git-client.desktop.in`, and `icons/enigma-git-client-<N>.png`.
- **What this repository has today** (`packaging/linux/`): a POSIX-`sh` `install.sh` that only runs
  from inside the unpacked tarball (it expects `app/` and `share/` beside it), installs into
  `~/.local/opt/enigma-markdown-editor` (or `--prefix DIR`), one 256 px icon, the desktop entry as
  `enigma-markdown-editor.desktop` with `Exec=@EXEC@` substituted by `sed`, **no uninstaller** (it
  prints `rm` commands), and `pack-linux.ps1`, which writes the tarball header by header so the mode
  bits are right from either OS. All four files are `100644` in the index.
- **The application:** project `src/Enigma.MarkdownEditor.Desktop`, apphost
  `Enigma.MarkdownEditor.Desktop`, app id `enigma-markdown-editor` (`build/Common.ps1` `PackageId`),
  configuration in `$XDG_CONFIG_HOME/Enigma.MarkdownEditor`. It **opens nothing given on its command
  line** (`Program.Main` forwards `args` to the Avalonia lifetime and nothing reads them), so the entry
  keeps no `%F` and no `MimeType=`. It has no external runtime prerequisite: `gio trash` is tried
  first and a built-in freedesktop trash is the fallback.
- **Icon art:** `Assets/app.ico` holds four 32-bit BMP frames — 16, 32, 48, 256 — and
  `Assets/app-256.png` and `packaging/linux/enigma-markdown-editor.png` are pixel-identical to its
  256 frame. There is no vector source.
- **Tooling on this machine:** `dotnet` 10.0.103, `bash`, `desktop-file-validate`, `python3` with
  Pillow. **No PowerShell**, so `pack-linux.ps1` cannot be executed here.
- `.gitattributes` already declares `* text=auto eol=lf`, `*.sh text eol=lf` and `*.png binary`.
- `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §6 (post-release) and the
  README's *Installation*, *Build from source* and *Repository layout* sections describe the tarball
  installer and must follow it.

## Design

**1 — Icons** — `packaging/linux/icons/enigma-markdown-editor-<N>.png` for N in 16 32 48 64 128 256.
16, 32 and 48 are the `app.ico` frames, decoded and written as PNG; 256 is the existing
`packaging/linux/enigma-markdown-editor.png`, moved (`git mv`) so its history follows; 64 and 128
are Lanczos resamples of the 256 art. Each verified to be RGBA at its nominal size.

**2 — Desktop-entry template** — `packaging/linux/enigma-markdown-editor.desktop` renamed to
`enigma-markdown-editor.desktop.in`, content kept (`Name=Enigma Markdown Editor`,
`GenericName=Markdown Editor`, `Categories=Utility;TextEditor;`, the keywords, the comment on why
there is no `%F`), plus GitClient's `StartupNotify=true` — safe because `StartupWMClass` lets the
desktop end the notification when the window maps. The comment is reworded for the bash substitution
and still never spells the placeholder out.

**3 — `packaging/linux/install.sh`** — GitClient's installer with this application's id, apphost,
project, configuration directory and wording, and three additions:

- **Bundled copy.** With no `--from`, when `app/Enigma.MarkdownEditor.Desktop` sits beside the script
  (the unpacked release tarball), that directory is the source; otherwise it builds from the checkout.
- **Build flags need a build.** `--rid` or `--framework-dependent` with a prebuilt source (`--from`
  or the bundled copy) is refused with a message, rather than silently ignored.
- **The 1.x layout is retired.** After the new install is in place, an install at the old default
  `~/.local/opt/enigma-markdown-editor` — recognised by the apphost inside it — is removed, and the
  installer says so. Its desktop entry and 256 px icon sat at the paths the new install overwrites.

The final message names the terminal command, the `PATH` note, where the settings live, and
`uninstall.sh`.

**4 — `packaging/linux/uninstall.sh`** — GitClient's uninstaller, adapted likewise; it also removes a
recognisable 1.x install at `~/.local/opt/enigma-markdown-editor`, and leaves
`$XDG_CONFIG_HOME/Enigma.MarkdownEditor` alone.

**5 — Modes** — both scripts `100755` in the index, so `./packaging/linux/install.sh` runs from a
checkout.

**6 — `packaging/linux/pack-linux.ps1`** — the tarball becomes `packaging/linux` plus the publish:

```
EnigmaMarkdownEditor-<version>-linux-x64/
  install.sh                          0755
  uninstall.sh                        0755
  LICENSE.md                          0644
  enigma-markdown-editor.desktop.in   0644
  icons/enigma-markdown-editor-<N>.png  0644 (six)
  app/                                the self-contained publish, modes as today
```

Only the entry list, the required-files check and the doc comment change; the ustar writer is
untouched.

## Acceptance criteria

- `bash -n` parses both scripts; neither contains `sudo`; both are `100755` in the index.
- `desktop-file-validate` accepts every desktop entry the installer writes, with no error and no
  warning; `Exec` is the quoted absolute path of the installed apphost.
- **From a checkout**, against a scratch `HOME` and XDG root: the install directory holds a runnable
  self-contained apphost, the symlink resolves to it, the six icons sit in their hicolor directories,
  and the installed application starts from a bare `env -i` environment.
- **Reinstall** replaces the installation: a stale file planted in the old tree is gone, and no
  staging or `.previous` directory is left behind.
- **`--from DIR`** installs a directory published separately; **the tarball layout** (built by hand
  in the scratchpad exactly as `pack-linux.ps1` lays it out) installs from its bundled `app/` with no
  `dotnet` involved; `--rid` with either is refused.
- **Migration:** a planted 1.x install at `$HOME/.local/opt/enigma-markdown-editor` is removed by
  the installer and by the uninstaller; a directory there that holds no apphost is left alone.
- `uninstall.sh` removes the four locations and leaves a scratch `settings.json` byte-identical; a
  second run reports there is nothing to remove; unknown options exit non-zero.
- README *Installation* / *Build from source* / *Repository layout* and `docs/RELEASE.md` §2/§4/§6
  describe the new installer.
- Build clean with zero warnings; the whole suite green.

## Out of scope

`.deb`, `.rpm`, Flatpak, AppImage, a system-wide install · a `linux-arm64` release tarball · `.md`
file association (`MimeType=`, `%F`) — the application opens nothing from its command line · a shell
test framework · Windows packaging.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| "Like done in Enigma.GitClient" | GitClient's `install.sh` / `uninstall.sh` / `.desktop.in` / six-icon layout, adapted | That is the installer the draft points at, and it carries lessons already paid for (runtime lookup, atomic swap) | Extending the POSIX `sh` tarball installer |
| The existing release tarball | Kept, re-laid out as `packaging/linux` + `app/` so the same scripts serve both | The tarball is a shipped artifact on the Releases page; dropping it was not asked for | Dropping it (GitClient ships none); two installers side by side writing the same desktop entry |
| Install locations | `$XDG_DATA_HOME/enigma-markdown-editor`, `$XDG_BIN_HOME` symlink, hicolor icons | GitClient parity; XDG-honouring; the symlink gives a terminal command | Keeping `~/.local/opt` |
| Upgrading from 1.x | Remove a recognisable `~/.local/opt/enigma-markdown-editor` after the new install lands | Otherwise every upgrader keeps a dead ~100 MB copy; the old installer itself replaced that directory wholesale | Leaving it with a note; ignoring it |
| `--prefix` | Dropped; `XDG_DATA_HOME` is honoured | GitClient has none; an uninstaller cannot find an arbitrary prefix | Keeping it as an alias |
| Icon sizes | 16, 32, 48, 64, 128, 256 — frames where the art exists, Lanczos for 64/128 | The standard hicolor set GitClient installs; no vector source to render from | Only the four sizes that exist; 256 alone (today) |
| Publish default | Self-contained, `--framework-dependent` opt-in | GitClient/Enigma.Tasks' verified finding: a framework-dependent launcher does nothing when the runtime lives in `~/.dotnet` | Framework-dependent by default |
| Build flags with a prebuilt copy | Refused with a message | Silently installing self-contained after `--framework-dependent` was asked for would surprise | Ignoring them, as GitClient does with `--from` |
| `StartupNotify` | `true`, as GitClient | `StartupWMClass` is already set, so the desktop can end the notification when the window maps | Leaving it absent |
| `pack-linux.ps1` verification | By inspection, plus the tarball layout rebuilt by hand and installed from | No PowerShell on this machine; `docs/RELEASE.md` §2 runs the real packer every release | Installing PowerShell (new tooling) |
| How it is verified | `bash -n`, `desktop-file-validate`, real installs into a scratch `HOME`/XDG root, a start from `env -i` | GitClient's Definition of Done for scripts, without a shell-test framework the project does not have | A `bats` suite |
