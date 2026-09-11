# FEATURE-3C44 — Editor & tabs

**Status: DONE** · Multi-phase (3) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-3c44-phase01-editor-tabs`, `feature/feature-3c44-phase02-highlighting`, `feature/feature-3c44-phase03-assists`.

## Objective

The real editing experience: AvaloniaEdit-based markdown editing in tabs — line numbers, explicit save + optional autosave, TextMate syntax highlighting, and markdown-specific typing assists.

## Context & constraints

- Depends on FEATURE-0F9A (shell/settings) and FEATURE-0DE1 PHASE02 (open-from-tree; replaces its basic tab).
- New Desktop packages (versions already pinned in CPM): `Avalonia.AvaloniaEdit` 12.0.0, `AvaloniaEdit.TextMate` 12.0.0 (brings TextMateSharp 2.0.3 + Grammars).
- Save model (decided): explicit Ctrl+S / `:w`, dirty indicators, close prompts; autosave-on-idle via `AutosaveEnabled`/`AutosaveDelaySeconds` settings (default off).
- MVVM without source generators; all IO async; ContentDialog for prompts, InfoBar for errors.

## Design

- **DocumentManager** (Desktop service + VM layer): `ObservableCollection<DocumentViewModel>`, active document, open-by-path de-dup (activates the existing tab). `DocumentViewModel`: `FilePath`, `Title`, `IsDirty`, AvaloniaEdit `TextDocument`, original encoding/BOM preserved on save, line-ending style preserved per file (new files: LF).
- **Dirty tracking** via AvaloniaEdit's `UndoStack.IsOriginalFile` so undoing back to the saved state clears the dot.
- **Tab strip**: dirty dot, close button, middle-click close, `Ctrl+W` close, `Ctrl+Tab`/`Ctrl+Shift+Tab` next/previous, overflow scrolling. Close-with-unsaved → ContentDialog Save / Discard / Cancel (Cancel aborts the close, including app exit: window-closing intercept iterates dirty docs).
- **Commands**: Save (`Ctrl+S`), Save As (`IFileDialogService`), Save All (`Ctrl+Shift+S`).
- **Editor view**: `ShowLineNumbers = true`; `WordWrap`, font family/size from settings applied live; subtle current-line highlight using Enigma brushes; context menu cut/copy/paste/select-all.
- **Autosave**: per-document idle timer (delay setting), active only when enabled and the document is dirty; suspended while a vim pending sequence or a dialog is open.
- **External changes** (watcher events from FEATURE-0DE1): clean document → silent reload preserving caret/scroll where possible; dirty document → InfoBar with Reload / Keep mine; file deleted externally → tab title flagged `(deleted)`, saving recreates the file.
- **PHASE02 — TextMate**: markdown grammar via `AvaloniaEdit.TextMate` + `TextMateSharp.Grammars`; a dark and a light TextMate theme mapped to the app theme variant and switched live; fenced code blocks inside markdown get per-language token colors from the grammar set.
- **PHASE03 — assists** (active in insert/standard editing only — never in vim normal/visual):
  - *Auto-continue*: Enter inside `-`, `*`, `+`, `1.` (increments), `> `, `- [ ]` items continues the marker; Enter on an empty item removes it and ends the list; Tab / Shift+Tab indent/outdent the current item. Pure decision logic (`ListContinuation`) lives in Core and is unit-tested; vim `o`/`O` deliberately do **not** auto-continue (authentic vim behavior).
  - *Formatting shortcuts*: `Ctrl+B` → `**…**`, `Ctrl+I` → `*…*` around selection or word under caret (toggle off when already wrapped); `Ctrl+K` → `[text](url)` with the selection as text and caret in `url`.
  - *Paste image*: when the clipboard holds a bitmap, save it as PNG to `<note dir>/<ImageFolderName>/<note-stem>-<yyyyMMddHHmmss>.png` (directory created on demand) and insert `![](relative/path.png)` with forward slashes; failure or unsupported clipboard → InfoBar and the paste falls through to normal text paste.

## Phases

### PHASE01 — AvaloniaEdit & tab lifecycle — **Status: DONE**

Steps: packages → DocumentManager + DocumentViewModel + tab strip UI → open/save/save-as/save-all/close flows + prompts (incl. window-close intercept) → autosave timer → external-change policy → encoding/line-ending preservation → settings (font/size/wrap) applied live → status bar line:col wired to the active editor.

Acceptance criteria:
- Full open/edit/save/close matrix works, including cancel paths and app-exit with multiple dirty tabs.
- Dirty dot appears/disappears correctly (undo-to-saved clears it); de-dup activates existing tabs.
- CRLF file round-trips as CRLF, LF as LF; BOM preserved; new files LF.
- External clean-reload / dirty-InfoBar / deleted-flag behaviors verified.
- Desktop.UnitTests: DocumentManager lifecycle (open, dedup, dirty, close-cancel); Core.UnitTests: line-ending detection/preservation logic.

### PHASE02 — TextMate highlighting & appearance — **Status: DONE**

Steps: TextMate installation on the editor → markdown grammar + theme pair → live theme-variant switch → verify fence languages (csharp, json, bash) → editor appearance polish (selection/current-line brushes consistent with Enigma theme).

Acceptance criteria:
- Markdown structure and fenced C#/JSON/bash are colored; unknown fence languages degrade to plain.
- Switching app theme swaps editor colors live, no restart.
- Opening a ~1 MB markdown file stays responsive (manual check recorded in the completion doc).

### PHASE03 — Markdown editing assists — **Status: DONE**

Steps: Core `ListContinuation` logic + tests → editor key handling for Enter/Tab/Shift+Tab → Ctrl+B/I/K commands → paste-image service (clipboard bitmap → PNG file + link) → interplay rules with vim modes (assists suppressed outside insert/standard editing).

Acceptance criteria:
- Continuation matrix green in Core.UnitTests (all marker kinds, nesting, empty-item termination, numbered increment).
- Shortcuts work with selection and bare caret, and toggle cleanly.
- Paste-image produces the file + correct relative link on Windows (Linux verified manually — record it); graceful InfoBar fallback otherwise.
- With vim enabled: assists inactive in normal/visual; Enter in insert mode still continues lists.

## Dependencies

FEATURE-0F9A (shell, settings) · FEATURE-0DE1 PHASE02 (tree opens documents). FEATURE-11AA and FEATURE-6054 build on this item's PHASE01.

## Out of scope

Vim behavior (FEATURE-11AA) · preview (FEATURE-6054) · auto-close pairs (decided against) · split view (backlog).
