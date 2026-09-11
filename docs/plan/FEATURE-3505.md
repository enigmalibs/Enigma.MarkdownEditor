# FEATURE-3505 — Vim o/O continues list items

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-3505-vim-open-list`, cut from the run branch at build time.
**Run:** vibe/2026-09-10-title-colors-vim-release

## Objective

`o` and `O` open a line that continues the markdown item the caret is on, the way Enter already does
in insert mode: `- ` repeats, `1.` counts on, `- [x]` becomes `- [ ]`, `> ` keeps its depth, and the
indentation is the one the user wrote. Today both keys open a bare empty line, so the one way to get
a second bullet with vim on is `A` then Enter.

## Context & constraints

Established by reading the code, not assumed.

- **The continuation rules already exist and are already tested.** `Core/Markdown/ListContinuation.cs`
  is documented as holding the rules "in one place rather than spread through a key handler", and
  `ListItemPrefix.Continuation` is exactly the text a continued line starts with — the same indent,
  quote depth and spacing, the next marker, and an empty task box when the item has one.
  `ListContinuationTests` covers the matrix. This item reuses that, it does not restate it.
- **The keys reach two different places.** With vim off — or in insert mode — Enter is taken by
  `Views/MarkdownAssists.cs`, gated on `DocumentManagerViewModel.AssistsActive`. In normal mode the
  assists are off by design (`VimEditingAssistPolicy`), and `o`/`O` are parsed by
  `KeySequenceParser` (`:656`) into `VimAction.OpenLineBelow` / `OpenLineAbove` and executed by
  `VimEngine.OpenLine` (`:1991`). So the change belongs in the engine, not in the assists.
- **`VimEngine.OpenLine` already does the whole job in one `Replace`.** It opens the insert group,
  inserts `_buffer.NewLine` at the end (or the start) of the current line, moves the caret, then
  `_typed.Begin(caret)` and `SetMode(Insert)`. The prefix can ride in that same single `Replace`,
  which keeps the engine's stated rule — one command, one buffer write, one undo step — intact.
- **`_typed.Begin` must stay after the prefix.** The insert recorder is what `.` and a macro replay,
  and the prefix is the *command's* work, not the user's typing: recording it would make `.` write
  the marker twice.
- **A count replays through `RepeatTyped`** (`VimEngine.cs:2719`), which for `o`/`O` inserts
  `newLine + text` per copy. Re-deriving the prefix from the line just written, per copy, is what
  makes `3o` produce three continued items.
- **`.` re-executes the command** through `RepeatLastChange` → `Execute` → `OpenLine`, so the prefix
  is taken from the line the caret is on at that moment, which is the vim-correct answer and needs no
  extra code.
- **`Core/Vim` may use `Core/Markdown`.** They are two namespaces of one assembly, and the engine's
  stated isolation is from *Avalonia* ("Nothing here knows about Avalonia, a key event or a text
  control"), not from markdown. `Core/Documents` already reaches into `Core/Files` the same way.
- **`VimLine.Text` is the line's text without its terminator**, which is exactly what
  `ListContinuation.Parse` takes.
- **`TreatWarningsAsErrors` is on solution-wide**, so the bar is a 0-warning release build. No XAML
  is touched, so no `AVLN` sweep is needed beyond the standard build.

## Design

**1 — `ListItemPrefix.Opening`** (new property, beside `Continuation`)

The text a line opened *above* this item starts with: what `Continuation` repeats, but with this
item's **own** marker rather than the next one, because a line inserted before an item takes that
item's place in the list and pushes it down — counting on would number the new item after the one it
precedes. The task box is still the empty one, for the same reason `Continuation`'s is.

```csharp
public string Opening => IsTask
    ? string.Concat(Indent, Quote, Marker, MarkerSpacing, UncheckedBox, CheckboxSpacing)
    : string.Concat(Indent, Quote, Marker, MarkerSpacing);
```

**2 — `ListContinuation.OpenBelow` / `OpenAbove`** (new, string in / string out)

```csharp
public static string OpenBelow(string? line)
public static string OpenAbove(string? line)
```

The text the opened line starts with, or `string.Empty` for "nothing to continue" — a line that is
not an item at all, and an **empty** item, which is a list the user is ending rather than continuing.
`OpenBelow` answers `Continuation`, `OpenAbove` answers `Opening`. Two methods rather than one with a
flag, so a case reads as the key it is about. Deliberately strings rather than `TextEdit`s: the
caller here is the vim engine, which owns the offsets and the caret, unlike the Enter path where
`TextEdit` carries both.

**3 — `VimEngine.OpenLine`**

```csharp
private void OpenLine(bool below)
{
    VimLine line = CurrentLine;
    string prefix = below
        ? ListContinuation.OpenBelow(line.Text)
        : ListContinuation.OpenAbove(line.Text);
    ...
}
```

Below: `Replace(line.EndOffset, 0, NewLine + prefix)`, caret at
`line.EndOffset + NewLine.Length + prefix.Length`. Above: `Replace(line.Offset, 0, prefix + NewLine)`,
caret at `line.Offset + prefix.Length`. Still one `Replace` and one undo group; `_typed.Begin` and
`SetMode` are unchanged and stay after it.

**4 — `VimEngine.RepeatTyped`**

For both open actions, derive the prefix from `CurrentLine.Text` inside the loop — after the first
copy that is the line the user just typed — and insert `newLine + prefix + text` (below) or
`prefix + text + newLine` (above), with the caret adjusted by the prefix's length.

**5 — Tests** — `tests/…/Core.UnitTests/Vim/VimInsertTests.cs` (where `o`/`O` already live) plus the
matrix cases in `ListContinuationTests`:

- `o` on `- foo`, `* foo`, `+ foo`, `1. foo`, `9. foo`, `42) foo`, `> quoted`, `- [ ] todo`,
  `- [x] done`, `  - nested`, `> - quoted item`;
- `O` on the same, asserting the marker is the item's own (`2. bar` gives `2. `, not `3. `);
- `o`/`O` on a line that is not an item — a heading, a paragraph, `---`, an empty line — open a bare
  line exactly as today;
- `o`/`O` on an **empty** item (`- ` with nothing after it) open a bare line and leave the item alone;
- one undo (`u`) takes the whole thing back, prefix included;
- `3o` writes three continued items;
- `o` then `Esc` then `.` writes another continued item;
- a `\r\n` buffer keeps its terminator (`FakeVimBuffer`'s `NewLine`), and the prefix carries no
  terminator of its own.

## Acceptance criteria

- Every case above passes, and `ListContinuationTests`' existing Enter matrix is untouched and green.
- `o`/`O` remain a single undo step and a single `Replace`.
- `.` and a count both reproduce the continuation.
- No new `VimOptions` field and no new `:set` name (see *Decisions*).
- Release build 0 warnings; full suite green.
- Roadmap and this file flipped to `DONE`; `docs/done/FEATURE-3505.md` written.

## Out of scope

`gO`, `]o` or any key vim does not have · renumbering the rest of an ordered list after an insert ·
auto-continuation on `p` of a yanked item · `formatoptions`-style configuration · changing what Enter
does · the assists' Tab/Shift+Tab indentation.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Is the behaviour optional? | Unconditional | Enter's continuation is unconditional too, and the ask is explicitly "like done by pressing ENTER in insert mode". A flag nobody sets is a branch nobody tests | A `VimOptions` switch; a `:set` name |
| A `:set` name for it? | No | `VimSet`'s own comment says an unknown option is named as unknown rather than silently accepted — the names there are vim's own, and vim has no option that means this. Inventing one would teach a vim user something false | `:set continuelists`, `:set fo=ro` |
| What marker does `O` use? | The item's own | A line inserted before `2.` takes its place and pushes it down; `3.` above `2.` is wrong in a way the user has to fix by hand | The next marker, like `o` |
| `o` on an empty item? | Open a bare line, leave the item alone | Enter *clears* an empty item because the caret is on it; `o` in vim never edits the line it is on, so the analogue is to continue nothing | Clearing the empty item as Enter does; writing a second empty marker |
| Where does the code go? | `Core/Vim` calling `Core/Markdown` | Same assembly, and the engine's documented isolation is from Avalonia, not from markdown. The rules stay in the one place that owns them | An `IVimLineContinuation` seam injected into the engine: indirection for one caller; duplicating the rules inside the engine |
| Return type of the new methods? | `string` (empty for "nothing") | The engine owns the offsets and the caret; a `TextEdit` would carry two values it would discard | `TextEdit?`, mirroring `OnEnter` |
| Prefix included in `.`'s recorded text? | No — inserted before `_typed.Begin` | It is the command's work; recording it would make `.` write the marker twice | Recording it as typed text |
