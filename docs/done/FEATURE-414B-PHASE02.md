# FEATURE-414B-PHASE02 — Settings as a content dialog

**Status: DONE** · Phase 02 of 2 · Branch: `feature/feature-414b-phase02-settings-dialog`
Plan: `docs/plan/FEATURE-414B.md`

## Summary

Settings was the last real `Window` in an application whose every other dialog — About, Manage
workspaces, First run — is a `ContentDialog` raised on the single host control in `MainWindow`. It is
one too now, and the names stop saying "window". Nothing a setting *does* changed: no `AppSettings`
property was renamed, no value migrated, nothing new persisted, no group or row added, removed or
reordered.

**The view is a `UserControl`.** `Views/SettingsWindow.axaml` → `Views/SettingsView.axaml`, root
`<UserControl>`, `Width="760"`, `Window.Styles` → `UserControl.Styles`. The window-only properties are
gone — `Title`, `Icon`, `Height`, `MinWidth`, `MinHeight`, `ShowInTaskbar`, `WindowStartupLocation`,
`Background` — the last of those because the dialog card paints its own ground.

**The footer is gone with it.** The bottom `Border` and its `Close` `Button` were row 1 of a two-row
`Grid`; the dialog's own `CloseButtonText = "Close"` replaces them, the `Grid` collapses, and
`OnCloseClick` goes from the code-behind, which is now genuinely nothing but a constructor.

**The inner `ScrollViewer` went too — see *Deviations*.** The plan kept it unless the host's own
template turned out to wrap content in one. It does, so it went.

**The service is a dialog service**, shaped like `AboutDialogService`: `Func<SettingsViewModel>` plus
`IContentDialogService`, the view newed up per showing, a `Gear` glyph resolved once into a
`static readonly Geometry` because `{ei:IconGeometry}` is markup-only. `Func<SettingsWindow>` and
`Func<Window> getOwner` are dropped — there is no window and no owner to resolve — and with them the
`AddTransient<SettingsWindow>()` registration, since a `UserControl` is built by the service rather
than pulled from the container.

**The save tail is preserved exactly.** `SaveAsync` still runs *after* the dialog has closed, and a
failure is still reported with `ShowMessageAsync("Settings were not saved", failure)` as a second,
sequential dialog. Only the reason in the comment changed: not "a modal child covers the main window"
but "the host is a single control, so a message raised while the settings dialog is up would replace
it rather than appear over it". Dismissal by Escape or a scrim click resolves as `DialogResult.None`,
and the result is deliberately not inspected — every edit is applied to the live `AppSettings` as it
is made, so every exit means the same thing.

## Files/modules touched

**Renamed + rewritten — `src/Enigma.MarkdownEditor.Desktop`**

- `Views/SettingsWindow.axaml` → `Views/SettingsView.axaml` — root element, styles element, the
  two-row `Grid` and its footer, the inner `ScrollViewer`. Every `SettingsCardExpander`,
  `SettingsCard`, control, icon, binding and row order is what it was, dedented two levels by the loss
  of the `Grid` and the scroller. Three comments that said "when this window closes" say "when this
  dialog closes".
- `Views/SettingsWindow.axaml.cs` → `Views/SettingsView.axaml.cs` — `Window` → `UserControl`,
  `OnCloseClick` deleted, the `Avalonia.Interactivity` using with it.
- `Services/ISettingsWindowService.cs` → `Services/ISettingsDialogService.cs` — renamed; the summary
  no longer promises a window.
- `Services/SettingsWindowService.cs` → `Services/SettingsDialogService.cs` — two constructor
  arguments instead of four, `ShowDialog` replaced by `IContentDialogService.ShowAsync`, the icon
  added, the save tail unchanged in behaviour and re-explained.
- `ViewModels/SettingsWindowViewModel.cs` → `ViewModels/SettingsViewModel.cs` — rename only, plus the
  four doc comments that named a window. No property, no logic, no `SaveAsync` change.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Extensions/ServiceCollectionExtensions.cs` — `AddTransient<SettingsWindow>()` removed;
  `ISettingsDialogService` built from two dependencies instead of four; the `AddShell` remarks now
  explain the transient ViewModel by what it reads rather than by a `Window` that cannot reopen.
- `ViewModels/MainWindowViewModel.cs` — `_settingsWindowService` → `_settingsDialogService` and the
  parameter with it. Three doc comments that justified themselves by "a dialog raised from behind the
  modal settings window would render behind it" now give the reason that is actually true — the host
  is a single control, so such a dialog would *replace* the settings dialog. Log message and inline
  comments follow.
- `ViewModels/FirstRunViewModel.cs`, `Services/FirstRunDialogService.cs` — cross-references to the
  renamed types and one doc comment each.

**Renamed — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Fakes/FakeSettingsWindowService.cs` → `Fakes/FakeSettingsDialogService.cs` — rename plus a doc
  comment that no longer says the real service builds a `Window`.
- `ViewModels/SettingsWindowViewModelTests.cs` → `ViewModels/SettingsViewModelTests.cs` — type name
  only; all cases untouched and still green, which is the point: the ViewModel did not change.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/SettingsPreviewSectionTests.cs` — shows a `SettingsView` inside a plain `Window`, the way
  `FirstRunViewTests` and `AboutViewTests` already show theirs. `Groups`/`Rows` take the view. The
  assertions are unchanged, so this remains the test that pins "every group and row is present and
  still edits its setting" — now through the `UserControl`.
- `Extensions/ServiceCollectionExtensionsTests.cs` — the `SettingsWindow` transient row is gone from
  the lifetime theory (the view is no longer a container-resolved type), and its comment now explains
  the ViewModel's transience by what it reads.
- `ViewModels/MainWindowViewModelTests.cs` — the `settingsWindow` local and `Create` parameter become
  `settingsDialog`; two test names say "dialog".
- `ViewModels/DocumentManagerViewModelTests.cs`, `ViewModels/FileTreeViewModelTests.cs`,
  `ViewModels/FirstRunViewModelTests.cs` — one test name each.

**Workflow artifacts**

- `docs/roadmap.md` — PHASE02 `TODO` → `IN PROGRESS` → `DONE`, and `FEATURE-414B` itself to `DONE`,
  its second and last phase being complete.
- `docs/plan/FEATURE-414B.md` — the same two statuses.

## Deviations & follow-ups

- **The inner `ScrollViewer` was removed, and this was measured rather than assumed.** The plan made
  it conditional: keep it, unless the host's template wraps content in a scroller of its own. A
  throwaway headless probe — a `ContentDialog` with the host's real `DialogMaxHeight="640"` and a
  2000px-tall child, shown and laid out — found **exactly one** `ScrollViewer` in the dialog's visual
  tree, viewport `700 × 495`, extent `700 × 2000`, sitting below the title and above the buttons. The
  host scrolls its content; it does not clip it. So the view is now the `StackPanel` directly, with a
  comment saying why there is no scroller in a 240-line settings list. The probe was deleted once it
  had answered the question. Nesting the two would not have been *broken* — the outer would have
  scrolled and the inner sat idle — which is why the plan left it a preference rather than a risk.
- **Comments outside the plan's file list still say "settings window".** Seven files carry it in
  prose about *when* settings closes rather than as a type reference:
  `Services/IVimSession.cs` (2), `Services/VimSession.cs`, `ViewModels/DocumentManagerViewModel.cs`,
  `ViewModels/FileTreeViewModel.cs`, `ViewModels/MarkdownPreviewViewModel.cs` (4),
  `Views/FirstRunView.axaml` (2) and `Views/MainWindow.axaml` (2). None is a dangling `<see cref>` —
  they all still read correctly as informal English — and the plan's *Files* section enumerates which
  files take doc-comment updates and does not include them. Left alone on scope discipline, recorded
  here as a **follow-up**: a one-word sweep, worth doing on its own rather than hidden in a rename.
- **No test covers `SettingsDialogService` itself**, and none covered `SettingsWindowService` before
  it. All four dialog services in the application are faked at their interface in ViewModel tests and
  never exercised against a real `ContentDialog` host, so the runtime acceptance criteria below are
  manual readings — as About, Manage workspaces and First run were when they shipped. Writing that
  first real dialog-service test would have been a new convention rather than this phase's work, so it
  is a **follow-up**, not a deviation: it is genuinely feasible headlessly (the probe above proves the
  host can be built and opened in an `[AvaloniaFact]`), and it would pin the title, the single Close
  button, the view as content, and the save-after-close ordering for all four services at once.
- **Not verified by eye.** No screenshot and no run of the application: the criteria that need one —
  that the gear button raises a dialog rather than a window, that the theme still switches live while
  it is open, and that the content scrolls when it does not fit — are a human reading. Doing so means
  backing up and restoring the real `%APPDATA%\Enigma.MarkdownEditor`, as recorded for
  `BUG-7AB2-PHASE05`. Everything statically verifiable *is* verified, below.
- **One presentational unknown handed to that reading.** `Width="760"` is the plan's number, chosen
  against `WorkspaceManagerView`'s 700 and inside the host's `DialogMaxWidth="860"`; the old window
  was 780 wide with a 560 minimum. The settings list is the tallest content the shared host will ever
  be asked to show, and the host's `DialogMaxHeight="640"` was deliberately **not** raised — per the
  plan, since that ceiling is shared with every other dialog. So the list will scroll, by design;
  whether 640 is a comfortable window onto it is the reading.
- **The plan's PHASE01 section still names `Views/SettingsWindow.axaml`.** Left as-is, for the reason
  PHASE01's own completion doc gives: a plan records what each phase was, not a live index.
- **The prose docs needed nothing.** `README.md` never describes settings as a window, never quotes
  the Close button and never mentions the gear button — it says "in settings", which is still true —
  and `RELEASENOTES.md` describes what 1.2.0 shipped, so this rename belongs to the next release's
  notes rather than to a released document. There is no `CLAUDE.md`/`AGENTS.md` in the repo. Raised in
  the documentation freshness sweep with both candidates offered, and skipped there.
- **No line-ending noise.** Every touched file is LF, as `.gitattributes` (`* text=auto eol=lf`)
  requires, and the renames are recorded as renames rather than delete-plus-add. Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -v normal`: **succeeded, 0 errors, 0 warnings**, and no
  `AVLN*` diagnostic anywhere in the normal-verbosity log. XAML was touched — the root element
  changed type — so the `AVLN` sweep is the real check here and not a formality.
- `dotnet test Enigma.MarkdownEditor.slnx`: **1967 passed, 0 failed, 0 skipped**
  (`Enigma.MarkdownEditor.Core.UnitTests` + `Enigma.MarkdownEditor.Desktop.UnitTests`).
- **1968 → 1967 is one removed `InlineData`, not a lost test.** The lifetime theory no longer has a
  `typeof(SettingsWindow)` case, because the type no longer exists and the view is no longer resolved
  from the container. Every other case is the same case.
- **Compiled bindings did the work the plan expected of them.** `x:DataType` plus
  `AvaloniaUseCompiledBindingsByDefault` mean a missed rename is a build error, not a silent runtime
  one, so "no reference left behind" is settled by the compiler rather than by grep — which is exactly
  what made the wholesale rename safe. The four errors the first build produced were all in the test
  project and all of them real. A grep for `SettingsWindow` and `settingsWindow` across `src` and
  `tests` now returns nothing.
- **Acceptance criteria, one by one.**
  - The gear button opens settings as a `ContentDialog` over the main window — no second OS window, no
    taskbar entry — titled "Settings", with a single "Close" button ✓ *by construction, pending the
    manual reading.* `SettingsDialogService.ShowAsync` sets `Title = "Settings"` and
    `CloseButtonText = "Close"` and sets no other button; `SettingsView` is a `UserControl`, so there
    is no window to place, own or list; `grep -rn "ShowDialog" src` returns nothing, and the only
    `Window` subclasses left in `Views` are `MainWindow` and `SplashWindow`.
  - Every group and row is present and still edits its setting ✓ —
    `SettingsPreviewSectionTests.ThePreviewGroup_SitsBesideTheEditorOne` asserts the five groups in
    order and `ThePreviewGroup_HasARowPerSetting` the five Preview rows, both through the real view;
    `EachRow_IsWiredToItsOwnSetting` asserts each row's control is the same control bound to the same
    property. All three pass against a `SettingsView` where they used to pass against a
    `SettingsWindow`, which is the whole claim of this phase.
  - The theme still switches live while the dialog is open, and the content scrolls when it does not
    fit ✓ *pending the manual reading.* The theme path is untouched — `SettingsViewModel` writes the
    live `AppSettings` and calls `IThemeService` exactly as before, and `SettingsViewModelTests`
    (renamed, otherwise unchanged) still covers it. The scrolling is now the host's, measured by the
    probe recorded above.
  - Closing by the button, by Escape and by clicking the scrim all persist the settings ✓ — all three
    resolve the one `await _dialogService.ShowAsync(...)`, and `SaveAsync` is the next statement after
    it on every path; the result is not branched on, so there is no path that skips the save.
  - A failed save still reports "Settings were not saved" in a dialog of its own afterwards ✓ — the
    `if (failure is not null)` block is unchanged and still sequenced after the first dialog has
    closed.
  - No type named `SettingsWindow*` remains, and nothing in `Desktop` resolves a `Window` to show
    settings ✓ — as above: the grep is empty, `Func<Window>` appears nowhere in `src`, and the
    `MainWindow` dependency the old service took is gone from the registration.
  - Build clean with zero warnings, no `AVLN`, whole suite passes ✓ — as above.
