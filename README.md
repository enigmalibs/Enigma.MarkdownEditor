# Enigma Markdown Editor

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

A cross-platform markdown editor for notes you keep as files. A workspace file tree on the left, a
tabbed [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) editor with vim emulation in the
middle, and a live [Markdig](https://github.com/xoofx/markdig) preview on the right. Windows and
Linux, self-contained — nothing to install alongside it.

> **What's new in 1.7.0** — Linux gets the installer Enigma.GitClient uses: `install.sh` builds the
> editor straight from a checkout or installs the one in the release tarball, adds an
> `enigma-markdown-editor` command and a full set of icons, and ships with an `uninstall.sh`. In the
> preview, code spans now sit on a chip of their own. See [RELEASENOTES.md](RELEASENOTES.md).

## Why

Notes are markdown files in a folder you own. Enigma Markdown Editor opens that folder, not a
database: every note stays a `.md` file on disk, readable and editable by anything else you care to
point at it, with no account, no sync service and no proprietary format in between.

What it adds on top is the editing. Modal vim keys, on by default, because notes are mostly edited
rather than written. A live preview that renders from the same Markdig tree the file parses to. And
a workspace that remembers itself, so the notes you had open come back where you left them.

## Screenshots

*Captures pending — the paths below are already final, so dropping the two files in needs no change
here.*

<!-- Uncomment once docs/images/screenshot-dark.png and docs/images/screenshot-light.png exist:
| Dark | Light |
|------|-------|
| ![Enigma Markdown Editor, dark theme](docs/images/screenshot-dark.png) | ![Enigma Markdown Editor, light theme](docs/images/screenshot-light.png) |
-->

## Features

**Workspaces and the file tree**

- A workspace is a set of folders. Add as many as you like; the tree lists, filters and watches them
  all, and picks up changes made outside the editor.
- Create, rename, move and delete notes and folders from the tree. A new note arrives with its own
  `#` title already in it, taken from the name you gave it. Deletes go to the system trash, not to
  nowhere.
- Markdown only by default; a setting shows every file instead.
- Give a folder a colour from its right-click menu — eight to pick from — and it carries a bar in that
  colour in the tree. **Every tab opened from inside it carries the same bar**, so a strip of eight
  tabs reads as which folder each note came from rather than as eight file names. A colour on a
  sub-folder overrides its parent's, and the colours are remembered with the workspace.

**Editor**

- Tabs, with the usual unsaved-changes question on close. A modified tab carries a dot that
  becomes its close button under the pointer, and the tab you are editing is the one whose title
  stands out. With more tabs than room the strip takes a thin scrollbar in a band of its own under
  them — the wheel scrolls it sideways as well, and neither ever covers a title.
- A file's encoding, byte-order mark and line endings are preserved exactly as they were found — a
  save never rewrites what it did not have to.
- Optional autosave, on a delay you choose, and reload when a file changes underneath you.
- Markdown syntax highlighting via TextMate grammars, including fenced code blocks, in both themes.
- Pick the editor's font from the fixed-width families actually installed, each listed in itself, and
  its size in points. Left on *Automatic* it takes the best code font on the machine, so a note looks
  the same on a Windows install with Cascadia Mono and a Linux one with DejaVu Sans Mono without the
  setting naming either.
- Writing assists: `Enter` carries a list marker down and renumbers as it goes — and so do vim's `o`
  and `O`, so a list keeps going without leaving normal mode — `Tab` / `Shift+Tab`
  move an item between levels, `Ctrl+B` / `Ctrl+I` / `Ctrl+K` wrap the selection or the word under
  the caret, and an image pasted from the clipboard is saved beside the note and linked.

**Vim emulation** — on by default, and live-switchable in settings. See the key reference below.

**Preview**

- Rendered natively from the Markdig syntax tree in the application's own palette, so it follows the
  window from dark to light instead of looking like a web page dropped into it.
- Headings, emphasis, strikethrough, code spans, links, bulleted / numbered / task lists with
  nesting, block quotes, thematic breaks, and images resolved beside the note.
- GFM tables as a real grid, honouring the alignment row; fenced code coloured by the same TextMate
  grammars the editor uses. Both keep their own horizontal scrollbar rather than being clipped.
- YAML front matter never renders as content — it becomes one chip naming its top-level keys.
- Links are clickable: the browser for an address, a tab for another note, the OS for anything else.
- Task lists are tickable: clicking a box in the preview — or pressing `Space` on a focused one —
  rewrites the one character between its brackets in the note you are editing, so the tab goes dirty,
  autosave picks it up, and `Ctrl+Z` in the editor takes it back in a single step.
- The pane is copyable: `Ctrl+A` selects the whole rendered note and `Ctrl+C` copies it as the pane
  reads it, markup-free — headings bare, list markers and task boxes kept, fenced code verbatim
  without its fences, table rows tab-separated so a paste into a spreadsheet lands one cell per
  column, and `[alt text]` where each image sat. A copy of the whole pane puts rich text beside the
  plain text in the same write, so Word and Outlook keep the headings, bold, links and tables while
  a plain editor still takes the text — and the rich text is deliberately unstyled, no fonts, sizes
  or colours, so a note copied out of the dark theme does not paste as pale text on a white page. A
  selection inside one paragraph copies just that range. The same copy is on the PREVIEW header's
  button and on the pane's context menu, which also offers *Select all*.
- A block the renderer cannot draw becomes one bordered sentence in its own place and is logged. It
  never takes the pane, or the application, down with it.
- Set in type of your own: a reading font chosen from every family installed, a size the headings
  scale with rather than against, a separate font *and size* for code spans and fenced blocks, and
  the line height. Either font left on *Automatic* takes the best the machine has — the reading font
  for prose, the best fixed-width one for code. Code is sized on its own rather than under the
  reading size, because a fixed-width face reads larger than prose at the same number of points.
  A task list's checkboxes follow the reading size too, each one drawn to the scale of the text
  beside it and centred on its first line.
- Three view modes — editor, split, preview — cycled with `Ctrl+Shift+V`. In split, scrolling the
  editor carries the preview to the block the top line belongs to.

**Search** — every markdown file in every folder of the workspace, as a plain substring,
case-insensitive unless you say otherwise. Notes whose *name* matches come first; the rest are
grouped by file with the matching line shown and the match highlighted. Clicking a hit opens the note
with the match selected and centred. A search stops at 500 hits, never reads a file over 10 MB, and
says so when either happens.

**Session restore** — the notes that were open, in tab order, with the one you were reading on
screen; the window back on a screen that still exists, at the size and state you left it; the panes
divided the way you dragged them; the sidebar on the tab you had chosen. Notes deleted or moved since
are passed over quietly, and a session file that cannot be read costs you a session and nothing else.

**Start-up**

- A splash screen while it loads — the icon, the name and the version, centred, replaced by the main
  window as soon as that window is ready and never on screen for less than a second. It stays out of
  the task switcher, so it leaves nothing behind.
- The very first start asks three questions, once: vim keys on or off, the theme, and the folder your
  notes live in. *Skip* is an answer too, and either way it never asks again — everything it asks is
  in settings afterwards.
- An About dialog, behind the info button in the toolbar — the icon, the version, the commit the
  build was cut from, the copyright and the licence of every dependency. `Escape` closes it.

## Vim key reference

This is the whole of it — the vim contract, exhaustive. Vim mode is **on by default**; turn it off in
settings and the editor becomes an ordinary one, live, without a restart. The current mode shows in
the status bar as `NORMAL`, `INSERT`, `VISUAL` or `V-LINE`.

### Modes

| Keys | |
|---|---|
| `Esc` | Back to normal mode; also cancels a half-typed sequence |
| `i` `a` | Insert before / after the caret — behind an operator or in visual mode they open a text object instead |
| `I` `A` | Insert at the first non-blank / at the end of the line |
| `o` `O` | Open a line below / above and insert — on a list, task or quote line the item is carried onto it, as `Enter` does in insert mode |
| `v` `V` | Visual mode, by character / by whole line |
| `o` | While selecting: swap the anchor and the caret, so the motions move the other end |
| `gv` | Reselect the last selection, in the mode it was made in; while selecting, swap it with the current one |

In a visual mode the operators act on the selection at once — `d` `y` `c`, `>` `<`, `gu` `gU` `g~` —
and so do the keys that spell them: `x` is `d`, `s` is `c`, and `X` `D` `Y` `C` `S` are the same
three over the selected *lines*, whatever the mode. `p` and `P` put the register over the selection:
characters go inline, lines go on lines of their own (splitting the line where the selection was),
and anything put over whole lines becomes whole lines. `p` leaves what it replaced in the register,
so `vp` on one word and then on another swaps them; `P` leaves the register as it was. `r<char>`
overwrites every selected character, `~` `u` `U` toggle, lower and upper the case, and `J` `gJ` join
the selected lines, two at the least. Every one of them leaves visual mode with the caret at the start
of what was selected — a join on its seam, a put where a put lands. Because `u` is the lowercase
operator while selecting, as it is in vim, undo and redo are not available until the selection is
gone.

### Motions

| Keys | |
|---|---|
| `h` `j` `k` `l` | Left, down, up, right (arrow keys too) |
| `0` `$` | Start / end of the line |
| `w` `b` `e` | Forward a word, back a word, to the end of a word |
| `W` `B` `E` | The same over WORDs — runs of anything but blanks, so `foo.bar` is one |
| `^` | First non-blank of the line |
| `+` `-` `_` | First non-blank of the line below / above / this one, a count of lines away. `Enter` is `+` |
| `N|` | Column N of the line |
| `f<char>` `F<char>` | Forward / back onto the next occurrence of the character |
| `t<char>` `T<char>` | Forward / back to just short of it |
| `;` `,` | Repeat the last of those four, the same / the other way round |
| `%` | The matching bracket — of the one under the caret, or of the first one along the line. `N%` is N percent of the way through the file |
| `}` `{` | Next / previous blank line |
| `)` `(` | Next / previous sentence |
| `gg` `G` | First / last line, at its first non-blank. `2gg` and `3G` name the line instead |
| `Home` `End` | `0` and `$` |
| `Space` `Backspace` | `l` and `h` that cross a line break |

`j` and `k` remember the column they started in and find it again past a short line. `w`, `b` and `e`
use vim's three character classes, so `foo.bar` is three words and `foo_bar` is one, and a blank line
is a stop — which is what walks a markdown file paragraph by paragraph. `W`, `B` and `E` see two
classes instead, blank and not, and stop on a blank line all the same.

A paragraph boundary is a line with nothing on it at all — a line of blanks is not one, which keeps a
markdown hard break inside its paragraph. A sentence is vim's own: it ends at a `.` `!` or `?`
followed by the end of the line or a blank, with any run of `)` `]` `"` `'` allowed in between, and a
paragraph boundary is a sentence boundary too. `%` knows the three bracket pairs, counts nesting and
crosses lines; on a line with no bracket it does nothing at all. Behind an operator it is inclusive
of both brackets, so `d%` takes the pair with everything between them, and `+` `-` `_` are linewise
as the lines they name.

Behind an operator the paragraph motions follow vim's exclusive-linewise rule: `d}` from the first
column of a paragraph deletes whole lines and stops short of the blank line, and from mid-line it
deletes to the end of the paragraph and leaves the blank line alone. `d<Space>` on the last character
of a line deletes that character and not the line break, exactly as `x` does.

The find family never leaves the line the caret is on, in either direction — that is what makes it a
jump rather than a search, and `/` covers everything else. Behind an operator the forward pair is
inclusive, so `df,` takes the comma with it and `dt,` stops on the character before it. `;` after a
`t` steps over the match it is already sitting next to, so holding it makes progress; `,` reverses a
find without becoming one, and a `;` after it still goes the original way. A find that matches
nothing does nothing at all, without a message.

The screen has motions and scrolls of its own:

| Keys | |
|---|---|
| `H` `M` `L` | First non-blank of the top / middle / bottom line on screen; `3H` is the third line from the top, `3L` the third from the bottom |
| `Ctrl+D` `Ctrl+U` | Scroll half a screen down / up and move the caret with it, keeping the column; a count scrolls that many lines instead |
| `Ctrl+F` `Ctrl+B` | Scroll a page down / up — the screen less two lines — with the caret on the new top / bottom line. `PageDown` and `PageUp` are the same |
| `Ctrl+E` `Ctrl+Y` | Scroll one line (a count of lines) down / up and leave the caret where it is, unless it would leave the screen |
| `zt` `zz` `zb` | Scroll so the caret's line is at the top / in the middle / at the bottom; the caret stays |
| `z` then `Enter`, `z.` `z-` | The same three, and the caret goes to the line's first non-blank. With a count, all six place the line the count names and move the caret to it |

`H`, `M` and `L` are motions like any other — `dL` deletes to the bottom of the screen, whole lines,
and `vH` extends a selection to the top — and `M` is the middle of the lines actually shown, so in a
file shorter than the window it is the middle of the file. The scrolls are commands rather than
motions, so no operator takes them, but every one of them works while selecting, where the caret's
move is the selection's. `Ctrl+D` stops scrolling where the last line reaches the bottom of the screen
and moves only the caret from there; `Ctrl+F` and `Ctrl+E` may go on until the last line is alone at
the top, as vim's do. None of them is a change, so `.` never repeats a scroll. Because the six Control
keys are commands in normal and visual mode, they are not shortcuts there — see *Keyboard shortcuts*.

### Operators

| Keys | |
|---|---|
| `d` `y` `c` + motion | Delete / yank / change over any motion above — `dw`, `y3j`, `cG`, `d0`, `de`, `d}`, `c)` |
| `d` `y` `c` + text object | The same over a text object — `diw`, `ciw`, `ci(`, `ya"` |
| `dd` `yy` `cc` | The same, on the whole line |
| `D` `C` | To the end of the line: delete, change |
| `Y` | Yank the whole line: `Y` is `yy`, not `y$` — vim's own irregularity, not neovim's |
| `>` `<` + motion / object | Shift the lines the range touches right / left by one shiftwidth — `>j`, `<}`, `>iw` |
| `>>` `<<` | The same on this line, and a count of lines below it |
| `gu` `gU` `g~` + motion / object | Lowercase / uppercase / toggle the case — `gUiw`, `guw`, `g~$` |
| `guu` `gUU` `g~~` | The same on this line, and a count of lines. `gugu`, `gUgU` and `g~g~` spell the same thing |

`cw` on a word changes to the *end* of the word, like `ce` (and `cW` like `cE`), and `dw` on the last
word of a line stops at the line's end. Both are vim's own irregularities, and both are here.

A shift is always linewise, whatever the motion, and leaves an empty line empty. How wide it is and
whether it is written as spaces or tabs follow the editor's own indentation settings, so `>>` indents
exactly as the Tab key does. Over a selection the count is the shift's own rather than a number of
lines: `V3>` moves the selected lines three widths, where `3>>` moves three lines by one. Neither
the shifts nor the case operators touch the register — nothing is taken out — and the caret lands
on the first non-blank after a shift and at the start of the range after a case change, which for
`guu` is the first column.

### Text objects

| Keys | |
|---|---|
| `iw` `aw` | The word under the caret; `aw` takes the blanks beside it too |
| `iW` `aW` | The same for a WORD, blank and not-blank being the only two classes |
| `i(` `a(` | Inside / including the enclosing `(` … `)`. `i)` and `ib` spell the same thing |
| `i[` `a[` | The same for `[` … `]`, and `i]` for it |
| `i{` `a{` | The same for `{` … `}`, with `i}` and `iB` |
| `i<` `a<` | The same for `<` … `>`, with `i>` |
| `i"` `a"` `i'` `a'` ``i` `` ``a` `` | Inside / including the surrounding quote pair |

A text object says *which text* rather than where to go, so it needs an operator in front of it —
`diw`, `ca(` — or a selection to redraw: `viw` selects the word the caret is on, and `vi{` the block
round it, both ends moving to the object rather than extending from where the `v` was.

How far each family reaches is the whole of what separates them. A word is a run of one of vim's
three character classes, the same ones `w` and `b` use, and it never leaves the caret's line — which
is why `aw` falls back to the blanks *before* the word when there are none after it. A bracket pair
counts nesting and is not bound to a line at all, so `ci{` over a fenced block is one keystroke.
A quote pair stays on the line and does not nest: pairs are counted off from the start of the line,
because nothing in the text says which of two identical characters opens and which closes.

A count multiplies a word object — `d3iw` is word, blanks, word, and `d2aw` is two words — and is
dropped by the others. An object with nothing round the caret does nothing at all, without a message.

### Editing

| Keys | |
|---|---|
| `x` `X` | Delete the character under / before the caret |
| `s` `S` | Substitute the character / the line: `cl` and `cc` |
| `p` `P` | Put after / before — beside the caret for characters, on a new line for lines |
| `r<char>` | Replace the character under the caret. Refuses rather than half-finishing |
| `~` | Swap the case under the caret and step past it |
| `J` `gJ` | Join this line with the one below (a count of lines, at least two), one space at the seam / nothing at all |
| `u` / `Ctrl+R` | Undo / redo |
| `ZZ` `ZQ` | Save and close / close without saving — `:wq` and `:q!` |
| `"{a-z}` | Use a named register for the next yank, delete, change or put — `"add`, `"ap`; `"A` appends |
| `"0` `"_` | The last yank, whatever was deleted since / the black hole: `"_dd` deletes and keeps nothing. `""` names the unnamed register explicitly |
| `.` | Repeat the last change, with a new count if one is given |
| `q{a-z}` `q` | Record the keys you type into a register, until the next `q`; `qA` appends to what `a` already holds |
| `@{a-z}` `@@` | Play a register back as keys, count times / play the last one again. `@"` and `@0` play the unnamed and yank registers |

The register records whether it holds characters or whole lines, which is why `xp` transposes and
`yyp` duplicates a line. It survives switching tabs. Put a `"` and a letter before a command to use a
named register — `"ayy` fills `a`, `"ap` puts it, and a capital (`"A`) appends; `"0` always holds the
last yank whatever you deleted since, and `"_` is the black hole that keeps nothing. `J` removes the
joined line's leading blanks and puts one space at the seam — none before a `)`, none after a line
that already ends in a blank, and never two after a full stop; `gJ` puts the lines together exactly as
they are. The caret lands on the seam, and the register is left alone.

`.` repeats the last change — an operator over a motion or object, a single-key edit, a put, a join,
or an insert with the text that was typed in it — as one undo step, and `3.` repeats it with a new
count. A macro is the same idea over several keys: `qa` starts recording into register `a` (the status
bar reads `recording @a`), `q` stops, and `@a` plays it back — `3@a` three times, `@@` whichever ran
last. The recording is watched as it is made, so you see it work before you replay it, and a replay
stops the moment a find or a search inside it fails. Because a macro register is just a register, a
recorded macro can be put into the buffer with `"ap`, edited, and yanked back.

### Counts

A count in front of almost anything, on either side of an operator, multiplying: `3dd`, `2d3w`, `5j`,
`4x`, `2p`. A count that overshoots clamps — `9j` on a three-line file lands on line three — except a
find, which finds nothing at all rather than settling for the last match it passed: `9fa` on a line
with three `a`s leaves the caret where it was.

Not every count multiplies: some name what they count. `2gg` and `3G` name a line, `N|` a column,
`N%` a percentage, `3H` and `3L` a line from the top or the bottom of the screen, and a count on the
`z` family names the line to place. Over a selection, `3>` is three shiftwidths rather than three
lines. `Ctrl+R` takes no count at all, where `3u` undoes three times.

### Search and ex commands

| Keys | |
|---|---|
| `/pattern` then `Enter` | Search forward for a regular expression — .NET syntax, not vim's |
| `?pattern` then `Enter` | The same, backward |
| `/` `?` then `Enter` | An empty pattern repeats the last one, the way the key points |
| `n` `N` | Next match the way the search went / the other way, wrapping with vim's own "search hit BOTTOM" message |
| `*` `#` | Search forward / backward for the first word at or after the caret on its line, as a whole word |
| `\c` `\C` | Anywhere in a pattern: match regardless of case / exactly, for this search alone |
| `:[range]s/pat/rep/[g][i][I]` | Substitute on the current line, or over a range — `%`, `3,7`, `.,$`, `'<,'>`, `.+2`. `:substitute` in full |
| `:N` `:$` | Go to line N / to the last line |
| `:set` `:se` | `ic` `scs` `ws` `sw` `et`, five ways: `:set ic`, `:set noic`, `:set ic!`, `:set sw=2`, `:set sw?` |
| `:w` `:q` `:wq` | Write, quit, write and quit — through the same save and unsaved-changes question the tab strip uses |
| `:x` `:wq!` | The same as `:wq` |
| `:q!` | Quit and throw away unsaved changes, without the question |

A pattern is a .NET regular expression, not vim's dialect: write `\b` for a word boundary, `(a|b)` for
a group and `{2,3}` for a count rather than `\<` `\(` `\{2,3}`, and `$1` in a replacement rather than
`\1`. `^` and `$` are a line's ends, a pattern may cross a line break, and one that will not compile —
or takes longer than a second to match — is named on the status bar and moves nothing. Case follows
vim: exact by default, `:set ignorecase` to loosen it, `:set smartcase` to make a capital in the
pattern mean it again, and `\c` or `\C` in the pattern to settle it for one search. `*` and `#` ignore
`smartcase`, as vim's do, and they take the first *keyword* at or after the caret on its line — so
`*` on the `(` of `(a)` searches for `a`. A line with no keyword left falls back to the run of
punctuation there, matched literally rather than between word boundaries, because a word boundary
means nothing beside a `)`. With `:set nowrapscan` a search stops at the end of the file and says so
instead of going round.

A search is a motion. `d/foo` deletes up to the match, `c?bar` changes back to it, `y*` yanks to the
next occurrence of the word and `dn` to the next match — exclusive, like every search in vim, and
linewise from the first column to a match in the first column — and in a visual mode `/` `?` `n` `N`
`*` `#` extend the selection. `.` repeats an operator over a search from wherever the caret is now.

`:s` is vim's shape with .NET's replacement: any punctuation can stand in for the `/`, `\r` in the
replacement breaks the line, `\t` is a tab, `&` is only an ampersand, and the pattern becomes the one
`n` repeats — an empty pattern, `:s//bar/`, reuses it. The whole command is one undo step, and the
status bar reads `3 substitutions on 2 lines`. `:set` knows `ignorecase` `smartcase` `wrapscan`
`shiftwidth` `expandtab`, by their long or short names; `:set` on its own shows them all.

### Not implemented

Marks, jump lists and folds — and, within what *is* here, `:` typed over a selection, the sentence
(`is`), paragraph (`ip`) and tag (`it`) objects, `2i(` walking a nesting level outwards, the
system-clipboard register `"+`, vim's own regex dialect (`\<` `\(` `\{n,m}` `\v`), the `c` flag of
`:s` and `&`, highlighted or incremental search, and `:wa` `:qa` `:e`. Most-wanted first. A
*character* key that is not in this reference is turned down by name rather than silently ignored,
and so is a half-typed sequence — but a key with no vim meaning at all is swallowed without a word:
`Tab`, `Delete`, `Insert`, the function keys. Saying so for each of them would turn the status bar
into a complaint about keys nobody pressed on purpose.

## Keyboard shortcuts

| | |
|---|---|
| `Ctrl+S` / `Ctrl+Shift+S` | Save / save all |
| `Ctrl+W` | Close the current tab |
| `Ctrl+Shift+V` | Cycle editor → split → preview |
| `Ctrl+B` / `Ctrl+I` / `Ctrl+K` | Bold, italic, link |
| `Ctrl+A` / `Ctrl+C` | In the preview: select the whole rendered note / copy it |
| `F2` / `Delete` | Rename / delete in the file tree |

In vim's normal and visual modes the editor also takes `Ctrl+D` `Ctrl+U` `Ctrl+F` `Ctrl+B` `Ctrl+E`
`Ctrl+Y` as scroll commands (see *Motions*) and `Ctrl+R` as redo. In insert mode, and with vim off,
they are the editor's again — `Ctrl+B` is bold.

## Installation

Both artifacts are **self-contained**: the .NET runtime ships inside them, so there is no SDK or
runtime to install first. Download them from the repository's Releases page.

### Windows

Run the `.msi`. It installs to `%ProgramFiles%\Enigma Markdown Editor` and adds a desktop and start
menu shortcut. Uninstall through *Apps & features* as usual.

### Linux

From the release tarball:

```bash
tar xzf EnigmaMarkdownEditor-<version>-linux-x64.tar.gz
cd EnigmaMarkdownEditor-<version>-linux-x64
./install.sh
```

Or straight from a checkout of this repository, which builds the application first (it needs the
.NET 10 SDK):

```bash
./packaging/linux/install.sh
```

Either way everything lands under `$HOME` — no root, no `sudo`, no package manager — and **Enigma
Markdown Editor** is in your application launcher:

| What | Where (`XDG_DATA_HOME` and `XDG_BIN_HOME` are honoured) |
|---|---|
| the application | `~/.local/share/enigma-markdown-editor` |
| an `enigma-markdown-editor` command | `~/.local/bin/enigma-markdown-editor`, a symlink to it |
| the desktop entry | `~/.local/share/applications/enigma-markdown-editor.desktop` |
| the icon, six sizes | `~/.local/share/icons/hicolor/<N>x<N>/apps/enigma-markdown-editor.png` |

The .NET runtime is bundled with the application, so it runs whatever is installed on the machine.
From a checkout, `--framework-dependent` builds against an installed .NET 10 runtime instead (a much
smaller install), `--rid` picks another runtime identifier, and `--from DIR` installs a directory you
published yourself; `--help` lists them. Run the installer again to upgrade in place — an
installation made by a 1.x installer in `~/.local/opt/enigma-markdown-editor` is replaced and
removed. `./uninstall.sh` (beside `install.sh`, in the tarball and in `packaging/linux/`) removes
those four things and nothing else: your settings and your notes are left as they are.

## Build from source

Requires the **.NET 10 SDK** (pinned in `global.json`: 10.0.100 with `rollForward: latestFeature`).

```
dotnet build Enigma.MarkdownEditor.slnx -c Release
dotnet test --solution Enigma.MarkdownEditor.slnx
dotnet run --project src/Enigma.MarkdownEditor.Desktop
```

The build bar is **zero warnings**: `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on
solution-wide via `Directory.Build.props`.

Tests are xUnit v3 on the [Microsoft Testing Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro).
MTP rejects a bare solution path, so the `--solution` flag above is required — plain `dotnet test`
still works from inside a test project's directory.

To produce the release artifacts:

```
./build/publish.ps1 -Rid win-x64        # -> artifacts/publish/win-x64
./build/publish.ps1 -Rid linux-x64      # -> artifacts/publish/linux-x64
./packaging/linux/pack-linux.ps1        # -> artifacts/EnigmaMarkdownEditor-<version>-linux-x64.tar.gz
```

Either runtime identifier can be published from either OS, and the scripts run under both Windows
PowerShell 5.1 and PowerShell 7+. The full release procedure is [`docs/RELEASE.md`](docs/RELEASE.md).

## Where your data lives

One folder per user holds everything the application writes, so a backup is one folder to copy and a
support request is one folder to zip:

| | |
|---|---|
| Windows | `%APPDATA%\Enigma.MarkdownEditor` |
| Linux | `~/.config/Enigma.MarkdownEditor` |

| File | |
|---|---|
| `settings.json` | Every setting, hand-editable |
| `workspaces.json` | Your workspaces and their folders |
| `session.json` | Open tabs, window placement, pane sizes |
| `logs/app.log` | The current log; older days are archived beside it |

A file that cannot be read is moved aside as `<name>.invalid.json` and the defaults are used, so a
corrupt file costs you that file's contents and nothing more. **Your notes are never in here** —
they are the markdown files in your own folders.

## Repository layout

```
src/
  Enigma.MarkdownEditor.Core        class library — no Avalonia dependency
                                    (vim engine, workspace/session/search, markdown pipeline)
  Enigma.MarkdownEditor.Desktop     the Avalonia application
tests/
  Enigma.MarkdownEditor.Core.UnitTests
  Enigma.MarkdownEditor.Desktop.UnitTests
build/                              publish scripts
packaging/linux/                    install.sh / uninstall.sh, desktop-entry template, icons,
                                    tarball packer
msiProfiles/                        one committed .msipkg.json per released version
docs/
  RELEASE.md                        the release runbook
  roadmap.md                        every work item and its status
  plan/<ID>.md                      the full plan per work item
  done/<ID>.md                      what was actually delivered, per completed unit of work
```

Assemblies keep the `Enigma.MarkdownEditor.*` prefix; the product's display name is
**Enigma Markdown Editor**.

## Conventions

- **Central Package Management** — every version lives in `Directory.Packages.props`; a
  `PackageReference` never carries a `Version` attribute.
- `LangVersion`, `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors` live once in
  `Directory.Build.props` and are never repeated in a `.csproj`.
- Implicit usings are **off**: every file declares its own `using` directives.
- Code style, naming and analyzer severities come from the root `.editorconfig`.

## Licence

[MIT](LICENSE.md) — Copyright © 2026 Josué Clément.
