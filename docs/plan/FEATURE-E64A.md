# FEATURE-E64A — New file starts with its title

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-e64a-new-file-title`, cut from the run branch at build time.
**Run:** vibe/2026-09-10-title-colors-vim-release

## Objective

A note created from the tree's *New file…* arrives with its own level-1 heading already in it —
`# <the file's name without its extension>`, then a blank line — instead of arriving empty. Only
markdown files get one; every other extension the tree can create still arrives empty.

## Context & constraints

Established by reading the code, not assumed.

- **`FileOperations.CreateFile` creates the file and writes nothing through the handle**
  (`Core/Files/FileOperations.cs:187`), deliberately: `FileMode.CreateNew` is what makes the
  operation a create rather than a truncate, and the comment says outright that "a new note starts
  empty". That sentence is what this item changes.
- **The glue lives in the tree, the decisions live in `Core`.** `FileTreeViewModel`'s own class
  comment states the split: "Every *decision* they take is in `Enigma.MarkdownEditor.Core.Files` …
  so what is left here is the glue: ask, plan, execute, re-read, report." The "is this markdown, and
  what heading does it get" decision therefore belongs in `Core`, and the write belongs in the glue.
- **`Core/Documents` already depends on `Core/Files`** (`DocumentFile.cs` throws
  `FileOperationException`), so the reverse direction is deliberately not taken — see *Decisions
  taken autonomously*.
- **`TextFileFormat.Default` is the one place the application's idea of "plain text" is written
  down** (its own doc comment says so): UTF-8 without a byte-order mark, line feeds. It is what a
  file the editor creates gets, so the heading is written through it.
- **The name is whatever the user typed.** `IFileOperationPrompts.AskForNewFileNameAsync` pre-fills
  `.md` and puts the caret before it (`Services/FileOperationPrompts.cs:36`), but the user can type
  any extension or none — so `FileTreeFilter.IsMarkdown` has to be asked rather than assumed.
- **The new file is opened straight afterwards** when the editor is the right application for it
  (`FileTreeViewModel.NewFileAsync`), so the heading is on screen the moment the dialog closes. The
  order matters: the content has to be written *before* the open, or the tab shows an empty buffer.
- **`RunAsync` already reports every failure** the operation can raise, through the InfoBar with the
  title "The file could not be created" — an `IOException` from the write lands there like any other.
- **`TreatWarningsAsErrors` is on solution-wide**, so the bar is a 0-warning release build.

## Design

**1 — `Core/Markdown/MarkdownScaffold.cs`** (new, `public static class`)

```csharp
public static string For(string? fileName)
```

The text a newly created file starts with, or the empty string when it should start empty:

- not markdown (`FileTreeFilter.IsMarkdown` says no), null, empty or white-space → `string.Empty`;
- a name whose stem is empty or white-space (`.md`, `   .md`) → `string.Empty`: there is no title in
  it, and `# ` alone is not a heading worth writing;
- otherwise `"# " + stem + "\n\n"`, where `stem` is `Path.GetFileNameWithoutExtension(fileName)`
  trimmed of surrounding white-space, written verbatim otherwise — no case change, no `-`/`_` to
  space rewriting (see *Decisions taken autonomously*).

Line feeds, not `Environment.NewLine`: everything in `Core` holds text with line feeds alone
(`DocumentFile.ReadAsync` normalizes on the way in) and `LineEndings.Apply` puts the file's own
terminator back on the way out.

**2 — `FileTreeViewModel.NewFileAsync`**

After the create and *before* the reveal and the open:

```csharp
string created = await FileOperations.CreateFileAsync(directory, name, cancellationToken);

if (MarkdownScaffold.For(name) is { Length: > 0 } scaffold)
{
    await DocumentFile.WriteAsync(created, scaffold, TextFileFormat.Default, cancellationToken);
}
```

Two operations rather than one: `FileOperations`' public surface and its four tests stay untouched,
and the file the write fails on is exactly the empty file the previous behaviour produced.

**3 — Tests**

- `tests/…/Core.UnitTests/Markdown/MarkdownScaffoldTests.cs` — a theory over the extensions
  (`.md`, `.markdown`, `.MD`, `.txt`, `.png`, none), the stem cases (spaces in the name, a dotted
  name such as `2026-09-10.notes.md`, `.md` alone, empty, null), and the exact shape of the output
  including its blank line.
- `tests/…/Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` — a new file called `notes.md`
  is on disk with `# notes` and a blank line in it; one called `notes.txt` is on disk and empty.

## Acceptance criteria

- Creating `notes.md` in the tree leaves a file whose bytes are exactly `# notes\n\n` in UTF-8 with
  no byte-order mark, and the tab that opens shows that heading.
- Creating `data.json` (with *Show all files* on) leaves a zero-byte file, as before.
- `MarkdownScaffold.For` is covered for every case listed above and never returns `"# "`.
- `FileOperations`' public API and `FileOperationsTests` are unmodified.
- Release build: 0 warnings; full suite green, including the new tests.
- Roadmap and this file flipped to `DONE`; `docs/done/FEATURE-E64A.md` written.

## Out of scope

Prettifying the title (case, separators) · a setting to switch the heading off · front matter ·
a template file or any other scaffolding · touching the *New folder* path · adding a heading to
files created outside the tree.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| What is the title's text? | The file name without its extension, verbatim | The ask is "a main title with the filename"; anything else guesses at a title the user did not type | Title-casing it; turning `-`/`_` into spaces — both surprise the user who named the file deliberately |
| Which files get a heading? | Markdown only | A `# heading` written into a `.json` or `.ps1` the tree can also create is corruption, not help | Every file; every text file |
| Is it optional? | No setting | Enter-key list continuation — the closest existing behaviour — has no setting either, and a one-line heading is trivially deleted | An `AppSettings` flag plus a settings-dialog row, for a behaviour nobody will want off |
| Where is the heading written? | A second call, `DocumentFile.WriteAsync`, from the tree's glue | Keeps `FileOperations` and its tests untouched, and reuses the one place that knows what "plain text" means here | A `content` parameter on `CreateFileAsync` — which would make `Core/Files` depend on `Core/Documents`, closing a namespace cycle, or duplicate the UTF-8/LF definition |
| Trailing blank line? | Yes — `# name\n\n` | It is what a person writing the note would type next, and it puts the first paragraph one Enter away rather than against the heading | Heading alone; heading plus two blank lines |
