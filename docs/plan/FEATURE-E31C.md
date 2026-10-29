# FEATURE-E31C — Catch all unhandled exceptions

**Status:** DONE
**Type:** FEATURE · 2 phases · Roadmap: `docs/roadmap.md` · **Branch:** one per phase, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every exception nothing else handled is caught, logged and flushed, and shown to the user in an
error content dialog. This holds from the first line of `Main` until the process exits, including
while the application is starting. The single exception is an exception escaping a background
thread: .NET ends the process right after the event, so that one is logged and flushed but gets no
dialog.

## Context & constraints

- `Services/CrashReporter.cs` (from `FEATURE-0F9A` PHASE03) subscribes three handlers in `Install()`:
  - `AppDomain.UnhandledException`: logs and flushes, no dialog;
  - `TaskScheduler.UnobservedTaskException`: logs, flushes, sets the exception observed, and shows
    "A background operation failed";
  - `Dispatcher.UIThread.UnhandledException`: logs, flushes, sets `Handled`, and shows
    "Something went wrong".
- The dialog is `ShowMessageAsync(title, "<Type>: <message>\n\nThe full details were written to
  <log>")`, guarded by a 0/1 flag.
- **Gap 1: anything before `Install()` is uncaught.** `Install()` runs only after the host is built.
  Before it, a failure in the settings, workspaces or session load, the logging setup, or the host
  build ends the process with nothing logged and nothing shown.
- **Gap 2: the dialog overwrites an open dialog.** `IContentDialogService.ShowAsync` resets the
  shared host before configuring it (see the enigma-avalonia-desktop skill). An error raised while
  Settings or Manage workspaces is open therefore replaces that dialog.
- **Gap 3: the look.** The dialog has no error icon and an `OK` button. Enigma.GitClient's safety net
  (`Services/UnhandledErrorReporter.cs`) shows:
  - `Title = "Something went wrong"`;
  - Phosphor `Warning` (Regular) in `IconBrush = #E8A33D`;
  - `CloseButtonText = "Close"`, `DefaultButton = Close`.
- **Gap 4: a start-up failure shows nothing.** Before the main window exists there is no dialog host,
  so composition that throws leaves the user with nothing on screen.
- The model to follow is GitClient's `Services/UnhandledExceptionHooks.cs`:
  - a static class;
  - `Install()` (AppDomain + TaskScheduler) is the first line of `Main`;
  - `InstallDispatcher()` runs once the platform is set up;
  - `Attach(reporter)` replays a bounded early queue (32 entries);
  - reports made before attachment are written to `Trace`.
- `App.axaml.cs` composes inline once `FEATURE-3185` has removed the splash.
- `CrashReporter` has no tests today.

## Out of scope

- A dialog for `AppDomain.UnhandledException` (the process is already terminating).
- Restart or report-upload features, and a "copy details" button.
- Changing how known failures are reported (load notices, file-operation prompts).

## PHASE01 — Early hooks & uniform error dialog

**Status:** DONE
**Branch:** `feature/feature-e31c-phase01-early-hooks-error-dialog`

### Steps

1. Add `Services/UnhandledExceptionHooks.cs`, a static class:
   - `Install()` hooks `AppDomain` and `TaskScheduler`;
   - `InstallDispatcher()` hooks the UI dispatcher;
   - `Attach(CrashReporter)` and `Detach()`;
   - `Report(Exception, UnhandledErrorSource)`.

   Before `Attach`, reports are queued (at most 32), written to `Trace`, and replayed once attached.
   The handlers keep today's semantics:
   - dispatcher: `Handled = true`;
   - task: `SetObserved()`;
   - AppDomain: no dialog.

   `UnhandledErrorSource` is `AppDomain`, `UnobservedTask` or `Dispatcher`. Idempotent installs.
2. `Program.Main`: call `UnhandledExceptionHooks.Install()` first.
   `App.OnFrameworkInitializationCompleted`: call `UnhandledExceptionHooks.InstallDispatcher()`
   first.
3. `CrashReporter` becomes the sink that `Attach` hands reports to:
   - `Report(exception, source)` logs at Critical for AppDomain and at Error for the others, then
     flushes;
   - for Dispatcher and UnobservedTask it shows the dialog on the UI thread;
   - `Install()` is replaced by `UnhandledExceptionHooks.Attach(crashReporter)` in `App`, at the same
     place;
   - `Detach()` runs at the start of the exit sequence, before the container is disposed.
4. The dialog:
   - GitClient's look: title "Something went wrong", `Warning` glyph, `#E8A33D`, `Close` as the
     default button;
   - the body is today's text: type, message, and the log path;
   - one dialog at a time; errors that arrive while it is open are logged, not queued as further
     dialogs;
   - if the host is open with another dialog, wait for that dialog's `Closed` and then show;
   - a failure to show is logged once, and from then on errors are only logged (no recursion).
5. Tests (Desktop, headless):
   - the hooks: an early report is replayed on `Attach` and the queue is bounded; installs are
     idempotent; nothing is reported after `Detach`;
   - `CrashReporter`, over a fake `IContentDialogService` and a fake flushable logger handle:
     - logs and flushes for each source;
     - shows the dialog with the configured title, icon, brush, Close and default button;
     - shows no dialog for AppDomain;
     - shows a second error after the first dialog is closed, not while it is open;
     - waits while the host reports `IsOpen`;
     - a dialog that throws turns showing off.
   - Hooks are process-global, so their tests reset state through an internal test seam.

### Acceptance criteria

- An exception thrown in `OnFrameworkInitializationCompleted` before the container exists reaches
  `Trace` immediately, and is logged and shown once the reporter attaches (when start-up got that
  far).
- The error dialog matches GitClient's look, never replaces an open dialog, and never loops on its
  own failure.
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green, with
  the new tests included.

## PHASE02 — Start-up failure dialog

**Status:** DONE
**Branch:** `feature/feature-e31c-phase02-startup-failure-dialog`

### Steps

1. Add `Views/StartupErrorWindow.axaml(.cs)`:
   - a small, centred window on `EnigmaBackgroundBrush`, titled with the product name;
   - it carries only a `ContentDialog` host (same bounds as the main window's);
   - on open, it shows the same error dialog as PHASE01 (title, glyph, brush, body, Close);
   - closing the dialog closes the window.
2. `App.OnFrameworkInitializationCompleted`: wrap the composition in `try/catch`. On an exception:
   - report it through `UnhandledExceptionHooks.Report(…, Dispatcher)` so it reaches `Trace`, and
     the log if the reporter attached;
   - flush the log;
   - set `desktop.MainWindow` to a `StartupErrorWindow` showing the exception.

   Closing that window ends the process, because it is the only window (`OnLastWindowClose`). The
   theme variant is applied before it, as before. A failure inside the fallback window itself
   propagates; that path has no host left to try.
3. The dialog body names the log file when logging was set up. Otherwise it says the error could
   not be written to the log.
4. Tests (headless):
   - `StartupErrorWindow` shows a dialog with the error's type and message, the Warning glyph and
     Close;
   - Close closes the window;
   - the start-up `catch` turns a throwing composition step into a `StartupErrorWindow` as the main
     window. Extract the composition behind a seam if needed, without changing behaviour.

### Acceptance criteria

- A composition that throws leaves the user a window with the error dialog, and Close ends the
  process.
- The exception is in `Trace`, and in the log file when logging had started.
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green, with
  the new tests included.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| How far "all unhandled exceptions" goes | From the first line of `Main` through exit, with an early queue | Today's gap is everything before `Install()`. GitClient's design closes it, and the apps stay uniform | Only restyling today's dialog (too narrow) |
| Dialog for AppDomain (terminating) exceptions | None; logged and flushed | .NET ends the process after the event. A blocking dialog on a dying process risks a hang. GitClient made the same call | Blocking the faulting thread on a UI dialog |
| Dialog look | GitClient's: "Something went wrong", Warning glyph in `#E8A33D`, default Close | Uniform Enigma apps | Today's plain `ShowMessageAsync` with OK |
| Error while another dialog is open | Wait for it to close, then show | `ShowAsync` resets the shared host and would destroy that dialog | Replacing it; skipping the dialog |
| Error flood | One dialog at a time; later errors only logged | A fault in a render loop can fire many times a second | Queuing every dialog; GitClient's batching policy (too heavy for one window) |
| Start-up failure | Fallback `StartupErrorWindow` hosting the same dialog | It is the only failure the user otherwise never sees | Log only and exit |
| Split | Two phases | The hooks and dialog stand alone; the fallback window is separately reviewable | One large dev |
