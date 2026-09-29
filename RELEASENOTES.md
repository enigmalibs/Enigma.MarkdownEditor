# Enigma Markdown Editor v1.7.0 Release Notes

A minor release, and most of it happens before the application starts: installing it on Linux. The
installer is now the one Enigma.GitClient uses. It builds the editor straight from a checkout when
there is no release tarball to hand, puts an `enigma-markdown-editor` command on your `PATH`, gives
the launcher a full set of icons, and comes with an uninstaller. Inside the editor, code spans in
the preview finally read as code. Nothing in your settings, workspaces or sessions changed shape,
and none of them needs a migration.

## New Features

- **Install on Linux from a checkout, in one command.** `./packaging/linux/install.sh` builds the
  application from the repository, with the .NET runtime bundled so it runs whatever is installed on
  the machine, and installs it for you: no root, no `sudo`, nothing outside your home directory. The
  same `install.sh` sits in the release tarball, and there it installs the application that came
  with it, with no .NET SDK needed. Run it again and it upgrades in place, swapping the new copy in
  only once the build has succeeded, so a failed build leaves the installed version working.
  `--framework-dependent` builds a much smaller copy against an installed .NET 10 runtime, `--rid`
  picks another runtime identifier, and `--from DIR` installs a directory you published yourself.
- **An `enigma-markdown-editor` command.** The installer links it into `~/.local/bin`, and says so
  if that directory is not on your `PATH`.
- **A crisp icon at every size.** The installer now puts the launcher's icon down in all six
  standard sizes, 16 to 256 pixels, rather than one 256-pixel image the desktop had to shrink for
  menus and task switchers.
- **`uninstall.sh`**, beside `install.sh` in the tarball and in the repository, removes exactly what
  the installer wrote: the application, the command, the desktop entry and the icons. It never
  touches `~/.config/Enigma.MarkdownEditor`, where your settings, workspaces, session and logs live,
  or any note.

## Changes

- **The Linux install has moved.** The application now lives in
  `~/.local/share/enigma-markdown-editor` (following `XDG_DATA_HOME`) rather than
  `~/.local/opt/enigma-markdown-editor`. **You do not need to do anything about it**: installing
  1.7.0 over a 1.x install removes the old copy once the new one is in place, and the desktop entry,
  which kept its name, now points at the new one.
- **`install.sh --prefix` is gone.** Set `XDG_DATA_HOME` (and `XDG_BIN_HOME` for the command) to
  install somewhere else. That way the uninstaller can always find what the installer wrote.
- **The release tarball's layout changed** to match: `install.sh`, `uninstall.sh`, the desktop-entry
  template, an `icons/` folder and the application in `app/`. It is unpacked and installed exactly
  as before.

## Fixes

- **Code spans in the preview read as code.** A `` `backtick` `` span was drawn one shade off
  the pane behind it, with its text touching the edges of its own background, so in running prose it
  looked like prose. It now sits on a chip of its own colour: lighter than the pane in the dark
  theme, darker in the light one, and translucent, so it reads the same inside a block quote or a
  table header. A little space either side of the text is part of the chip, and a line never breaks
  between a chip and its padding. **Copying is unaffected**: the padding is the renderer's, not your
  note's, and a copy out of the preview hands back exactly the characters the note contains.

## Dependencies

- **`Markdig` moves from 1.3.2 to 1.4.0**, a minor bump of the parser behind the preview. The
  preview's test suite passes unchanged against it.
- **`Microsoft.Extensions.Hosting` moves from 10.0.11 to 10.0.12**, a patch bump in the .NET 10
  band.
- **`coverlet.collector` moves from 10.0.1 to 10.1.0.** Test-only and not redistributed.
- **Deliberately held back:** the version-coupled Avalonia set (`Avalonia`, `.Desktop`,
  `.Themes.Fluent`, `.Fonts.Inter` and the two headless test packages) stays at 12.1.2 although
  12.1.3 is out, and `Enigma.Avalonia.Desktop` stays at 1.0.0 with it (1.1.0 is out). A coupled set
  moves as a set, on its own. `xunit.v3` stays at 3.2.2 (4.0.1 is out): a major test-framework bump
  is its own piece of work, and the Avalonia headless test harness at 12.1.2 is built against 3.2.2.

## Compatibility

- **Unchanged from 1.6.0 where it matters.** Still **.NET 10** and **Avalonia 12.1.2**, still
  **Windows x64** and **Linux x64**, both shipped **self-contained**. The Windows MSI is unchanged
  apart from its version.
- **Your data is read as it was left.** `settings.json`, `workspaces.json` and a stored session gain
  nothing and lose nothing, so there is nothing to migrate, in either direction between 1.6.0 and
  1.7.0.
- **A Linux install from a checkout needs the .NET 10 SDK**; the release tarball does not.

## Version

- **1.7.0**: a SemVer MINOR release. Functionality was added (the installer), two display defects
  were fixed, and nothing you depend on changed incompatibly. The one option removed, `--prefix`,
  belonged to the packaging script rather than to the application, and has a replacement.

# Enigma Markdown Editor v1.6.0 Release Notes

A minor release about the two things you do before you write anything: making a note, and finding it
again. A new note now arrives with its own title already in it. A folder can be given a colour, and
every tab opened from inside it carries that colour, so a strip of eight tabs says which folder each
note came from rather than just what it is called. And under vim, `o` and `O` carry a list marker
onto the line they open, the way `Enter` already did in insert mode. Nothing was removed and nothing
changed shape: 1.5.0's settings, workspaces and sessions are read as they are, with no migration, and
no dependency moved.

## New Features

- **A new note starts with its own title.** *New file…* in the tree used to leave an empty file;
  it now writes `# ` and the name you gave it, followed by a blank line, so the first thing you type
  is the first paragraph rather than the heading you meant to add. The title is the file name with
  its extension taken off and nothing else done to it — `2026-09-10.stand-up.md` becomes
  `# 2026-09-10.stand-up`, because a name you chose deliberately is not one to rewrite. Only markdown
  gets a heading: every other extension the tree can create still arrives empty.
- **A folder can be given a colour, and its tabs carry it.** Right-click a folder in the tree and
  pick one of eight colours — or *None* to take it away — and the folder gets a bar in that colour
  beside its name. **Every tab opened from inside it gets the same bar**, at the tab's leading edge,
  which is the point: with notes open from three folders at once, the strip says where each one came
  from at a glance. A colour applies to everything under the folder, and a colour set on a sub-folder
  overrides its parent's, so a whole tree can be one colour with one corner of it another. Workspace
  roots can be coloured too. The colours are remembered per folder with the workspace, and the tabs
  follow along live: colouring a folder recolours the notes already open under it without reopening
  anything, switching workspaces re-reads the colours, and saving a note into another folder moves
  its tab to that folder's colour.
- **Vim's `o` and `O` continue a list.** On a list, task or quote line, opening a line carries the
  item onto it: `- ` repeats, `1.` counts on to `2.`, `- [x]` opens a fresh `- [ ]`, and `> ` keeps
  its quote depth — with your own indentation and spacing, tabs included. `O` uses the item's *own*
  marker rather than the next one, because the line it opens goes before that item and takes its
  place in the list. An empty item continues nothing, which is how a list is ended, and a line that
  is not an item opens plain exactly as before. A count works as it should — `3o` writes three
  continued items, numbering as it goes — `.` opens another one, and the marker comes back off with
  a single `u`.

## Compatibility

- **Unchanged from 1.5.0.** Still **.NET 10** and **Avalonia 12.1.2**, still **Windows x64** and
  **Linux x64**, both shipped **self-contained**. No dependency moved this release.
- **Your data is read as it was left, in both directions.** `settings.json` gains no key, and a
  stored session is untouched, so neither needs a migration. `workspaces.json` gains one **new
  optional key** — the folder colours — and its file-format version deliberately stays at `1`: a
  workspaces file written by 1.5.0 opens in 1.6.0 with no colours set, and one written by 1.6.0 still
  opens in 1.5.0, which simply ignores the colours it does not know about. So you can move back and
  forth between the two versions without losing a workspace.
- **The one behaviour that changed for existing files is that none did.** The title is written when a
  note is *created*; a note you already have is never touched.

## Version

- **1.6.0** — a SemVer MINOR release: functionality added, nothing removed, nothing deprecated, and
  no existing behaviour changed.

# Enigma Markdown Editor v1.5.0 Release Notes

A minor release, and all of it is the preview pane. Until now it could be read but not taken:
selecting inside one paragraph worked, and everything past that paragraph's edge did not. The pane
can now be selected whole and copied — as the text a reader sees, and, in the same write, as rich
text that a word processor keeps the formatting of. Nothing was removed and nothing changed shape:
per-paragraph selection and its `Ctrl+C` behave exactly as they did in 1.4.1, no dependency moved,
and 1.4.1 settings, workspaces and sessions are read as they are, with no migration.

## New Features

- **The preview pane can be selected and copied.** `Ctrl+A` in the pane selects the whole rendered
  note and `Ctrl+C` copies it. The same copy is on a button in the `PREVIEW` header and on the
  pane's right-click menu, which also offers **Select all**. All three follow one rule — the
  selection when there is one, the whole note otherwise — so a selection inside a single paragraph
  still copies just that range, exactly as it did before.
- **What lands on the clipboard is the note as it reads, not as it was written.** No `#` before a
  heading and no `**` around bold: the markup goes and what a reader can see stays. List markers and
  their nesting are kept, task items keep their `[ ]` and `[x]`, fenced code arrives verbatim
  without its fences, table rows are tab-separated so a paste into a spreadsheet lands one cell per
  column, and an image leaves its `[alt text]` where it sat. A thematic rule and the front-matter
  chip contribute nothing, neither being text on screen. The text is projected from the same
  markdown the pane draws from, so the two cannot disagree about what is in the note.
- **A copy of the whole pane carries a rich-text flavour beside the plain text, in one write.** The
  application being pasted into chooses which it wants: Word and Outlook take the HTML and keep the
  headings, bold, links and tables; a plain text editor takes the text. The HTML is deliberately
  unstyled — no fonts, no sizes, no colours — so a note copied out of the dark theme does not arrive
  as pale text on a white page. The one exception is a table column's alignment, which is something
  the author wrote rather than something the pane chose. Images are rewritten to absolute paths on
  the way out, so one that sat beside the note still resolves after the markup has left the editor.

## Compatibility

- **Unchanged from 1.4.1.** Still **.NET 10** and **Avalonia 12.1.2**, still **Windows x64** and
  **Linux x64**, both shipped **self-contained**. Nothing was installed, upgraded or dropped
  alongside the application: no dependency moved this release.
- **Your data is read as it was left.** No key was added, renamed or retyped, so a `settings.json`
  written by 1.4.1 loads unchanged and needs **no migration**. `workspaces.json` and `session.json`
  are unchanged too, and nothing above is a setting — the copy is a gesture, not an option.

## Version

- **1.5.0** — a SemVer MINOR release: functionality added, nothing removed, nothing deprecated, and
  no existing behaviour changed.

# Enigma Markdown Editor v1.4.1 Release Notes

A patch release, and there is one thing in it. Saving while vim was part-way through an insert
session wrote the file and then went on claiming it had not: the tab kept its unsaved dot, and
closing that tab asked about changes that were already on disk. Nothing else moved — no feature, no
setting and no dependency — and 1.4.0 settings, workspaces and sessions are read exactly as they
were left.

## Fixes

- **The unsaved dot clears on every save, including one made in insert mode.** `Ctrl+S`, `:w` and
  autosave all wrote the file correctly, but a save taken while vim held an insert session open left
  the tab's dot showing — so closing that tab asked whether to keep changes that were already on
  disk, and even a plain normal-mode save after such a session could leave the dot behind. Only the
  flag was ever wrong; what landed on disk was always right. The dot now goes as soon as the save
  completes and stays gone across `Esc`, and closing the tab afterwards asks nothing. The same fix
  closed a second hole on its way past: an edit that lands *during* the disk write no longer counts
  as saved, so a keystroke typed into the moment a file is being written keeps the tab dirty and
  autosave armed instead of being quietly forgotten. Undo is unaffected — `u` after an insert
  session still takes the whole session back in one step, saved mid-way or not.

## Compatibility

- **Unchanged from 1.4.0.** Still **.NET 10** and **Avalonia 12.1.2**, still **Windows x64** and
  **Linux x64**, both shipped **self-contained**. Nothing was installed, upgraded or dropped
  alongside the application: no dependency moved this release.
- **Your data is read as it was left.** No key was added, renamed or retyped, so a `settings.json`
  written by 1.4.0 loads unchanged and needs **no migration**. `workspaces.json` and `session.json`
  are unchanged too, and the fix above is a corrected flag rather than a new setting.

## Version

- **1.4.1** — a SemVer PATCH release: one defect fix, nothing added, nothing removed and nothing
  changed in shape.

# Enigma Markdown Editor v1.4.0 Release Notes

A minor release, and almost all of it is vim. Five rounds of engine work land together: the rest of
the motions with the editing commands that go with them, the visual-mode command set, the registers
with `.` repeat and macros, regex search with `:s` and `:set`, and the screen motions with the
scrolls beside them. The README's key reference is still advertised as exhaustive, and its
**deliberately not implemented** list is shorter than it has been in any release so far — four of the
five rounds took something off it. Settings, meanwhile, got tidier to look at. Nothing was removed
and nothing changed shape: 1.3.0 settings, workspaces and sessions are read as they are, with no
migration.

## New Features

- **The rest of the motions, and the editing commands that go with them.** `W` `B` `E` walk WORDs —
  runs of anything but blanks, so `foo.bar` is one — with `iW` and `aW` to match. `^` `+` `-` `_` and
  `N|` reach a line's first non-blank and its columns, `Enter` being `+`; `Space` and `Backspace` are
  an `l` and an `h` that cross a line break, and `Home` and `End` finally mean `0` and `$` instead of
  being swallowed. `%` jumps to the matching bracket, counting nesting and crossing lines, and `N%`
  goes N percent of the way through the file; `}` `{` walk paragraphs and `)` `(` sentences, both by
  vim's own definitions rather than an approximation of them. `J` and `gJ` join lines, with vim's
  rules about the space at the seam; `s` `S` and `X` substitute a character, substitute a line, and
  delete backwards. The `>` and `<` operators shift the lines a range touches — `>j`, `<}`, `>iw`,
  `>>` — always linewise, and always at the width and in the tabs-or-spaces the editor's own
  indentation settings say, so `>>` indents exactly as the Tab key does. `gu` `gU` and `g~` lower,
  upper and toggle the case over any motion or object, doubled forms included. `ZZ` and `ZQ`
  save-and-close and close-without-saving, and `:q!` `:x` `:wq!` are the same three from the ex line.
  The README's *Not implemented* list lost the WORD family.
- **The visual-mode commands — everything that acts on a selection without being `d` `y` `c`.** `x`
  is `d` and `s` is `c`; `X` `D` `Y` `C` `S` are those three over the selected *lines*, whatever the
  mode you selected in. `p` and `P` put the register over the selection — characters inline, lines on
  lines of their own — and `p` leaves what it replaced behind, so `yiw` then `viwp` on another word
  swaps the two. `r<char>` overwrites every selected character, `~` `u` `U` toggle, lower and upper
  the case, and `J` `gJ` join the selected lines. `o` swaps the anchor and the caret so the motions
  move the other end, and `gv` reselects the last selection in the mode it was made in — after an
  operator it covers exactly what the operator acted on, even if a shift has since moved every
  character of it. Each of these was refused by name in 1.3.0; the *Not implemented* list no longer
  mentions them.
- **The registers, `.` repeat and macros — the three things that make vim remember what it did.** Put
  `"` and a letter in front of a command to use a named register: `"ayy` fills `a`, `"ap` puts it, a
  capital (`"A`) appends. `"0` always holds the last yank whatever you have deleted since, and `"_`
  is the black hole that keeps nothing, so `"_dd` deletes without disturbing what you were carrying.
  `.` repeats the last change — an operator over a motion or an object, a single-key edit, a put, a
  join, or an insert with the text that was actually typed in it — as one undo step, and `3.` repeats
  it with a new count. A macro is the same idea over several keys: `qa` records into register `a`
  (the status bar reads `recording @a` while it does), `q` stops, `@a` plays it back, `3@a` three
  times and `@@` whichever ran last. A replay stops the moment a find or a search inside it fails,
  and because a macro register is just a register, a recorded macro can be put into the buffer with
  `"ap`, edited, and yanked back. All three came off the *Not implemented* list.
- **Search is a regular expression, and it is a motion.** `/` and `?` take a .NET regular expression
  — `\b` for a word boundary, `(a|b)` for a group, `$1` in a replacement — matched with a one-second
  ceiling, and a pattern that will not compile is named on the status bar and moves nothing. `n` and
  `N` step through the matches with vim's own `search hit BOTTOM` message, `*` and `#` search for the
  word under the caret as a whole word, and `\c` or `\C` anywhere in a pattern settles its case for
  that search alone. Being a motion is the part that matters: `d/foo` deletes up to the match,
  `c?bar` changes back to it, `dn` takes the next one, and in a visual mode `/` `?` `n` `N` `*` `#`
  extend the selection — none of which needed a special case. `:s` came with it, in vim's shape with
  .NET's replacement: any punctuation for the `/`, a `[range]` of `%` `3,7` `.,$` `'<,'>` `.+2`, the
  `g` `i` `I` flags, `\r` in the replacement to break the line, the whole command one undo step, and
  the status bar reading `3 substitutions on 2 lines`. `:N` and `:$` go to a line, and `:set` reads
  `ignorecase` `smartcase` `wrapscan` `shiftwidth` and `expandtab` five ways each — `:set ic`,
  `:set noic`, `:set ic!`, `:set sw=2`, `:set sw?`. Regex search and `:s` were the two entries the
  *Not implemented* list used to open with.
- **The engine can see the screen.** `H` `M` and `L` go to the first non-blank of the top, middle and
  bottom line on screen, and they are motions like any other — `dL` deletes to the bottom of the
  screen and `vH` extends a selection to the top. `Ctrl+D` and `Ctrl+U` scroll half a screen and take
  the caret with them, keeping its column exactly as `j` and `k` do; `Ctrl+F` and `Ctrl+B` — and
  `PageDown` and `PageUp` — scroll a page and land the caret on the new top or bottom line; `Ctrl+E`
  and `Ctrl+Y` move the view a line at a time and leave the caret alone unless it would go off
  screen. `zt` `zz` `zb` put the caret's line at the top, the middle or the bottom without moving the
  caret, and `z` then `Enter`, `z.` and `z-` do the same three and take the caret to the line's first
  non-blank; with a count, all six place the line the count names. None of them is a change, so `.`
  never repeats a scroll, and every one of them works while selecting.

## Changes

- **The settings dialog is tidier.** Three things about how it looks, none about what it does. The
  heading icon is gone — the title already carries it, and the icon's own column was a wide empty
  gutter down the left of the dialog. The padding is applied once rather than twice, which also
  brings the view into line with the About, first-run and workspace dialogs, none of which add an
  outer margin of their own. And the groups now sit on a darker well — lighter, in the light theme —
  with the same corner radius and the same subtle edge as the cards on it, so they read as raised
  rather than sunken. The practical part is the first one: with the icon's column gone the dialog
  asks for less width than the host allows it, where before it wanted 872px against a 860px cap and
  had its content squeezed to fit. Nothing a setting does changed — no group or row was added,
  removed or reordered, and every edit still applies live as you make it.

## Dependencies

- **The version-coupled `Avalonia.*` set moves from 12.1.1 to 12.1.2** — a patch bump, taken as a set
  as it always is: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`
  and the two headless test packages.
- **`coverlet.collector` moves from 6.0.4 to 10.0.1.** Test-only and not redistributed, so nothing
  about it reaches a shipped build.
- **`xunit.v3` is deliberately held at 3.2.2.** 4.0.0 is out; a major test-framework bump is its own
  piece of work rather than a passenger on a release. AvaloniaEdit stays at 12.0.0 and Markdig at
  1.3.2, neither having anything newer.

## Compatibility

- **Unchanged from 1.3.0, except the Avalonia patch.** Still **.NET 10**, now **Avalonia 12.1.2**,
  still **Windows x64** and **Linux x64**, both shipped **self-contained**. The target-framework set
  did not move, so there is nothing to install or upgrade alongside the application.
- **Your data is read as it was left.** No key was added, renamed or retyped this cycle, so a
  `settings.json` written by 1.3.0 loads unchanged and needs **no migration**. `workspaces.json` and
  `session.json` are unchanged too, and everything new above is a vim keystroke rather than a setting.

## Version

- **1.4.0** — a SemVer MINOR release: five rounds of vim additions and a tidier settings dialog.
  Nothing was removed and nothing changed in a breaking way.

# Enigma Markdown Editor v1.3.0 Release Notes

A minor release, and mostly about the vim engine. Two of the things the README's key reference has
listed as **deliberately not implemented** since 1.0.0 are now implemented — the character find
motions and text objects — so this release shortens that list rather than adding to an open-ended
one. Settings, meanwhile, stops being a separate window. Nothing was removed and nothing changed
shape: 1.2.0 settings, workspaces and sessions are read as they are, with no migration.

## New Features

- **The vim character find motions — `f` `F` `t` `T`, with `;` and `,` to repeat.** `f<c>` jumps
  forward onto the next occurrence of the character you type next and `F<c>` back onto the previous
  one; `t<c>` and `T<c>` stop just short of it. `;` repeats the last of the four the same way round
  and `,` the other way — and a `;` after a `,` still goes the original way. They take a count on
  either side and work behind `d`, `y` and `c` and in both visual modes, so `df,` takes the comma
  with it, `dt,` stops on the character before it, and `2fa` finds the second `a`. The family never
  leaves the caret's own line in either direction — that is what makes it a jump rather than a
  search, and `/` still covers everything else. A find that matches nothing does nothing at all,
  silently, and a count that overshoots finds nothing rather than settling for the last match it
  passed.
- **Text objects — `iw` `aw`, the four bracket pairs and the three quote characters.** A text object
  says *which text* rather than where to go: `diw` deletes the word the caret is on, `ci(` changes
  everything inside the enclosing parentheses, `ya"` yanks a quoted string with its quotes. `i` takes
  what is inside, `a` takes the delimiters too — and, for a word, the blanks beside it. The four
  bracket pairs are `(` `[` `{` `<`, each spelled by either half of the pair (`i(` and `i)` are the
  same thing, as are `ib` and `iB`), and the three quote characters are `"` `'` and `` ` ``. They
  work behind `d`, `y` and `c` and in visual mode, where `viw` selects the word the caret is on and
  `vi{` the block round it, both ends moving to the object rather than extending from where the `v`
  was. How far each family reaches is the whole of what separates them: a word is a run of one of
  vim's three character classes and stays on its line, a bracket pair counts nesting and is not bound
  to a line at all — so `ci{` over a fenced block is one keystroke — and a quote pair stays on the
  line and pairs off from its start, because nothing in the text says which of two identical
  characters opens and which closes. A count multiplies a word object (`d3iw`, `d2aw`) and is dropped
  by the others.
- **Settings is a dialog rather than a window.** It was the last real window in an application whose
  every other dialog — About, Manage workspaces, First run — is raised on the main window itself. It
  now opens the same way, closing on its own *Close* button, `Escape` or a click outside it. Nothing
  a setting does changed: no group or row was added, removed or reordered, and every edit still
  applies live as you make it.

## Changes

- **The editor's default font size is 16 rather than 18.** This affects a first run, or a
  `settings.json` that never carried the key — a stored value wins, so an existing profile that has
  ever had the editor's size set keeps what it is set to, and nothing is migrated.
- **The preview settings rows are labelled like the editor's.** *Text font*, *Text size* and *Code
  font* become *Font family*, *Font size* and *Code font family*, so the two font groups in Settings
  name the same things the same way. The rows, their order and what each one edits are unchanged.

## Fixes

- **The tab strip has a scrollbar again, and it no longer eats a row of height.** With more tabs than
  room the strip drew nothing to say so and could only be scrolled by the wheel, because the previous
  fix had hidden a bar that floated across the very tabs it was there to reach. There is now a thin
  6px bar in a band of its own under the tabs, present only while the tabs overflow and the same
  thickness whatever the pointer is doing. The strip keeps its height either way, so opening the last
  tab that fits no longer makes the tabs themselves change size. The wheel still scrolls it sideways.
- **Preview checkboxes line up with their text, and are sized with it.** A task item's box sat
  visibly above the text beside it, and the gap grew as the preview's font size or line height was
  raised — the box was pinned to the top of its row while the prose beside it centres in its line.
  The box is now offset onto the text's own centre line, and its size follows the reading size
  instead of being a fixed 20px, so a task and a bullet in the same list are finally drawn to one
  scale. At the shipped default the box comes out at exactly the size it already was, so nobody's
  preview changes.

## Dependencies

- **No dependency changed in this release.** The version-coupled `Avalonia.*` set is deliberately
  held at **12.1.1** and moves as a set or not at all; AvaloniaEdit stays at 12.0.0, Markdig at 1.3.2
  and xunit.v3 at 3.2.2.

## Compatibility

- **Unchanged from 1.2.0.** Still **.NET 10**, still **Avalonia 12.1.1**, still **Windows x64** and
  **Linux x64**, both shipped **self-contained**. The target-framework set did not move, so there is
  nothing to install or upgrade alongside the application.
- **Your data is read as it was left.** No key was added, renamed or retyped this cycle, so a
  `settings.json` written by 1.2.0 loads unchanged and needs **no migration**. `workspaces.json` and
  `session.json` are unchanged. The one thing an existing user may notice is the editor's default
  size above — and only if the key was never set.

## Version

- **1.3.0** — a SemVer MINOR release: two additions to the vim engine, Settings moved into a dialog,
  a lowered default and two fixes. Nothing was removed and nothing changed in a breaking way.

# Enigma Markdown Editor v1.2.0 Release Notes

A small release, and mostly repairs: the five defects reported against 1.1.0, and the new setting the
last of them needed. **One of them concerns unsaved work** — a tab edited in vim insert mode showed no
unsaved marker and closed without asking, so edits could be lost. If you use vim mode, that is the
entry to read. Nothing was removed and nothing changed shape: 1.1.0 settings, workspaces and sessions
are read as they are.

## New Features

- **Code font size, in the preview settings** — code spans and fenced blocks are now set at a size
  you choose, in points, instead of one point under the reading size. A fixed-width face reads larger
  than prose at the same number of points, so how much smaller code has to be set depends on the two
  families you picked, which is not a subtraction; the new row sits directly under *Code font* in the
  preview group of Settings and defaults to 14. It reaches an open preview without a restart, and it
  applies to inline `` `code` `` as well as to fenced blocks — inline spans previously ignored the
  code size altogether and came out at the full prose size, the largest code on the page.

## Fixes

- **Closing a tab no longer leaves a dead editor behind.** Closing the *active* tab while other tabs
  were open left the middle pane drawing an editor over no document — a text area that raised an
  error the moment the pointer reached it. The neighbouring tab is activated instead, and the pane
  draws nothing at all when the last tab is closed.
- **Edits made in vim insert mode are recognised straight away — this is the one that could cost you
  work.** The unsaved marker was driven off the undo stack alone, which AvaloniaEdit only recomputes
  once an undo group closes; insert mode holds one group open for the whole session, so the dot never
  appeared while you were typing and closing the tab discarded the work without asking. Typing now
  raises the marker itself: the dot appears as you type, closing that tab asks first, and autosave
  sees the change. Undoing back to the saved state still clears the dot, and one `u` still undoes a
  whole insert session.
- **The application exits when its window is closed.** The shutdown sequence — flush the log, stop
  the host, dispose it — ran on the UI thread and blocked waiting on work that needed that same
  thread to continue, so the window went away and the process stayed. It now runs off the UI thread
  under a bounded wait, and a sequence that overruns is logged rather than allowed to hold the
  process open.
- **The editor font family chosen in Settings is applied.** Picking a family did nothing at all,
  restart or no restart, while the size beside it and both preview families worked. The editor now
  takes the family you picked, live, and *Automatic* still resolves to the best fixed-width face the
  machine has.
- **Code in the preview is no longer bold.** The preview honoured the bold weight the Dark+ and
  Light+ TextMate themes mark whole scopes with, so fenced keywords came out heavier than the same
  text in the editor. The colours are still the editor's; the weight no longer is.

## Dependencies

- **No dependency changed in this release.** The version-coupled `Avalonia.*` set is deliberately
  held at **12.1.1** and moves as a set or not at all; AvaloniaEdit stays at 12.0.0 and Markdig at
  1.3.2.

## Compatibility

- **Unchanged from 1.1.0.** Still **.NET 10**, still **Avalonia 12.1.1**, still **Windows x64** and
  **Linux x64**, both shipped **self-contained**. The target-framework set did not move, so there is
  nothing to install or upgrade alongside the application.
- **Your data is read as it was left.** `settings.json` gains one optional key,
  `PreviewCodeFontSize`; a file written by 1.1.0 does not have it and loads on the default of **14**,
  so upgrading in place needs no migration. `workspaces.json` and `session.json` are unchanged.

## Version

- **1.2.0** — a SemVer MINOR release: five fixes and one additive setting, with a new row in the
  settings window and a new key in `settings.json`. Nothing was removed and nothing changed in a
  breaking way.

# Enigma Markdown Editor v1.1.0 Release Notes

A minor release, entirely additive. It is mostly about the parts of the application you meet before
you have typed anything — a splash screen while it starts, three questions on the very first run, and
an About dialog that can finally say which version you are looking at — plus type you choose
yourself, on both sides of the split. Nothing was removed and nothing changed shape: 1.0.0 settings,
workspaces and sessions are read as they are.

Four of the seven items below — the tab strip, the editor's and the preview's typography, and the
tickable task boxes — landed between 1.0.0 and now and never had release notes of their own. They are
documented here rather than left unannounced.

## New Features

- **A splash screen while it starts** — a small undecorated window, centred, carrying the icon, the
  product name and the version, shown about 0.8 s into the process and replaced by the main window
  once composition has finished, never sooner than a second after it appeared. It stays out of the
  task switcher, so nothing is left behind on the desktop or in Alt-Tab.
- **An About dialog** — the info button in the toolbar, immediately left of the settings gear, opens
  it: the application icon, the product name, the version, the commit the build was cut from, the
  copyright, and the attribution list with every dependency's licence. `Escape` closes it. The
  version is read from the assembly the build stamped, so what the dialog shows is what you are
  running rather than a constant somebody remembered to update.
- **First-run setup** — the very first start now asks, once, about the three things worth deciding
  before the editor is used: whether vim emulation is on, which theme to use, and the folder your
  notes live in. Every way out of the dialog is final — *Get started*, *Skip*, `Escape` and a click
  outside it all write the settings file, which is what stops the questions coming back. Everything
  else stays in Settings, where it was.
- **A tab strip that behaves like one** — a modified tab carries a dot that becomes its close button
  under the pointer, the tab you are editing is the one whose title stands out, and the wheel scrolls
  the strip sideways when there are more tabs than room.
- **The editor's font is yours to pick** — any fixed-width family actually installed, each listed in
  itself, and the size in points, with an optional line-number margin. Left on *Automatic* it takes
  the best code font on the machine, so the same note looks right on a Windows install with Cascadia
  Mono and a Linux one with DejaVu Sans Mono without the setting naming either.
- **So is the preview's** — a reading font for prose, a separate one for code spans and fenced
  blocks, the body size and the line height. Headings scale *with* the body size rather than against
  it, so raising the size raises the whole hierarchy proportionally instead of flattening it.
- **Task lists are tickable from the preview** — clicking a box, or pressing `Space` on a focused
  one, rewrites the one character between its brackets in the note you are editing: the tab goes
  dirty, autosave picks it up, and `Ctrl+Z` in the editor takes it back in a single step. It works on
  a task nested in a sub-list and on one inside a block quote.

## Dependencies

- **No dependency changed in this release.** The version-coupled `Avalonia.*` set is deliberately
  held at **12.1.1** and moves as a set or not at all; AvaloniaEdit stays at 12.0.0 and Markdig at
  1.3.2.

## Compatibility

- **Unchanged from 1.0.0.** Still **.NET 10**, still **Avalonia 12.1.1**, still **Windows x64** and
  **Linux x64**, both shipped **self-contained**. The target-framework set did not move, so there is
  nothing to install or upgrade alongside the application.
- **Your data is read as it was left.** `settings.json`, `workspaces.json` and `session.json` keep
  their 1.0.0 shape — no schema changed and nothing new is persisted. Upgrading in place needs no
  migration.
- **One thing to expect on the first 1.1.0 start:** the first-run questions are triggered by the
  *absence* of `settings.json`, so an existing install is not asked them — but an installation that
  never wrote one (nothing was ever changed from the defaults) will be asked, once.

## Version

- **1.1.0** — a SemVer MINOR release: everything above is additive, and nothing was removed or
  changed in a breaking way.

# Enigma Markdown Editor v1.0.0 Release Notes

The first release of **Enigma Markdown Editor** — a cross-platform markdown editor for notes kept as
files. It opens a folder, not a database: every note stays a `.md` file on disk that anything else
can read, and what the application adds on top is the editing — modal vim keys on by default, a
preview rendered from the same Markdig tree the file parses to, and a workspace that remembers
itself.

## Feature overview

- **Workspaces and the file tree** — a workspace is a set of folders, listed, filtered and watched
  together, picking up changes made outside the editor. Notes and folders are created, renamed,
  moved and deleted from the tree, and a delete goes to the system trash. Markdown only by default;
  one setting shows every file instead.
- **Editor** — tabs with the usual unsaved-changes question, and a save that preserves the file's
  encoding, byte-order mark and line endings exactly as they were found. Optional autosave on a
  delay you choose, and reload when a file changes underneath you. Markdown syntax highlighting
  through TextMate grammars, fenced code blocks included, in both themes. Writing assists: `Enter`
  carries a list marker down and renumbers, `Tab` / `Shift+Tab` move an item between levels,
  `Ctrl+B` / `Ctrl+I` / `Ctrl+K` wrap the selection or the word under the caret, and an image pasted
  from the clipboard is saved beside the note and linked.
- **Vim emulation** — on by default and live-switchable, with no restart. Normal, insert, visual and
  visual-line modes; the `h j k l 0 $ w b e gg G` motions; `d` / `y` / `c` over any of them plus
  their doubled and capital forms; `x`, `p` / `P`, `r`, `~`, undo and redo; counts on either side of
  an operator; `/` search with `n` / `N`; and the `:w` `:q` `:wq` ex commands. The README's *Vim key
  reference* is the exhaustive contract, and names what is deliberately left out of 1.0.
- **Preview** — rendered natively from the Markdig syntax tree in the application's own palette, so
  it follows the window from dark to light. Headings, emphasis, strikethrough, code spans, links,
  nested bulleted / numbered / task lists, block quotes, thematic breaks, images resolved beside the
  note, GFM tables as a real grid honouring the alignment row, and fenced code coloured by the same
  grammars the editor uses. YAML front matter becomes one chip naming its keys rather than content.
  Three view modes — editor, split, preview — with the preview following the editor's scroll in
  split.
- **Search** — every markdown file in every workspace folder, as a plain substring, case-insensitive
  unless you say otherwise. Name matches first, then the rest grouped by file with the matching line
  shown; clicking a hit opens the note with the match selected. Bounded at 500 hits and 10 MB per
  file, and it says so when it stops.
- **Session restore** — the open notes in tab order with the active one, the window back on a screen
  that still exists, the pane divisions, and the sidebar tab you had chosen.
- **Failure is local, everywhere** — a block the renderer cannot draw becomes one bordered sentence
  in its own place, a settings or session file that cannot be read is moved aside as
  `<name>.invalid.json` and the defaults are used, and a note that has disappeared since the last
  session is passed over quietly. None of these take the application down.

## Compatibility

- **Windows x64** and **Linux x64**, both shipped **self-contained**: the .NET runtime is inside the
  artifact, so there is no SDK or runtime to install first.
- Built on **.NET 10**, **Avalonia 12.1.1**, **AvaloniaEdit 12.0.0** and **Markdig 1.3.2**.
- Windows is installed from an MSI (`%ProgramFiles%\Enigma Markdown Editor`, desktop and start-menu
  shortcuts). Linux is a tar.gz with an `install.sh` that installs under `$HOME` — no root, no
  package manager.
- Per-user data lives in `%APPDATA%\Enigma.MarkdownEditor` on Windows and
  `~/.config/Enigma.MarkdownEditor` on Linux. Your notes are never in there.

## Version

- Initial release: **1.0.0**.
