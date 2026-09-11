# FEATURE-E64A — New file starts with its title

**Status: DONE** · Single phase · Branch: `feature/feature-e64a-new-file-title` (cut from
`vibe/2026-09-10-title-colors-vim-release`)
Plan: `docs/plan/FEATURE-E64A.md` · Roadmap: `docs/roadmap.md`

## Summary

A note created from the tree's *New file…* now arrives with `# <its name>` and a blank line in it
instead of arriving empty. Only markdown arrives that way: every other extension the tree can create
is still a zero-byte file.

**The decision is one function over a name, and it lives in `Core`.** `MarkdownScaffold.For(name)`
answers with the heading or with the empty string, and it asks `FileTreeFilter.IsMarkdown` rather
than looking at the extension itself — so "what counts as markdown" stays the one answer the tree
filters, opens and highlights by. That is what makes the whole behaviour reachable from 17 test cases
with no file system in sight, and it is the split `FileTreeViewModel`'s own class comment asks for:
the decisions in `Core`, the glue in the tree.

**Three edge cases the plan named, all covered.** A file called `.md` has an extension and no name,
so `Path.GetFileNameWithoutExtension` answers empty and the scaffold answers empty too — `# ` on its
own is not a title, and a heading the user has to finish is worse than none. A name carrying a folder
(`C:\notes\idea.md`) gives the file's title and never the folder's. And the title is the name
verbatim: `2026-09-10.stand-up.md` becomes `# 2026-09-10.stand-up`, with no case change and no `-`
turned into a space, because someone who names a file that way named it that on purpose.

**The heading is a second write, deliberately.** `FileOperations.CreateFile` keeps
`FileMode.CreateNew` as the only thing standing between "the name was taken since the check" and a
file being silently overwritten, and its public surface and its four tests are untouched. The write
that follows goes through `DocumentFile.WriteAsync` with `TextFileFormat.Default`, which is the one
place in the application that says what plain text means here — UTF-8 without a byte-order mark,
line feeds — so the new note is byte-identical in shape to every other file the editor writes. A
write that fails leaves exactly the empty file the previous behaviour produced, and `RunAsync`
reports it through the InfoBar like any other failure of the operation.

**Order matters and is now fixed by a test.** The content is written *before* the reveal and the
open, or the tab would show an empty buffer for a file that has a heading on disk.

## Files/modules touched

**Created**

- `src/Enigma.MarkdownEditor.Core/Markdown/MarkdownScaffold.cs` — `For(string? fileName)`, the whole
  decision.
- `tests/Enigma.MarkdownEditor.Core.UnitTests/Markdown/MarkdownScaffoldTests.cs` — 19 cases: the
  markdown extensions including a shouted `.MD`, the non-markdown ones and an extensionless name,
  null/empty/white-space, the three no-name-in-front-of-the-extension forms, a name carrying its
  folder, and the exact shape of the output including its blank line.
- `docs/done/FEATURE-E64A.md` — this document.

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/ViewModels/FileTreeViewModel.cs` — three `using` directives and
  a five-line block in `NewFileAsync`, between the create and the reveal.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` — two cases:
  a new `idea.md` is on disk as exactly the bytes `# idea\n\n`, and a new `data.json` is on disk
  empty. The first asserts on **bytes** rather than text on purpose: the byte-order mark's absence
  and the line feeds are half of what the format decision buys.
- `README.md` — the file-tree bullet, from the documentation freshness sweep. See *Deviations*.

**Untouched, deliberately**

- `src/Enigma.MarkdownEditor.Core/Files/FileOperations.cs` and its tests — the create still creates
  an empty file, which is what keeps `FileMode.CreateNew` meaningful.
- `Core/Settings/AppSettings.cs` — no setting was added (see the plan's decision table).

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-E64A` `TODO` → `IN PROGRESS` → `DONE`. `IN PROGRESS` is the widest
  Status cell the table can hold and it was already the column's width, so no other row moved and
  the net diff is one row.
- `docs/plan/FEATURE-E64A.md` — the item header's status.

## Deviations & follow-ups

- **The README edit is an addition, not a correction, and it was made anyway.** The sweep's rule is
  to edit only what the diff made factually wrong, and "Create, rename, move and delete notes and
  folders from the tree" was not made *wrong* by this dev — it was made *incomplete*, in a way the
  user meets on their very next new note. `docs/done/FEATURE-F6A4.md` records the cost of leaving
  such a gap for the release item to find (1.5.0's sweep had to fold in a bullet
  `FEATURE-4797-PHASE02` had declined), so the clause was added here where the change is fresh
  rather than deferred. One sentence, in the bullet the diff is about; nothing was reorganized.
- **The first attempt at the byte assertion did not compile**, because the `\n` escapes in the
  generated test source were written as real newlines inside a `u8` literal (`CS1010: Newline in
  constant`). Fixed inside the same fix cycle; the second build was clean. Recorded because the
  cause was the authoring route, not the design.
- **Nothing prettifies the title.** A file called `my_notes.md` gets `# my_notes`, not
  `# My notes`. That is the plan's decision, and if it ever becomes unwanted it is a one-line change
  in one function — which is the point of the function existing.
- **The heading is not added to files created outside the tree**, nor to a *Save as* onto a new path:
  both are out of scope, and both are cases where the user already has content in the buffer.
- **No line-ending noise.** `git diff --check` is empty, and every file this dev touched or created
  is LF throughout, as `.gitattributes` (`* text=auto eol=lf`) requires. Nothing was done and nothing
  needs to be.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **succeeded,
  0 Warning(s), 0 Error(s)**, with `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on
  solution-wide.
- `grep -i AVLN` over that same normal-verbosity log: **no output**. This dev touches no XAML, but
  the build was non-incremental so the XAML compiler actually ran across all four projects.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3453 passed, 0 failed, 0
  skipped** — the 3432 that were green at 1.5.0, plus the 21 added here (19 scaffold cases + 2 tree
  cases).
- **One fix cycle used of the three available**: the compile error above. The build and the suite
  were green on the cycle after it.
- **Acceptance criteria, one by one.**
  - Creating `notes.md` leaves exactly `# notes\n\n` in UTF-8 with no byte-order mark ✓ —
    `ANewNote_ArrivesWithItsOwnTitleInIt` asserts the bytes.
  - Creating a non-markdown file leaves a zero-byte file ✓ — `ANewFileThatIsNotMarkdown_ArrivesEmpty`.
  - `MarkdownScaffold.For` never returns `"# "` ✓ — the three `.md`-with-no-name cases.
  - `FileOperations`' public API and `FileOperationsTests` unmodified ✓ — `git diff` names neither.
  - Release build 0 warnings, full suite green ✓ — above.
  - Roadmap and plan flipped to `DONE`, completion doc written ✓ — this document.
- **Not verified here, and worth a look before the release tag:** nobody has watched the new note
  open in a live window. The tab, the caret and the preview are all driven from the file's content,
  which the test pins byte for byte, but this environment cannot show a window — the same standing
  limitation `docs/RELEASE.md` §1 already carries pre-flight items for.
