# FEATURE-0DE1 — Workspaces & file tree

**Status: DONE** · Multi-phase (3) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-0de1-phase01-workspaces`, `feature/feature-0de1-phase02-tree`, `feature/feature-0de1-phase03-file-ops`.

## Objective

Named workspaces — each an ordered list of root directories — managed entirely in-app and persisted per user, plus a file tree that stays in sync with the file system and supports full file management.

## Context & constraints

- Depends on FEATURE-0F9A (shell, settings, Enigma services, logging).
- Persistence via **Enigma.Configuration**: `workspaces.json` → `WorkspacesConfig { int Version = 1, List<Workspace> Workspaces, Guid? LastActiveWorkspaceId }`; `Workspace { Guid Id, string Name, List<string> RootDirectories }`. Atomic writes and validation come from the library.
- All file IO async (dotnet-async skill); UI updates marshalled via `Dispatcher.UIThread`.
- Dialogs/notifications: ContentDialog for decisions, InfoBar for non-blocking errors, `IFolderDialogService` for directory picking.

## Design

- **Core** `Workspaces/`: models above + `IWorkspaceService` (list/create/rename/delete workspace; add/remove/reorder roots; `ActiveWorkspace` + change events; persistence through `IConfig<WorkspacesConfig>`). Validation: non-empty unique name, ≥ 1 root; a missing root directory is a warning (shown in UI), never a blocker.
- **Switcher**: toolbar workspace button + flyout listing workspaces, entry "Manage workspaces…".
- **Manage dialog** (ContentDialog): workspace list (add/rename/remove) and, for the selected workspace, its root directories (add via folder picker, remove, reorder). First-run empty state: sidebar hint "Create a workspace and add a folder…".
- **Tree** (sidebar): multi-root; root nodes show the directory name with full path tooltip; directories first, natural (numeric-aware) case-insensitive sort; lazy expansion. **Filter**: folders + `*.md` by default; `ShowAllFiles` setting (also a View-menu toggle) reveals everything else; hidden/dot entries (`.git`, `.obsidian`, …) are always excluded. Empty-folder and access-denied nodes render gracefully (icon + tooltip, InfoBar on demand).
- **Watcher**: one `FileSystemWatcher` per root (recursive), events debounced ~300 ms and coalesced, applied incrementally so expansion state is preserved; on watcher overflow/error → full refresh of that root; watchers rebuilt on workspace/root changes.
- **Open**: double-click/Enter on `.md` (or any text file when ShowAllFiles) raises an `OpenDocumentRequest` handled by the document manager (a basic AvaloniaEdit tab until FEATURE-3C44 replaces it); binary/unknown files open with the OS default via `TopLevel.Launcher`.
- **File operations** (PHASE03) via context menu + shortcuts: New File (default `.md`, name validation, opens the new file), New Folder, Rename (F2), Delete → OS trash, Move via drag & drop within/between roots with conflict prompt (Overwrite / Keep both / Cancel).
- **Trash**: Core `ITrashService`; Desktop implementations — Windows: `SHFileOperation` with `FOF_ALLOWUNDO` (P/Invoke); Linux: `gio trash`, falling back to a minimal XDG Trash-spec implementation; if trash is unavailable → ContentDialog-confirmed permanent delete. Operations the app itself initiates are reconciled with watcher events (no double-apply, no flicker).

## Phases

### PHASE01 — Workspace model & management UI — **Status: DONE**

Steps: Core models + `IWorkspaceService` + persistence + unit tests (temp config path) → DI registration → switcher + manage dialog + first-run empty state → last-active workspace reload on startup.

Acceptance criteria:
- Create/rename/delete workspaces and add/remove/reorder roots from the UI; state survives restart; last active workspace reopens.
- Validation enforced (unique non-empty name, ≥ 1 root); missing directory shows a warning but the workspace still opens.
- Core.UnitTests cover the service CRUD + persistence round-trip.

### PHASE02 — File tree, filter & watcher — **Status: DONE**

Steps: tree ViewModels (testable filter/sort logic in Core) → TreeView view with icons → `.md`+folders filter + ShowAllFiles toggle → watcher service with debounce/coalescing → open-into-basic-tab.

Acceptance criteria:
- External create/rename/delete appears in the tree within ~1 s, expansion preserved.
- Filter rules exact (md always; toggle widens; hidden/dot always excluded); toggle persists (setting).
- Double-click opens a `.md` into a tab; unknown types go to the OS default app.
- Unit tests: filter/sort logic; watcher-coalescing logic against a temp directory.

### PHASE03 — File operations — **Status: DONE**

Steps: context menu + command implementations → `ITrashService` (both OS implementations + fallback) → drag & drop move + conflict dialog → op/watcher reconciliation → InfoBar error surfacing.

Acceptance criteria:
- All operations work from the tree; delete lands in the recycle bin/trash on Windows (Linux verified manually — record in the completion doc); fallback confirm-permanent path covered with a test double.
- Conflict prompt honors Overwrite/Keep-both/Cancel; no tree desync after any operation.
- Core.UnitTests: name validation, move/conflict planning logic.

## Dependencies

FEATURE-0F9A complete. PHASE02's basic tab is superseded by FEATURE-3C44 PHASE01.

## Out of scope

Workspace-wide search (FEATURE-40F5) · session restore (FEATURE-40F5) · file copy/duplicate (backlog).
