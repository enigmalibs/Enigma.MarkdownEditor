# BUG-7AB2 — Tab, dirty flag, exit & font fixes

**Status: DONE** · 5 phases · Roadmap: `docs/roadmap.md`
Planned on `develop`; each phase cuts its own branch from `HEAD` at build time.

## Objective

Five independent defects reported against **1.1.0**, one per phase. Four are outright bugs; the fifth is a rendering defect whose fix requires a new setting (`PreviewCodeFontSize`), which is therefore built with it rather than split off.

Root causes for phases 01–04 were established by reading the code during the interview and are recorded below. Each phase still opens with a **failing test that reproduces the defect** before anything is changed — a root cause that cannot be reproduced is a hypothesis, not a diagnosis, and any deviation from what is written here goes in that phase's completion doc.

## Context & constraints

- **Two projects, one rule:** `Enigma.MarkdownEditor.Core` never references Avalonia. `AppSettings` lives in `Core`, so anything that needs a `FontFamily`, a `TextDocument` or a dispatcher belongs in `Desktop`.
- **`TreatWarningsAsErrors` is on solution-wide** and `EnforceCodeStyleInBuild` with it. The bar for every phase is a **0-warning** build.
- **Compiled bindings are the default** (`AvaloniaUseCompiledBindingsByDefault`). Every view root and `DataTemplate` carries `x:DataType`, and a binding whose source type does not match the target does **not** fall back to the reflection binding's conversion chain — that is PHASE04's whole story. `AVLN*` diagnostics are emitted by an MSBuild task and are **not** promoted by `TreatWarningsAsErrors`, so `dotnet build -v normal | grep -i AVLN` is part of the check whenever XAML is touched.
- **`vim` is on by default** (`AppSettings.VimEnabled = true`). Any manual verification of the editor happens under vim emulation, because that is what a user actually gets — PHASE02 exists precisely because it was not.
- **The ViewModel-only tests cannot see either of the first two bugs.** `DocumentManagerViewModelTests` drives `DocumentManagerViewModel` with no view attached, so nothing writes back through the tab strip's two-way selection binding; `TabStrip.Edit` edits the `TextDocument` directly, so no vim undo group is ever open. Both phases need tests that go through the real control.
- **The real-view harness already exists** and is the right tool: `tests/…Desktop.UnitTests/Views/TabStrip.cs` shows a real `EditorPaneView` in a real window over real files, with headless pointer input. Extend it rather than building a second harness.
- **Watchers stay.** `DirectoryWatcher`, `DocumentFileWatcher` and their factories are deliberately kept — see PHASE03 for why they are not the cause of the exit hang. Nothing in this item removes or disables them.
- **No dependency moves.** The coupled `Avalonia.*` set stays at 12.1.1, AvaloniaEdit at 12.0.0.

---

## PHASE01 — Closing a tab leaves a live editor

**Status: DONE** · `docs/done/BUG-7AB2-PHASE01.md`

### Symptom

With several tabs open, closing the active one leaves the pane drawn as though a document were open — line-number margin on the left, editor to its right. Clicking or hovering it raises, on the UI thread:

```
System.InvalidOperationException: Document is null
   at AvaloniaEdit.Editing.EmptySelection.StartSelectionOrSetEndpoint(...)
   at AvaloniaEdit.Editing.SelectionMouseHandler.ExtendSelectionToMouse(Point)
   at AvaloniaEdit.Editing.SelectionMouseHandler.TextArea_MouseLeftButtonUp(...)
```

which reaches `Dispatcher.UnhandledException` → `CrashReporter` → the error popup the report mentions. Reported once with a single tab open, not reproducible.

### Root cause

Order of operations in `DocumentManagerViewModel.Remove` (`src/Enigma.MarkdownEditor.Desktop/ViewModels/DocumentManagerViewModel.cs:744-763`):

```csharp
int index = Documents.IndexOf(document);
…
Documents.Remove(document);

if (ActiveDocument == document)      // ← already false by here
{
    ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Min(index, Documents.Count - 1)];
}
```

The tab strip's `ListBox` binds `SelectedItem="{Binding ActiveDocument}"` two-way (`Views/EditorPaneView.axaml:96-98`). Removing the selected item from the bound collection clears the `ListBox`'s selection, which writes `null` straight back into `ActiveDocument` **before** the guard runs. `ActiveDocument == document` is therefore false, the neighbour is never activated, and `ActiveDocument` stays `null`.

The pane then holds a `TextEditor` whose `Document` binding (`ActiveDocument.Document`) has gone null, while its `IsVisible` is keyed on `HasDocuments` — still `true`, because other tabs remain. A visible, hit-testable text area with no document is exactly what AvaloniaEdit throws about.

Two consequences worth naming:

- **The single-tab case is a different path and already correct**: `Documents.Count` reaches 0, `HasDocuments` goes false, the editor is collapsed and the placeholder shows. The one unreproducible sighting is most plausibly a click landing in the frame between the document going null and the layout pass that hides the control; hardening below closes that window too.
- **The existing tests pass** because they never attach a view, so nothing writes `null` into `ActiveDocument` and the guard holds. This is why the bug shipped green.

### Design

1. **Capture activity before mutating the collection.** In `Remove`, take `bool wasActive = ReferenceEquals(ActiveDocument, document);` alongside the existing `index`, before `Documents.Remove(document)`, and branch on `wasActive` afterwards. The neighbour rule itself is unchanged and is the intended one: the tab that took its place, or the one before it when the closed tab was last.
2. **Harden the view so this class of fault cannot render.** Add `HasActiveDocument => ActiveDocument is not null` to `DocumentManagerViewModel`, notified from both `OnActiveDocumentChanged` and `OnDocumentsChanged`. In `EditorPaneView.axaml`, re-key the `TextEditor`'s `IsVisible` to `HasActiveDocument` and the placeholder `TextBlock`'s to `!HasActiveDocument`. The tab strip `Border` keeps `HasDocuments` — a strip with tabs and no selection is still a strip.
3. **Placeholder text is unchanged**, verbatim: `Double-click a markdown file in the tree to open it here.`
4. Nothing else about closing changes: the unsaved-changes prompt, `Cancel` refusing the close, middle-click close, the context menu and `:q` all go through the same `CloseAsync`/`Remove` pair.

### Acceptance criteria

- With three tabs open and the middle one active, closing it activates the tab that took its index; closing the last tab in the strip activates its predecessor.
- With one tab open, closing it shows the placeholder with the exact text above and no editor.
- After either, a pointer press and release over the pane area raises nothing.
- `ActiveDocument` is never left `null` while `Documents` is non-empty.
- Full suite green, 0 warnings, no `AVLN` output.

### Tests

- `Views/EditorTabStripTests` (through `TabStrip`, i.e. a real `ListBox` with the two-way selection binding): closing the active middle tab activates the successor; closing the active last tab activates the predecessor; closing the only tab hides the editor and shows the placeholder; the editor is not visible whenever there is no active document.
- A pointer press/release over the pane after closing the only tab completes without throwing — the direct regression test for the reported stack.
- `ViewModels/DocumentManagerViewModelTests`: `HasActiveDocument` notification fires on activation, on close, and on the last close.

### Files

Modified: `ViewModels/DocumentManagerViewModel.cs` · `Views/EditorPaneView.axaml` · `tests/…/Views/EditorTabStripTests.cs` · `tests/…/Views/TabStrip.cs` (close/visibility helpers) · `tests/…/ViewModels/DocumentManagerViewModelTests.cs`.

---

## PHASE02 — Dirty flag missed in insert mode

**Status: DONE** · `docs/done/BUG-7AB2-PHASE02.md`

### Symptom

Typing in the editor raises no dirty dot on the tab, and closing the tab discards the work without asking. Switching to another tab makes the dot appear.

### Root cause

`DocumentViewModel.IsDirty` is derived from exactly one source (`ViewModels/DocumentViewModel.cs:411-417`):

```csharp
if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
{
    IsDirty = !Document.UndoStack.IsOriginalFile;
}
```

Vim is on by default. Entering insert mode calls `VimEngine.BeginInsertGroup` (`Core/Vim/VimEngine.cs:1101-1109`) → `AvaloniaEditVimBuffer.BeginUndoGroup` (`Views/AvaloniaEditVimBuffer.cs:124-134`) → `UndoStack.StartUndoGroup()`, and the group stays open for the whole insert session. AvalonEdit only recalculates `IsOriginalFile` — and only then raises `PropertyChanged` — when the group depth returns to zero in `EndUndoGroup`. So for the entire time the user is typing, `IsOriginalFile` is stale `true` and `IsDirty` stays `false`: no dot, and `CloseAsync`'s `if (target.HasUnsavedChanges)` is false, so the work goes without a question.

`VimEditorBehavior.OnDocumentChanged` calls `_engine.Reset()`, which ends the insert group — which is exactly why switching tabs makes the flag appear, and why `Esc` does too.

The undo group itself is correct and stays: one `u` undoing a whole insert session is what vim does, and four phases of `FEATURE-11AA` depend on it.

### Design

1. **`TextChanged` may raise the flag; only the undo stack may clear it.** In `DocumentViewModel.OnTextChanged`, set `IsDirty = true` before raising the event. `OnUndoStackPropertyChanged` keeps its current assignment (`IsDirty = !IsOriginalFile`), so undoing back to the saved state still clears the dot the moment the group closes. Document the asymmetry in a comment: the text changing is proof there is something to save, while only the undo stack can prove there is not.
2. **Programmatic replacement must not mark the document dirty.** `ApplyReload` assigns `Document.Text`, which raises `TextChanged`. Guard it with a private `_replacing` flag around the assignment, and set `IsDirty = false` explicitly at the end of `ApplyReload` — after a reload the buffer equals the file by construction, and that must not depend on whether `UndoStack.ClearAll()` / `MarkAsOriginalFile()` happened to raise a change notification. `MarkSaved` needs no guard: it does not touch the text.
3. **Known, accepted limitation** (recorded here and in the completion doc): while a vim insert session is open, editing back to the saved text cannot clear the dot until the group closes on `Esc` or a tab switch. The cost is one stale dot in an edge case; the alternative is breaking insert-session undo.
4. Nothing in `Core/Vim` or the vim behaviour changes.

### Acceptance criteria

- Typing one character in vim **insert** mode, without leaving insert mode, shows the dirty dot on the tab.
- Closing that tab while still in insert mode raises the unsaved-changes prompt, and `Cancel` keeps it open.
- Saving clears the dot; undoing back to the saved state (after leaving insert mode) clears the dot.
- Reloading a document from disk leaves it clean.
- The existing `EditingADocument_MarksItDirtyAndUndoingBackToTheSavedStateClearsIt` still passes unchanged.
- Autosave still arms on the first edit of a clean document and does not fire behind an open prompt.
- Full suite green, 0 warnings.

### Tests

- `ViewModels/DocumentViewModelTests` (or the existing `DocumentManagerViewModelTests`): an edit made **inside an explicitly opened undo group** (`Document.UndoStack.StartUndoGroup()`) sets `IsDirty` while the group is still open — the exact mechanism, with no vim harness needed.
- Closing a document edited inside an open undo group prompts about unsaved changes.
- `ApplyReload` leaves `IsDirty` false and `HasExternalChange` false.
- A real-input test through the pane: with vim enabled, send the keystrokes for `i` then a character and assert the tab's dot is visible without an `Esc`. If the `TabStrip` harness cannot drive text input into the `TextArea`, record that in the completion doc and keep the undo-group test as the automated proof, with the vim path verified by hand.

### Files

Modified: `ViewModels/DocumentViewModel.cs` · `tests/…/ViewModels/DocumentManagerViewModelTests.cs` · `tests/…/Views/EditorTabStripTests.cs` (+ `TabStrip.cs` if input helpers are added).

---

## PHASE03 — Process hangs on exit

**Status: DONE** · `docs/done/BUG-7AB2-PHASE03.md`

### Symptom

Closing the window leaves the process running for ever.

### Root cause

`App.axaml.cs:205-219`, the `desktop.Exit` handler, runs on the UI thread and blocks it:

```csharp
services.GetRequiredService<ISessionService>().FlushAsync().GetAwaiter().GetResult();
host.StopAsync().GetAwaiter().GetResult();
host.Dispose();
```

`SessionService.FlushAsync` awaits `WriteAsync`, which awaits `_config.WriteAsync(cancellationToken)` — a real asynchronous file write — with no `ConfigureAwait(false)` (`Services/SessionService.cs:149-160, 194-212`). The await captures Avalonia's UI `SynchronizationContext`, so the continuation is posted back to the UI thread that is blocked inside `GetResult()` waiting for it. Textbook sync-over-async deadlock, on a foreground thread, so the process cannot exit. `host.StopAsync()` on the next line is the same shape.

**The watchers are not the cause**, and this is why they are kept: `FileSystemWatcher` raises its events on threadpool/IOCP threads and `System.Threading.Timer` callbacks likewise; neither creates a foreground thread, so neither can hold a .NET process open. Removing them would very likely have left the hang exactly as reported.

### Design

1. **Extract the sequence so it is testable.** New `Services/AppShutdown.cs`:

   ```csharp
   internal static bool Run(Func<CancellationToken, Task> work, TimeSpan timeout)
   ```

   It starts `work` with `Task.Run` — so its continuations resume on the thread pool instead of being posted to the UI thread this method blocks — waits on it for at most `timeout`, and returns whether it finished. Cancellation is linked to the same timeout via a `CancellationTokenSource`, so a cooperative write is asked to stop before the wait gives up. Work abandoned by a timeout is left on a threadpool (background) thread, which cannot keep the process alive.
2. **`App` passes one lambda** doing the whole sequence: `await session.FlushAsync(ct)` → `await host.StopAsync(ct)` → `host.Dispose()`, each `ConfigureAwait(false)`.
3. **Timeout: 5 seconds**, as a named constant on `AppShutdown` with the reasoning in its XML doc.
4. **On timeout, say so and get the record onto the disk.** Log a warning naming the timeout, then flush synchronously through `IFlushableLoggerHandle.Flush()` — the same handle `CrashReporter` uses for exactly this reason — because the `host.Dispose()` that normally flushes NLog is part of the work that was abandoned.
5. **Secondary hygiene, same phase:** `SessionService.WriteAsync`/`FlushAsync` get `ConfigureAwait(false)`. It is not sufficient on its own (it cannot govern what `Enigma.Configuration` captures internally, which is why step 1 carries the fix) but a service method with no UI affinity should not be capturing a UI context.
6. Nothing about `ISessionService`'s public surface, the session file format, or the window's `OnClosing` placement recording changes.

### Acceptance criteria

- Launching the app and closing the window ends the process — verified by hand on Windows with `Get-Process` after the window disappears, and recorded in the completion doc.
- `session.json` is still written on a normal exit (placement, panes and open documents restored on the next start).
- A shutdown step that never completes does not hang the application: it exits after the timeout with a warning in the log file.
- `AppShutdown.Run` returns when called from a blocked UI thread with work that needs a free thread to complete.
- Full suite green, 0 warnings.

### Tests

- `Services/AppShutdownTests` under `UiThread.RunAsync` (which blocks the headless UI thread): `Run` completes for work that awaits a real asynchronous operation — this test fails against the current inline `GetAwaiter().GetResult()` shape and passes against `Task.Run`, so it is the regression test for the deadlock itself.
- `Run` returns `false`, and returns promptly, for work that never completes.
- `Run` returns `true` and propagates nothing for work that completes immediately.
- The work's `CancellationToken` is signalled when the timeout elapses.
- `Services/SessionServiceTests`: `FlushAsync` still writes the file (guard against the `ConfigureAwait` change altering behaviour).

### Files

Created: `Services/AppShutdown.cs` · `tests/…/Services/AppShutdownTests.cs`.
Modified: `App.axaml.cs` · `Services/SessionService.cs` · `tests/…/Services/SessionServiceTests.cs`.

---

## PHASE04 — Editor font family never applied

**Status: DONE** · `docs/done/BUG-7AB2-PHASE04.md`

### Symptom

Choosing a font family for the editor in Settings has no effect, including after a restart. Editor font **size** works; both preview font families work.

### Root cause (to confirm first)

`Views/EditorPaneView.axaml:194-195`:

```xml
FontFamily="{Binding FontFamily}"
FontSize="{Binding FontSize}"
```

`DocumentManagerViewModel.FontFamily` is a `string` (`:207`); the target is `Avalonia.Media.FontFamily`. Compiled bindings are on solution-wide, and a compiled binding does not run the reflection binding's implicit type-conversion chain — so the value never reaches the control and the property keeps its inherited default. The line below works because `int` → `double` is a numeric conversion compiled bindings do handle, which matches the reported behaviour exactly: size works, family does not. The preview's two families work because `MarkdownVisualBuilder` assigns them in C# (`new FontFamily(family)`), never through a binding.

The phase **opens by confirming this** with the failing test below, since a binding failure of this kind surfaces only as an Avalonia binding error on the trace log. If the confirmed cause turns out to be different — AvaloniaEdit's own Fluent control theme assigning a family inside the `TextEditor` template would be the other candidate — the fix moves to assigning `Editor.FontFamily` from the pane's code-behind alongside `EditorAppearance`, and the deviation is recorded in the completion doc.

### Design

1. **Give the ViewModel the type the target wants.** `DocumentManagerViewModel.FontFamily` changes from `string` to `Avalonia.Media.FontFamily`, built as `new FontFamily(_fonts.ResolveEditorFamily(_settings.Value.EditorFontFamily))`. The resolution itself, the fallback rules and `IFontCatalog` are unchanged, as is what the settings file holds — an uninstalled stored family still renders with the fallback and stays in the file verbatim.
2. **No converter class.** A one-line type change beats a converter nobody would find, and `Desktop` already references Avalonia everywhere.
3. XAML is untouched: the existing binding type-matches once the property does.
4. The three existing assertions that read the family as a string (`DocumentManagerViewModelTests` around `:594`, `:613`, `:630`) read `.Name` instead.
5. **Sweep for the same class of defect** while the cause is fresh: grep the `.axaml` files for compiled bindings whose source is a `string` and whose target is a non-string Avalonia type. Anything found that is out of this phase's scope is a recommendation in the completion doc, not a change.

### Acceptance criteria

- Choosing an installed monospace family in Settings changes the editor's font as soon as the settings window closes, and again after a restart.
- "Automatic" resolves to the best installed monospace family and renders with it.
- A stored family this machine does not have renders with the fallback and is left in the settings file unchanged.
- `Editor.FontFamily.Name` equals the resolved family after `OnSettingsChanged`.
- Full suite green, 0 warnings, no `AVLN` output.

### Tests

- A real-view test through `TabStrip`/`EditorPaneView`: with a family set in the settings, the shown editor's `FontFamily.Name` is that family; changing the setting and calling `OnSettingsChanged` moves it. This is the test that fails before the fix.
- `DocumentManagerViewModelTests`: automatic resolution, an uninstalled family falling back, and the settings file left unchanged — the existing three cases, retyped.
- Manual: confirm by eye that the editor renders in the chosen family, under vim, in both theme variants.

### Files

Modified: `ViewModels/DocumentManagerViewModel.cs` · `tests/…/ViewModels/DocumentManagerViewModelTests.cs` · `tests/…/Views/EditorAppearanceTests.cs` or `EditorTabStripTests.cs` (whichever the harness fits) · possibly `Views/EditorPaneView.axaml` / `EditorPaneView.axaml.cs` if the confirmed cause is the template rather than the binding.

---

## PHASE05 — Preview code font size & weight

**Status: DONE** · `docs/done/BUG-7AB2-PHASE05.md`

### Symptom

Code in the preview is too big, and looks bold.

### Root cause

- **Size.** `MarkdownPreviewTypography.CodeFontSize => BodyFontSize - 1` (`Controls/MarkdownPreviewTypography.cs:61`) — 17pt against the 18pt `PreviewFontSize` default. Worse for inline spans: `MarkdownVisualBuilder.Code(CodeInline)` deliberately sets only the family and lets the size inherit, so `` `code` `` inside a sentence renders at the full 18pt — the largest code in the preview.
- **Weight.** `MarkdownCodeBlock.cs:203-205` applies `FontWeight.Bold` whenever the TextMate token says so. The Dark+/Light+ themes mark some scopes bold, so fenced-block keywords come out heavy — and heavier than the same text in the editor, which paints tokens through a different path.

### Design

1. **New setting.** `AppSettings.PreviewCodeFontSize` — `int`, `[Range(6, 72)]`, default **14**. Absolute points, consistent with `EditorFontSize` and `PreviewFontSize`, so the settings file stays hand-editable in the obvious way and the label means what it says. Its XML doc states why it is absolute rather than derived from prose.
2. **`MarkdownPreviewTypography` gains `CodeFontSize` as a record parameter**, replacing the computed member: `(string FontFamily, string CodeFontFamily, double BodyFontSize, double CodeFontSize, double LineHeight)`. `Default` passes `CodeFontSize: 13`, one under its 14pt body, so a preview built by hand (a designer, a test) looks exactly as it did before the setting existed. `AsideFontSize` stays derived from prose — an aside is prose, not code.
3. **`MarkdownPreviewViewModel.Typography`** passes `_settings.Value.PreviewCodeFontSize` through. `OnSettingsChanged` already re-raises `Typography`, so the change reaches an open preview with no new plumbing.
4. **Inline spans obey it too.** `MarkdownVisualBuilder.Code(CodeInline)` sets `run.FontSize = _type.CodeFontSize` alongside the family, and the comment that explained the old exception is replaced by one explaining the new rule: with an explicit size the point of the setting is that all code honours it.
5. **Bold goes.** Drop the `if (token.IsBold)` branch in `MarkdownCodeBlock`. Colour, italic and underline are kept. `MarkdownCodeToken.IsBold` and the highlighter that fills it stay as they are — the token is a faithful reading of the theme — with a comment at the render site saying the weight is deliberately not applied in a read-only preview, where colour already carries the syntax.
6. **Settings window.** A `SettingsCard` "Code font size" with an `IntEditor` (`Width="110" Unit="pt"`), placed directly after the existing "Code font" card in the Preview group so the two code rows sit together; `SettingsWindowViewModel.PreviewCodeFontSize` (`int?`) mirrors `PreviewFontSize` exactly.

### Acceptance criteria

- A fenced block and an inline code span both render at `PreviewCodeFontSize`; changing it in Settings changes both without a restart.
- Default is 14, and an existing `settings.json` with no `PreviewCodeFontSize` key loads at 14 with no error and no quarantine.
- 5 and 73 are rejected by validation; 6 and 72 are accepted.
- No run inside a fenced block is bold, in either theme variant; token colours are unchanged.
- Settings shows the row, and it round-trips through the file.
- Full suite green, 0 warnings, no `AVLN` output.

### Tests

- `Core.UnitTests/Settings`: default is 14; `AppSettingsValidatorTests` for the range ends and both rejections; `AppSettingsRoundTripTests` contains the key and defaults it from a partial file (with the round-trip value set off 14 so it is not asserting the default against itself).
- `Desktop.UnitTests/Controls/MarkdownPreviewTypographyTests`: `CodeFontSize` is carried, not derived; `Default` reports 13; `LineHeightAt` unchanged.
- `MarkdownCodeBlockTests`: a token the theme marks bold renders at `FontWeight.Normal` and keeps its foreground.
- `MarkdownPreviewTests`: an inline code span's `FontSize` equals the typography's code size and differs from the paragraph's; a fenced block's does too.
- `ViewModels/SettingsWindowViewModelTests`: the property reads and writes the setting, and a null assignment does not overwrite it.
- `Views/SettingsPreviewSectionTests`: the new row is present.
- Manual: confirm by eye that code no longer reads heavier than the prose around it, light and dark.

### Files

Modified: `Core/Settings/AppSettings.cs` · `Core/Settings/AppSettingsValidator.cs` (if the range needs a cross-property rule; otherwise unchanged) · `Controls/MarkdownPreviewTypography.cs` · `Controls/MarkdownVisualBuilder.cs` · `Controls/MarkdownCodeBlock.cs` · `ViewModels/MarkdownPreviewViewModel.cs` · `ViewModels/SettingsWindowViewModel.cs` · `Views/SettingsWindow.axaml` · `README.md` (settings list, via the freshness sweep) · the test files above.

---

## Dependencies

The five phases are independent and may be built in any order; PHASE01 → PHASE05 is the recommended order (the two crash/data-loss defects first). `FEATURE-2760` (Release 1.2.0) must be built **after all five**, because it documents them.

## Out of scope

Removing or gating the file/directory watchers · the `SelectableTextBlock.path` comma-separated font stack still in `SettingsWindow.axaml` (a style setter, not a binding — cosmetic, recommendation only; carried over from `FEATURE-E340-PHASE01` follow-up 6) · making the preview's bold-in-code a setting · a synchronous write API on `IConfig<T>` · any dependency version bump · README screenshots · building any artifact.
