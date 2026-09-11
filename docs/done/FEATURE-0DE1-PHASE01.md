# FEATURE-0DE1-PHASE01 — Workspace model & management UI

**Status: DONE** · Branch: `feature/feature-0de1-phase01-workspaces` · Plan: `docs/plan/FEATURE-0DE1.md`

## Summary

Workspaces exist: a named, ordered set of root directories, persisted as a hand-editable
`workspaces.json` beside the settings file, switched from a toolbar flyout and edited in a
manage-workspaces dialog. The sidebar stops being a placeholder — it shows the open workspace's
folders (the tree itself lands in PHASE02) or, on a fresh install, how to make one.

Three decisions were taken with the user before building (all three were the recommended option):

1. **One recovery pipeline, not two.** PHASE03's quarantine-then-defaults logic was lifted out of
   `SettingsLoader` into `ConfigRecovery`, which both files now use. A workspaces file that cannot be
   read is moved aside and the editor starts with an empty list rather than throwing on every read of
   it — the same failure mode, and the same fix, as the settings file.
2. **The manage dialog asks everything inline.** It is hosted by the application's single
   `ContentDialog`, so a nested confirmation would replace it rather than stack on it. Removing a
   workspace confirms in a bar inside the dialog; renaming is a box in the details pane.
3. **A workspace is created folder-first.** "New workspace…" opens the folder picker and names the
   workspace after the folder (numbered if that name is taken). That is what makes "at least one root
   directory" an invariant rather than a rule the user can trip over — and "Remove" is disabled on a
   workspace's last folder.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Configuration`

| File | Notes |
|---|---|
| `ConfigRecovery.cs` | The generic load-or-recover, moved here from `SettingsLoader` and made `<T>`. Quarantine name and validator come from the caller; the stamped-fallback and scratch-path behaviour is PHASE03's, unchanged. |
| `ConfigLoadResult.cs` | `SettingsLoadResult` generalized. Same four members; one type now instead of one per file. |

### Created — `src/Enigma.MarkdownEditor.Core/Workspaces`

| File | Notes |
|---|---|
| `Workspace.cs` | `Id` / `Name` / `RootDirectories`, with the per-property rules as attributes. `Clone()` is what makes a failed write rollback-able. |
| `WorkspacesConfig.cs` | `Version` / `Workspaces` / `LastActiveWorkspaceId`. A file from a **newer** version is rejected on purpose — see *Deviations* 2. |
| `WorkspacesConfigValidator.cs` | Only the cross-item rules: unique names, unique identifiers, at least one folder each, no repeated or blank folder. The per-property rules stay on the attributes, which the library evaluates all the way into the list. |
| `WorkspaceValidation.cs` | The same rules phrased for a person: `ValidateName`, `ValidateRootDirectory`, `SuggestName`, `MissingRootDirectories`, plus path normalization and the OS-appropriate path comparer. Pure, so the dialog can ask before it commits. |
| `IWorkspaceService.cs`, `WorkspaceService.cs` | List/create/rename/delete, add/remove/reorder folders, activate, and the two change events. Every mutation writes the file before returning; a failed write restores the previous state and rethrows. |
| `WorkspaceValidationException.cs` | A rule the user broke, with a message written for them. |
| `ActiveWorkspaceChangedEventArgs.cs` | Which workspace is open now, or none. |
| `WorkspacesLoader.cs` | The workspaces file's half of `ConfigRecovery`: which path, which validator, which quarantine name. |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Services/IFolderPicker.cs`, `FolderPicker.cs` | One method over `IFolderDialogService`. See *Deviations* 1 — Avalonia 12 makes the storage interfaces impossible to implement outside Avalonia, so without this seam the dialog's main flow could not be tested at all. |
| `Services/IWorkspaceManagerDialogService.cs`, `WorkspaceManagerDialogService.cs` | Builds the view and its ViewModel per showing and hands them to the `ContentDialog`. |
| `Services/ConfigStartupNotice.cs` | The notice text generalized (internal), so both files say the same three things in their own words. |
| `Services/WorkspacesStartupNotice.cs` | "Workspaces could not be read", "with no workspaces". |
| `ViewModels/WorkspacesViewModel.cs` | The switcher and the sidebar. Rebuilds its lists from the service's events, so the manage dialog does not have to tell it anything. |
| `ViewModels/WorkspaceManagerViewModel.cs` | The dialog: selection, live name validation, the inline removal confirmation, folder add/remove/reorder, and an error bar. Every `await` is wrapped — an `AsyncRelayCommand` parks a faulted task rather than throwing. |
| `ViewModels/WorkspaceListItem.cs`, `WorkspaceRootItem.cs` | Row snapshots, rebuilt rather than kept in step; a row that cannot go stale needs no change notification. |
| `Views/WorkspaceManagerView.axaml(.cs)` | 700 × 420: workspace list, name box, folders list with the four buttons, and the two inline bars. Code-behind is empty. |

### Modified

| File | Change |
|---|---|
| `Core/Settings/SettingsLoader.cs` | Now three lines delegating to `ConfigRecovery`; `SettingsLoadResult.cs` deleted in favour of `ConfigLoadResult<AppSettings>`. |
| `Core/Settings/AppPaths.cs` | `workspaces.json`, `workspaces.invalid.json`, `WorkspacesFile`. |
| `Desktop/Services/SettingsStartupNotice.cs` | Now three lines delegating to `ConfigStartupNotice`. |
| `Desktop/Services/StartupNotice.cs` | `Combine`, because two files can fail at once and the InfoBar shows one message. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | `AddWorkspaces`, `AddStartupNotice` (the notice moved out of `AddAppSettings`, which spans both files now), and the shell's four new registrations. |
| `Desktop/App.axaml.cs` | Loads the workspaces file next to the settings file, combines the two notices, logs a workspaces failure. |
| `Desktop/ViewModels/MainWindowViewModel.cs` | Holds the `WorkspacesViewModel` the toolbar and sidebar bind to. |
| `Desktop/Views/MainWindow.axaml` | Switcher button + flyout, the sidebar's two states (empty-state hint / folders with a missing-folder warning), and the dialog host's size bounds — which are not reset between dialogs, so they belong here rather than in a caller. |
| `Desktop/Views/MainWindow.axaml.cs` | One handler that closes the switcher flyout. See *Deviations* 3 — it is posted, not called, and that detail is the difference between the switcher working and doing nothing. |

### Created / modified — tests (193 total, was 88)

| File | Notes |
|---|---|
| `Core.UnitTests/Workspaces/WorkspaceServiceTests.cs` | 30 tests over a real file: CRUD, normalization, the last-active round trip through a restart, a dangling last-active identifier, a workspace whose folder was deleted, the rollback after a failed write, and the events each operation raises. |
| `Core.UnitTests/Workspaces/WorkspaceValidationTests.cs` | 24 tests: names (blank, too long, taken, its own), folders (blank, duplicate however spelt, missing but accepted), normalization including a drive root, and the numbering `SuggestName` falls back to. |
| `Core.UnitTests/Workspaces/WorkspacesFileTests.cs` | 11 tests on the file itself: round trip with identifiers and order, hand-editability, every rejection (duplicate name, duplicate id, no folder, repeated folder, blank name, newer version), and quarantine-then-clean-write. |
| `Core.UnitTests/Workspaces/TempWorkspacesFolder.cs` | Throw-away folder with real directories to point workspaces at. |
| `Desktop.UnitTests/ViewModels/WorkspaceManagerViewModelTests.cs` | 18 tests against a real workspaces file and a fake picker: create/rename/remove/add/remove-folder/reorder, the inline confirmation's whole life, live name validation, and a missing folder shown rather than dropped. |
| `Desktop.UnitTests/ViewModels/WorkspacesViewModelTests.cs` | 9 tests: both sidebar states, the missing-folder flag, activation, the manage command, both failure paths surfaced through the InfoBar, and a change made elsewhere arriving through the service's events. |
| `Desktop.UnitTests/Services/WorkspacesStartupNoticeTests.cs` | 7 tests, including what the user is told when **both** files fail at once. |
| `Desktop.UnitTests/Fakes/` | `FakeWorkspaceService`, `FakeWorkspaceManagerDialogService`, `FakeFolderPicker`. |
| Existing suites | `SettingsLoader*`/`AppSettings*` tests re-typed to `ConfigLoadResult<AppSettings>`; `MainWindowViewModelTests` and `ServiceCollectionExtensionsTests` extended for the new dependency and registrations. |

## Deviations & follow-ups

1. **`IFolderPicker` is a seam the plan does not mention.** The plan says the dialog picks folders
   through `IFolderDialogService`, and it does — but only behind this one-method interface. Avalonia 12
   marks `IStorageFolder`/`IStorageItem` as not implementable outside Avalonia (the compiler refuses:
   *"This interface or abstract class is -not- implementable by user code"*), and the picker answers in
   those types, so a ViewModel that depends on it directly cannot be tested against a folder the user
   picked. The seam is 20 lines and the shape every caller wanted anyway: a path, or nothing.
2. **A workspaces file from a newer version is rejected, not read as best it can be.** `Version` is
   `[Range(1, CurrentVersion)]`, so a v2 file quarantines instead of loading. This is the opposite of
   the settings file's "an unknown key is ignored so a downgrade is not bricked", and deliberately so:
   an unknown *key* can be ignored safely, but reading a whole newer file and then rewriting it in this
   version's shape would silently drop whatever the newer version added. Quarantine keeps the file.
3. **A real bug, found by driving the app, that no unit test could have caught.** Closing the switcher
   flyout from the row's `Click` handler cancelled the click: `Click` is raised **before** the button
   executes its `Command`, and closing the flyout tears the row out of the tree, clearing the very
   `Command`/`CommandParameter` bindings the button was about to read. Clicking a workspace in the
   switcher did nothing at all — silently. The hide is now posted to the dispatcher so the click
   finishes first. Worth remembering for every future flyout in this app.
4. **The `ContentDialog` host's size bounds are now set in `MainWindow.axaml`** (`360`/`860`/`640`).
   They are not reset between dialogs, so the manage view (700 × 420) needed them raised; the crash and
   settings-failure dialogs auto-size and are only *allowed* to grow wider, not made wider.
5. **The manage dialog does not subscribe to the workspace service.** It is modal and built per
   showing, so nothing else can change the list while it is open — and a handler left on a singleton
   service would outlive the dialog. It refreshes itself after each of its own operations instead.
6. **`AddAppSettings` no longer registers the start-up notice.** Two files can fail at once and the
   InfoBar shows one message, so the notice is composed in the composition root and registered through
   the new `AddStartupNotice`.
7. **No test for the flyout-hide fix.** It is view code that needs a running Avalonia application; the
   suite has no headless UI harness. It was verified by driving the real executable (below), and it is
   the one behaviour here that a regression would not fail a test.
8. **The sidebar's flat folder list is scaffolding.** PHASE02 replaces it with the real tree; the
   missing-folder warning bar above it is not scaffolding and should survive.
9. **Documentation freshness sweep: offered, declined.** The candidates were the README's "under
   construction" block (it still says "no file tree" and describes only the shell, so it now
   understates what works) and a README line naming the per-user folder, which holds a second file as
   of this dev. You chose to skip both, so no prose doc was touched in this commit. The README status
   block is now stale in a way a reader would notice — worth doing before FEATURE-5B28.
10. **Line endings (recommendation only, no action taken).** New files are LF in the working tree and
   `.gitattributes` (`* text=auto eol=lf`) keeps them that way; nothing to do. If you ever want it
   applied explicitly, the git-repo-hygiene command set — **not run here** — is
   `git add --renormalize .`, `git status`, `git commit -m "Normalize all line endings to LF"`.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release -t:Rebuild
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 193   failed: 0   succeeded: 193   skipped: 0
```

193 tests = PHASE03's 88 + 105 added here (65 Core workspaces, 34 Desktop, 6 in the existing suites).
The XAML warning count was read separately (`AVLN*` warnings are not promoted by
`TreatWarningsAsErrors`): zero.

### How the run-time criteria were verified

The real Windows executable was driven end to end — UI Automation to locate and invoke controls, one
coordinate click for the native folder picker's *Select Folder* button (it exposes no invokable
pattern), and `PrintWindow(PW_RENDERFULLCONTENT)` for the screenshots, which are in the session
scratchpad rather than the repo.

| Criterion | Result |
|---|---|
| Create a workspace from the UI | **Met.** "New workspace…" opened the picker titled *Choose the workspace's first folder*; selecting `…\roots\Notes` created a workspace named **Notes** over it, opened it, and wrote `workspaces.json`. |
| Rename from the UI | **Met.** Typing `Docs` enabled the rename button; applying it renamed the workspace, and `"Name": "Docs"` was on disk immediately — the toolbar and the sidebar caption followed in the same frame. |
| Delete from the UI | **Met.** The trash button showed the inline bar *"Remove 'Notes' from the list? The folders themselves are not touched."*; confirming removed it from the list and from the file. Cancel leaves it alone. |
| Add / remove / reorder roots from the UI | **Met.** *Move up* on the second folder reordered the file's `RootDirectories` (Unplugged before Documents). *Remove* is greyed out while a workspace has one folder, and enables on the second. |
| State survives restart; last active reopens | **Met.** Closed the editor after creating **Notes** through the picker and restarted: it came up on Notes, with its folder listed. |
| Validation enforced (unique non-empty name, ≥ 1 root) | **Met.** Typing a name another workspace holds shows *"There is already a workspace called 'Documents'."* under the box and keeps the rename button off. A workspace cannot reach zero folders: the create flow starts from a folder and the last one cannot be removed. |
| A missing directory warns but the workspace still opens | **Met.** A workspace whose second root was deleted opened normally: the switcher row read *3 folders — 1 missing* with a warning icon, the sidebar showed an amber bar naming the folder, and the row itself was listed greyed with a warning icon rather than dropped. |
| Core.UnitTests cover the service CRUD + persistence round-trip | **Met.** `WorkspaceServiceTests` (30) and `WorkspacesFileTests` (11), both against real files. |

The log file recorded no `WARN`, `ERROR` or `FATAL` entry across the whole session. The
`%APPDATA%\Enigma.MarkdownEditor` folder created by this verification was deleted afterwards, so the
profile is back to its pre-run state.
