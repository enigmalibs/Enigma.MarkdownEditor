# Release runbook

Checklist for cutting a new **Enigma Markdown Editor** version. This is an application, not a
library: there is **no `dotnet pack` and no `dotnet nuget push`** anywhere below. A release ships two
artifacts, both self-contained, attached to a GitHub Release:

| Artifact | Built by |
|---|---|
| `EnigmaMarkdownEditor.msi` (win-x64) | `build/publish.ps1` → Enigma.Msi |
| `EnigmaMarkdownEditor-X.Y.Z-linux-x64.tar.gz` | `build/publish.ps1` → `packaging/linux/pack-linux.ps1` |

Replace `X.Y.Z` with the version being released (e.g. `1.0.0`) throughout. It lives in `<Version>` in
**both** `src/*` project files. Run every command **from the repository root** — the MSI profile's
paths are repo-root-relative.

There is no CI. Every build, and both platform verifications, happen locally — which is why §2 is a
manual checklist rather than a pipeline, and why it is not optional.

## 1. Pre-flight

On the release branch, before merging:

- [ ] `<Version>X.Y.Z</Version>` set in `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj`
      **and** `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj`.
- [ ] `RELEASENOTES.md` has a top `X.Y.Z` section (newest-first; any `(unreleased)` heading renamed
      to `X.Y.Z`).
- [ ] The README what's-new callout reads `X.Y.Z`.
- [ ] The README screenshots exist and are current — `docs/images/screenshot-dark.png` and
      `docs/images/screenshot-light.png`, with the image block in *Screenshots* uncommented.
- [ ] Clean, warning-free build:
      ```bash
      dotnet build Enigma.MarkdownEditor.slnx -c Release
      ```
      The bar is **0 Warning(s)**. `TreatWarningsAsErrors` is on solution-wide, so a C# warning fails
      this outright.
- [ ] **No Avalonia XAML diagnostics.** The XAML compiler's `AVLN*` codes are the ones a green build
      can still hide — compiled bindings report a missing `x:DataType` as `AVLN2100`, and a binding
      that silently stops resolving is exactly the kind of regression no unit test sees:
      ```bash
      dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN
      ```
      Expect no output.
- [ ] Full test suite green:
      ```bash
      dotnet test --solution Enigma.MarkdownEditor.slnx -c Release
      ```
      The `--solution` flag is **not optional**: this repo runs the Microsoft Testing Platform runner
      (see `global.json`), and on the .NET 10 SDK in MTP mode a bare `dotnet test <solution>` is
      rejected.
- [ ] **The unsaved dot clears on a save made in insert mode.** With vim on: `i`, type something,
      then `Ctrl+S` — the tab's dot goes, stays gone across `Esc`, and closing the tab afterwards
      asks nothing. Repeat for `:w` and for autosave, and confirm the file on disk has the text each
      time. Platform-independent, which is why it sits here rather than in §2's Linux smoke set.
      This is what `BUG-38F5` was, and it shipped in 1.4.0 over a green suite: the save always wrote
      the file and only the flag was wrong, so nothing failed — the dot is the only place it showed.
- [ ] **The preview pane copies, in both flavours.** Open a note with headings, bold, a link, a GFM
      table, a task list and a fenced code block. Click in the preview, `Ctrl+A`, `Ctrl+C`. Paste
      into a plain editor — the text arrives markup-free, with the table's rows tab-separated.
      Paste into **Word or Outlook** — the headings, bold, links and table survive as formatting,
      and nothing arrives pale-on-white from the dark theme. Then repeat the copy from the
      `PREVIEW` header's button and from the pane's right-click *Copy*, and confirm both are
      disabled with no tab open. Platform-independent, which is why it sits here rather than in
      §2's Linux smoke set — though §2 is where the X11 `text/html` target gets its only exercise.
      The rich-text flavour's *bytes* are covered by tests, down to the CF_HTML offsets, but no
      test can watch another application render them, and nothing in the suite builds
      `MainWindow`, so the button and the menu have no automated coverage at all. This is what
      `FEATURE-4797` left open, and it shipped in 1.5.0 over a green suite for exactly that
      reason.
- [ ] **A new note arrives with its own title.** *New file…* in the tree, name it `stand-up.md`, and
      the tab that opens shows `# stand-up` and a blank line under it — the file on disk holds exactly
      that and nothing more. Repeat with a name carrying dots (`2026-09-10.notes.md`, which must give
      `# 2026-09-10.notes`) and with a non-markdown name (`data.json` with *Show all files* on, which
      must arrive empty). Platform-independent. The bytes are covered by tests; what no test can do is
      confirm the tab, the caret and the preview all agree with them. This is what `FEATURE-E64A`
      left open.
- [ ] **A folder colour reaches the tree and the tab strip, and survives a restart.** Right-click a
      folder → *Colour* → pick one: the folder gets a bar beside its name, and a note opened from
      inside it — including from a sub-folder — gets the same bar at its tab's leading edge. Colour a
      second folder differently, open notes from both, and confirm the strip tells them apart at a
      glance; confirm the tab widths did not change and the strip did not shift. Colour a sub-folder
      of a coloured folder and confirm the sub-folder's colour wins under it. Then: colour a folder
      **while a note from it is already open** (the tab must recolour without reopening), *Save as*
      that note into an uncoloured folder (the tab must lose the colour), switch workspaces and back,
      and restart the editor. Finally check the *Colour* item is disabled on a file and enabled on a
      workspace root. Platform-independent. Every binding, brush and re-resolve is covered by tests
      through a headless window; whether 4px of colour actually reads at a glance — on the selected
      tab in particular, whose background is lighter — is a judgement only a person at the window can
      make. This is what `FEATURE-A225` left open.
- [ ] **Vim `o` and `O` continue a list.** With vim on, in normal mode on `- item`: `o` opens
      `- `, `O` opens `- ` above it. Repeat on `1. item` (`o` gives `2. `, `O` gives `1. `), on
      `- [x] done` (both give `- [ ] `), on `> quoted`, and on an indented item. On an empty `- `,
      both must open a plain line and leave the item alone. Then `3ox<Esc>` (three continued items,
      numbering as it goes), `.` after an `o`, and one `u` taking the marker back with what was typed
      on it. Platform-independent — §2's vim smoke set is where the keys get their Linux exercise.
      This is what `FEATURE-3505` left open.
- [ ] **Code spans read as chips, in both themes.** Open a note with a `` `code span` `` in a
      paragraph, in a block quote and in a table header row. In the dark theme each chip is lighter
      than what surrounds it, in the light theme darker, and in all three places the text has a
      little space before its chip's edges; a long line never wraps between a chip and its padding.
      Then drag-select across a sentence holding a chip, copy, and paste into a plain editor: the
      text arrives without the padding. Platform-independent. The colour's resolution and the
      padding's indices are covered by tests, but the headless suite renders without Skia, so no
      test can say how the chip actually looks. This is what `BUG-18AD` left open.
- [ ] **Every delete asks first.** Select a note in the tree and press `Delete`: a dialog titled
      *Delete 'name'?* opens, saying it goes to the recycle bin, and nothing has left the tree yet.
      *Cancel* — the note is still there. Repeat and press `Escape`, then repeat and click outside
      the dialog: still there each time. Repeat from the right-click *Delete*, and this time press
      *Delete*: the row goes and the note is in the recycle bin. Then `Ctrl`+click three entries
      (one of them a folder with notes in it) and delete them: **one** dialog, titled *Delete 3
      items?*, naming all three; *Cancel* keeps all three, *Delete* takes all three. Select seven
      and confirm the dialog names five and says *…and 2 more.* Platform-independent — §2's tree
      step is where the trash's own name gets its Linux check. Which question is asked, and what it
      says, is covered by tests through a fake dialog host; that the real dialog opens on the key and
      the menu, takes focus, and closes on `Escape` is not, because nothing in the suite builds
      `MainWindow`. This is what `FEATURE-47F5` left open.
- [ ] **The icon reads in the Windows shell, against a dark and a light taskbar.** Run the Release
      build and check the window's title bar and its taskbar button, then `Alt`+`Tab`. Check the
      `.exe` in Explorer at the *Small*, *Medium* and *Extra large* views (these use the 16, 48 and
      256 px frames). Repeat with Windows switched to the other app/taskbar theme. The mark is a
      dark square, so on a dark taskbar its rounded edge and the outlined box must still stand out.
      The About dialog shows the same mark, from `Assets/app-256.png`. Windows caches
      icons per path, so an `.exe` that still shows the previous icon after a rebuild is the cache
      (`ie4uinit.exe -show` refreshes it), not the build. `AppIconTests` checks the `.ico`'s frame
      set and the PNG's size, but the headless suite renders without Skia, so no test can say how the
      icon looks. This is what `FEATURE-EA38` left open; §2's desktop-menu item covers the Linux
      launcher.
- [ ] `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json` committed — see §4 for the clone
      rule.

## 2. Manual Linux verification

**Required, every release.** There is no CI and the Windows machine that builds the release never
runs the Linux artifact, so this is the only thing standing between a broken `linux-x64` build and a
published one. Run it under WSLg or a Linux VM, from the real tarball, not from a `dotnet run`:

```bash
./build/publish.ps1 -Rid linux-x64
./packaging/linux/pack-linux.ps1
# copy artifacts/EnigmaMarkdownEditor-X.Y.Z-linux-x64.tar.gz to the Linux machine
tar xzf EnigmaMarkdownEditor-X.Y.Z-linux-x64.tar.gz
cd EnigmaMarkdownEditor-X.Y.Z-linux-x64
./install.sh
```

- [ ] `install.sh` completes without error, installs from the bundled `app/` without building
      anything, and names `uninstall.sh`. `enigma-markdown-editor` in a terminal starts the
      application (with `~/.local/bin` on the `PATH`).
- [ ] **From a checkout on the same machine**, `./packaging/linux/install.sh` builds the application
      and installs it over the tarball's install ("Replacing …"); the menu entry still launches it,
      now as the checkout's build. This is the installer's second source, and the only one that
      runs `dotnet publish`.
- [ ] The application appears in the desktop menu, with its icon, and launches from there — not only
      from the terminal. (This is what proves the `.desktop` `Exec` substitution and the icon
      install; a terminal launch proves neither.)
- [ ] **Launch** — the window opens, at a sane size, with all three panes.
- [ ] **About** — the info button in the toolbar opens the dialog, **with the application icon drawn
      in it**, plus the name, the version, the build line and the copyright. The icon is the point:
      it is a PNG resource decoded by Skia, and a bitmap that fails to decode draws nothing and logs
      nothing, so this is the only place a broken `Assets/app-256.png` on Linux would show. `Escape`
      closes it.
- [ ] **Workspace** — add a folder, and the tree lists it. Filter the tree. Change a file outside the
      editor and the tree notices.
- [ ] **Tree operations** — create, rename and move a note. **Delete one, answer the confirmation
      (it must say *trash*, not *recycle bin*), and confirm it lands in the system trash**, not
      nowhere; this is the operation most likely to differ from Windows.
- [ ] **Edit and save** — type, `Ctrl+S`, and the file on disk changes. Open a file with CRLF endings
      and a BOM, save it, and confirm both survived.
- [ ] **Vim smoke set** — `i` … `Esc`, `o` and `O` on a list item (the marker follows), `dw`,
      `3dd`, `yy` `p`, `v` motion `d`, `f<c>` then `;`, `diw`,
      `ci(`, `W`, `d}`, `>>` then `<<`, `gUiw`, `J`, `Space` and `Backspace` across a line break,
      `Home`/`End` in both modes, `yiw` then `viwp` on another word (the two swap), `Vjr-`, `vjJ`,
      `gv` after each of those, `"ayy` then `"ap`, `x` then `.`, `3ix<Esc>`, `qadwq` then `@a` (the
      status bar reads `recording @a` while recording), `/text` `n` `N`, `?text`, `*` on a word,
      `d/text`, `v/text` (the selection grows to the match), `:%s/a/b/g` (the status bar counts the
      substitutions; `u` takes them all back at once), `:set ic` then `/TEXT`, `:42`, and in a note
      longer than the window: `Ctrl+D` then `Ctrl+U` (the caret comes back to its column), `Ctrl+F`
      and `PageDown` (a page each, caret on the new top line), `zt` then `zz` on a mid-file line (the
      line goes to the top, then to the middle, and stays put), `H` and `L`, `Ctrl+E` held for a few
      lines (the view keeps moving), `Ctrl+B` in insert mode (still bold); then `:w`, `:q`,
      `:q!` on a dirty tab (no dialog), `ZZ`.
      The mode chip in the status bar tracks each change. The find and text-object keys earn their
      place on this platform in particular: both read the key *after* them, through the platform's own
      text input, so a layout or a dead key is resolved before the engine ever sees it — which is the
      part that differs from Windows. `>>` is worth typing here too: its width and its tabs-or-spaces
      follow the editor's own indentation options, seeded when the engine attaches. So are the scroll
      keys: they are read from the key code with Control held, and they move the view through
      AvaloniaEdit's text view directly, which a headless test can only prove so far.
- [ ] **Preview** — a note with headings, a GFM table, a fenced code block, a task list and an image
      renders correctly; `Ctrl+Shift+V` cycles editor → split → preview; in split, scrolling the
      editor carries the preview.
- [ ] **Ticking a task from the preview** — click a box in the preview and confirm the character
      between its brackets changes in the editor, the tab goes dirty, and `Ctrl+Z` takes it back in
      one step. Repeat on a task nested in a sub-list and on one inside a block quote.
- [ ] **Search** — a workspace-wide search returns name matches first, and clicking a hit opens the
      note with the match selected.
- [ ] **Session restore** — close with several tabs open, reopen, and the tabs, the active one, the
      window placement and the pane sizes all come back.
- [ ] **Settings and logs are written** — change a setting, confirm it survives a restart, and
      confirm `~/.config/Enigma.MarkdownEditor/` holds `settings.json` and `logs/app.log`.
- [ ] `./uninstall.sh` removes the application, the command, the desktop entry and the icons; the
      menu entry disappears, and `~/.config/Enigma.MarkdownEditor/` is still there.

## 3. Merge and tag

Merge the release branch into `main`, then locally:

```bash
git switch main
git pull
```

The tag convention is a **bare `X.Y.Z`**, no `v` prefix.
Tag the merge commit and push the tag:

```bash
git tag X.Y.Z
git push origin X.Y.Z
```

## 4. Build the artifacts

Both start from a publish. The publish output is what actually ships — the MSI harvests it and the
tarball wraps it — so a stale `artifacts/publish/` silently packages the previous version. The
scripts clean it for you unless you pass `-NoClean`. (`packaging/windows/publish.*` and
`packaging/linux/publish.sh` are not these: they publish a framework-dependent ReadyToRun build into
`bin/` for local use, and nothing here reads their output.)

```bash
./build/publish.ps1 -Rid win-x64      # -> artifacts/publish/win-x64
./build/publish.ps1 -Rid linux-x64    # -> artifacts/publish/linux-x64
./packaging/linux/pack-linux.ps1      # -> artifacts/EnigmaMarkdownEditor-X.Y.Z-linux-x64.tar.gz
```

Both scripts run under Windows PowerShell 5.1 and PowerShell 7+, and either runtime identifier can be
published from either OS. `artifacts/` is git-ignored, so none of this pollutes the tree.

### The MSI

Open `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json` in **Enigma.Msi.Desktop** and
build it. The profile is that application's native `.msipkg.json` format, so it opens, validates and
builds without anything being added to this repository — which is why this repo takes no dependency
on Enigma.Msi.

**Launch it from the repository root.** Every path in the profile is relative, and Enigma.Msi
resolves them against its own working directory — it does not rebase them onto the profile's folder.
Started from its start-menu shortcut it will report `install.releasePath: Directory does not exist.`
and four more like it. From a PowerShell prompt at the repository root:

```powershell
Start-Process "$env:ProgramFiles\Enigma.Msi\Enigma.Msi.Desktop.exe" -WorkingDirectory (Get-Location)
```

The paths stay relative deliberately: the profile is committed and cloned by every future release,
so it must not carry this machine's checkout path.

Building an MSI needs Windows and the WiX CLI (`dotnet tool install --global wix`).

The profile's `releasePath` is `artifacts\publish\win-x64` — the self-contained publish folder, not
`bin\Release\net10.0`. That is deliberate: the raw build output carries `runtimes/` for every
platform Skia and HarfBuzz support, which the publish step has already resolved down to win-x64.
**Run the win-x64 publish before building the MSI**, every time.

#### The profile clone rule

Each version gets its **own committed profile**. To make the next one, copy the most recent
`msiProfiles/Enigma.MarkdownEditor.Desktop.*.msipkg.json` and change exactly three things:

| Field | Rule |
|---|---|
| `upgradeCode` | **Keep verbatim.** It is the application's permanent identity. Change it and Windows Installer treats the new release as an unrelated product: the old version is never upgraded, only accumulated alongside. |
| `productId` | **A new GUID for every version**, from a real generator — `[guid]::NewGuid()` on Windows, `uuidgen` elsewhere. Never hand-written. |
| `version` | The release `X.Y.Z`, matching `<Version>` in the Desktop csproj. |

Everything else — `install`, `scope`, `compression`, `output`, `controlPanel`, `shortcuts` — carries
over unchanged.

## 5. Publish the GitHub Release

Create a release against the `X.Y.Z` tag, with notes matching that section of `RELEASENOTES.md`, and
attach **both** artifacts:

- `artifacts\EnigmaMarkdownEditor.msi`
- `artifacts/EnigmaMarkdownEditor-X.Y.Z-linux-x64.tar.gz`

## 6. Post-release verification

Download the artifacts **from the release page** — not from `artifacts/` — so the upload itself is
part of what is checked.

- [ ] **Windows, on a machine that is not the build machine**: the MSI installs, the desktop and
      start-menu shortcuts work, the application launches, and uninstall removes it cleanly.
- [ ] The installed version reads `X.Y.Z` in *Apps & features*, and on the executable itself:
      ```powershell
      (Get-Item "$env:ProgramFiles\Enigma Markdown Editor\Enigma.MarkdownEditor.Desktop.exe").VersionInfo |
        Select-Object FileVersion, ProductVersion
      ```
      `FileVersion` is `X.Y.Z.0`. `ProductVersion` is the informational version and reads
      `X.Y.Z+<commit sha>` — the SDK appends the source revision, so the `+…` suffix is correct, not
      a mismatch; what matters is the part before the `+`.
      The application's **About** dialog — the info button in the toolbar, left of the gear —
      reads the same `X.Y.Z`, and its *Build* line reads the first seven characters of the tagged
      commit. The window title still carries no version, so those three are what there is to check.
- [ ] **Linux**: the downloaded tarball unpacks, `install.sh` runs, and the application launches from
      the desktop menu. The tarball's own name carries `X.Y.Z`.
- [ ] The tag exists on `main` and the release notes match `RELEASENOTES.md`.
- [ ] A fresh install writes its per-user folder — `%APPDATA%\Enigma.MarkdownEditor` or
      `~/.config/Enigma.MarkdownEditor` — and starts as a first run, without inheriting the build
      machine's session. The main window must be the first thing on screen,
      and the application must still be running afterwards.
