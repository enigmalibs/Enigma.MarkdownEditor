# Roadmap

| ID           | Title                                 | Status    | Plan                      |
|--------------|---------------------------------------|-----------|---------------------------|
| FEATURE-0F9A | Solution bootstrap & app shell        | DONE      | docs/plan/FEATURE-0F9A.md |
| - PHASE01    | Repo hygiene & solution skeleton      | DONE      | (in FEATURE-0F9A.md)      |
| - PHASE02    | Avalonia bootstrap & 3-pane shell     | DONE      | (in FEATURE-0F9A.md)      |
| - PHASE03    | Settings, logging & crash handling    | DONE      | (in FEATURE-0F9A.md)      |
| FEATURE-0DE1 | Workspaces & file tree                | DONE      | docs/plan/FEATURE-0DE1.md |
| - PHASE01    | Workspace model & management UI       | DONE      | (in FEATURE-0DE1.md)      |
| - PHASE02    | File tree, filter & watcher           | DONE      | (in FEATURE-0DE1.md)      |
| - PHASE03    | File operations                       | DONE      | (in FEATURE-0DE1.md)      |
| FEATURE-3C44 | Editor & tabs                         | DONE      | docs/plan/FEATURE-3C44.md |
| - PHASE01    | AvaloniaEdit & tab lifecycle          | DONE      | (in FEATURE-3C44.md)      |
| - PHASE02    | TextMate highlighting & appearance    | DONE      | (in FEATURE-3C44.md)      |
| - PHASE03    | Markdown editing assists              | DONE      | (in FEATURE-3C44.md)      |
| FEATURE-11AA | Vim emulation                         | DONE      | docs/plan/FEATURE-11AA.md |
| - PHASE01    | Engine core, modes & motions          | DONE      | (in FEATURE-11AA.md)      |
| - PHASE02    | Operators, counts & registers         | DONE      | (in FEATURE-11AA.md)      |
| - PHASE03    | Visual modes                          | DONE      | (in FEATURE-11AA.md)      |
| - PHASE04    | Search & ex commands                  | DONE      | (in FEATURE-11AA.md)      |
| FEATURE-6054 | Markdown preview                      | DONE      | docs/plan/FEATURE-6054.md |
| - PHASE01    | Markdig pipeline & renderer v1        | DONE      | (in FEATURE-6054.md)      |
| - PHASE02    | Tables, code highlight & links        | DONE      | (in FEATURE-6054.md)      |
| - PHASE03    | View modes & scroll sync              | DONE      | (in FEATURE-6054.md)      |
| FEATURE-40F5 | Search & session restore              | DONE      | docs/plan/FEATURE-40F5.md |
| - PHASE01    | Workspace-wide search                 | DONE      | (in FEATURE-40F5.md)      |
| - PHASE02    | Session restore                       | DONE      | (in FEATURE-40F5.md)      |
| FEATURE-5B28 | Release 1.0.0                         | DONE      | docs/plan/FEATURE-5B28.md |
| - PHASE01    | Packaging, docs & scripts             | DONE      | (in FEATURE-5B28.md)      |
| - PHASE02    | Version, MSI profile & runbook        | DONE      | (in FEATURE-5B28.md)      |
| FEATURE-F3FA | Tab strip polish                      | DONE      | docs/plan/FEATURE-F3FA.md |
| FEATURE-E340 | Typography settings                   | DONE      | docs/plan/FEATURE-E340.md |
| - PHASE01    | Editor font picker & line numbers     | DONE      | (in FEATURE-E340.md)      |
| - PHASE02    | Preview typography & settings         | DONE      | (in FEATURE-E340.md)      |
| FEATURE-C8F1 | Interactive preview checkboxes        | DONE      | docs/plan/FEATURE-C8F1.md |
| BUG-7675     | Vim j/k loses the caret column        | ABANDONED | docs/plan/BUG-7675.md     |
| FEATURE-7FA8 | Splash screen & about dialog          | DONE      | docs/plan/FEATURE-7FA8.md |
| - PHASE01    | About dialog & version plumbing       | DONE      | (in FEATURE-7FA8.md)      |
| - PHASE02    | Splash screen & startup sequence      | DONE      | (in FEATURE-7FA8.md)      |
| FEATURE-284C | First-run setup                       | DONE      | docs/plan/FEATURE-284C.md |
| FEATURE-6EF0 | Release 1.1.0                         | DONE      | docs/plan/FEATURE-6EF0.md |
| BUG-7AB2     | Tab, dirty flag, exit & font fixes    | DONE      | docs/plan/BUG-7AB2.md     |
| - PHASE01    | Closing a tab leaves a live editor    | DONE      | (in BUG-7AB2.md)          |
| - PHASE02    | Dirty flag missed in insert mode      | DONE      | (in BUG-7AB2.md)          |
| - PHASE03    | Process hangs on exit                 | DONE      | (in BUG-7AB2.md)          |
| - PHASE04    | Editor font family never applied      | DONE      | (in BUG-7AB2.md)          |
| - PHASE05    | Preview code font size & weight       | DONE      | (in BUG-7AB2.md)          |
| FEATURE-2760 | Release 1.2.0                         | DONE      | docs/plan/FEATURE-2760.md |
| BUG-8F2A     | Tab scrollbar & checkbox alignment    | DONE      | docs/plan/BUG-8F2A.md     |
| - PHASE01    | Thin scrollbar on the tab strip       | DONE      | (in BUG-8F2A.md)          |
| - PHASE02    | Preview checkbox alignment & size     | DONE      | (in BUG-8F2A.md)          |
| FEATURE-414B | Settings surface                      | DONE      | docs/plan/FEATURE-414B.md |
| - PHASE01    | Preview labels & editor default       | DONE      | (in FEATURE-414B.md)      |
| - PHASE02    | Settings as a content dialog          | DONE      | (in FEATURE-414B.md)      |
| FEATURE-23AB | Vim find motions & text objects       | DONE      | docs/plan/FEATURE-23AB.md |
| - PHASE01    | Character find & repeat               | DONE      | (in FEATURE-23AB.md)      |
| - PHASE02    | Text objects                          | DONE      | (in FEATURE-23AB.md)      |
| FEATURE-449F | Release 1.3.0                         | DONE      | docs/plan/FEATURE-449F.md |
| FEATURE-724A | Vim: repeat, registers & search       | DONE      | docs/plan/FEATURE-724A.md |
| - PHASE01    | Motions & editing extras              | DONE      | (in FEATURE-724A.md)      |
| - PHASE02    | Visual-mode commands                  | DONE      | (in FEATURE-724A.md)      |
| - PHASE03    | Registers, repeat & macros            | DONE      | (in FEATURE-724A.md)      |
| - PHASE04    | Regex search, substitute & :set       | DONE      | (in FEATURE-724A.md)      |
| - PHASE05    | Screen motions & viewport             | DONE      | (in FEATURE-724A.md)      |
| BUG-B549     | Settings dialog polish & test hint    | DONE      | docs/plan/BUG-B549.md     |
| - PHASE01    | xUnit cancellation-token hint         | DONE      | (in BUG-B549.md)          |
| - PHASE02    | Settings dialog chrome                | DONE      | (in BUG-B549.md)          |
| FEATURE-53E4 | Release 1.4.0                         | DONE      | docs/plan/FEATURE-53E4.md |
| BUG-38F5     | Save leaves the dirty dot in vim      | DONE      | docs/plan/BUG-38F5.md     |
| FEATURE-6A2F | Release 1.4.1                         | DONE      | docs/plan/FEATURE-6A2F.md |
| FEATURE-4797 | Preview select-all & copy             | DONE      | docs/plan/FEATURE-4797.md |
| - PHASE01    | Select-all & plain-text copy          | DONE      | (in FEATURE-4797.md)      |
| - PHASE02    | Rich-text (HTML) flavour              | DONE      | (in FEATURE-4797.md)      |
| FEATURE-F6A4 | Release 1.5.0                         | DONE      | docs/plan/FEATURE-F6A4.md |
| FEATURE-E64A | New file starts with its title        | DONE      | docs/plan/FEATURE-E64A.md |
| FEATURE-A225 | Folder & tab colours                  | DONE      | docs/plan/FEATURE-A225.md |
| - PHASE01    | Colours in the workspaces file        | DONE      | (in FEATURE-A225.md)      |
| - PHASE02    | Folder colours in the tree            | DONE      | (in FEATURE-A225.md)      |
| - PHASE03    | Tab colours                           | DONE      | (in FEATURE-A225.md)      |
| FEATURE-3505 | Vim o/O continues list items          | DONE      | docs/plan/FEATURE-3505.md |
| FEATURE-8381 | Release 1.6.0                         | DONE      | docs/plan/FEATURE-8381.md |
| BUG-18AD     | Preview code spans barely visible     | DONE      | docs/plan/BUG-18AD.md     |
| - PHASE01    | Code-span chip gets its own colour    | DONE      | (in BUG-18AD.md)          |
| - PHASE02    | Code-span chip padding                | DONE      | (in BUG-18AD.md)          |
| FEATURE-4BB7 | Linux installer & uninstaller         | DONE      | docs/plan/FEATURE-4BB7.md |
| FEATURE-3D59 | Release 1.7.0                         | DONE      | docs/plan/FEATURE-3D59.md |
| FEATURE-1C17 | Enigma.Avalonia.Desktop 1.2.0         | DONE      | docs/plan/FEATURE-1C17.md |
| FEATURE-425B | No success or info InfoBars           | DONE      | docs/plan/FEATURE-425B.md |
| FEATURE-4B09 | File tree multi-selection             | DONE      | docs/plan/FEATURE-4B09.md |
| - PHASE01    | Multi-selection in the tree           | DONE      | (in FEATURE-4B09.md)      |
| - PHASE02    | Delete the whole selection            | DONE      | (in FEATURE-4B09.md)      |
| FEATURE-47F5 | Confirm every file tree delete        | DONE      | docs/plan/FEATURE-47F5.md |
| FEATURE-23F7 | Release 1.8.0                         | DONE      | docs/plan/FEATURE-23F7.md |
| FEATURE-EA38 | New application icon                  | DONE      | docs/plan/FEATURE-EA38.md |
| FEATURE-977D | Release 1.9.0                         | DONE      | docs/plan/FEATURE-977D.md |
| FEATURE-3185 | Remove the splash screen              | DONE      | docs/plan/FEATURE-3185.md |
| FEATURE-982C | Uniform About dialog                  | DONE      | docs/plan/FEATURE-982C.md |
| FEATURE-7CE4 | App icon in the output directory      | DONE      | docs/plan/FEATURE-7CE4.md |
| FEATURE-2877 | Update NuGet packages                 | DONE      | docs/plan/FEATURE-2877.md |
| FEATURE-E31C | Catch all unhandled exceptions        | DONE      | docs/plan/FEATURE-E31C.md |
| - PHASE01    | Early hooks & uniform error dialog    | DONE      | (in FEATURE-E31C.md)      |
| - PHASE02    | Start-up failure dialog               | DONE      | (in FEATURE-E31C.md)      |
| FEATURE-F36D | Publish scripts for Windows and Linux | TODO      | docs/plan/FEATURE-F36D.md |
| FEATURE-B260 | Release 1.10.0                        | TODO      | docs/plan/FEATURE-B260.md |

> `BUG-7675` abandoned — not reproducible. The sticky column is correct over the real control: `j`/`k`
> through a short line returns to the column the run started in, and so do the arrow keys, a count,
> an empty intermediate line, holding `j` against the last line, and `$`. Both suspects in the plan
> were disproven — `CaretOffset` round-trips exactly, and `ScrollToCaret` cannot move the caret.
