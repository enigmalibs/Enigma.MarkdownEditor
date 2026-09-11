# BUG-7AB2-PHASE03 — Process hangs on exit

**Status: DONE** · Phase 03 of 5 · Branch: `bugfix/bug-7ab2-phase03-exit-hang`
Plan: `docs/plan/BUG-7AB2.md`

## Summary

Closing the window left the process running for ever. The cause is the one the plan named, and it was
reproduced end to end before anything was changed: the `desktop.Exit` handler ran on the UI thread and
blocked it on `FlushAsync().GetAwaiter().GetResult()`, while the continuation inside `FlushAsync`
captured Avalonia's synchronization context and was posted straight back to that blocked thread.
Textbook sync-over-async, on a foreground thread, so the process could not exit.

The fix has two halves, and measuring them separately turned out to matter (see the deviation below).

`Services/AppShutdown.cs` is the structural half. `Run(work, timeout)` starts the sequence with
`Task.Run` — so it begins with no synchronization context to capture and its continuations resume on
the thread pool rather than on the caller — waits for it with a bound, and reports whether it
finished. Work abandoned on a timeout is left on a pool thread, which is a background thread and
cannot hold the process open. `App` passes one lambda doing the whole sequence
(`FlushAsync` → `StopAsync` → `Dispose`, each `ConfigureAwait(false)`), and on a timeout logs a
warning and flushes it through `IFlushableLoggerHandle` — because the `host.Dispose()` that normally
flushes NLog is part of the work that was just abandoned, and a hang that leaves no record is the one
nobody can diagnose.

`SessionService`'s write continuations are the second half: they now use `ConfigureAwait(false)`,
which the class remarks explain by contrast with `WorkspaceService`, whose opposite choice is equally
deliberate. Nothing here resumes onto the user interface — the only thing after the await is a log
line — so capturing the caller's context bought nothing and, on the way out, cost everything.

The watchers were not touched, as planned. They were never the cause: `FileSystemWatcher` and
`System.Threading.Timer` raise their callbacks on threadpool/IOCP threads, and no background thread
can hold a .NET process open.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Desktop`**

- `Services/AppShutdown.cs` — `DefaultTimeout` (5 s, with the reasoning in its XML doc) and
  `Run(Func<CancellationToken, Task>, TimeSpan)`.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `App.axaml.cs` — the `desktop.Exit` handler runs the sequence through `AppShutdown.Run` and reports a
  timeout; `ISessionService` and `IFlushableLoggerHandle` are resolved *before* the sequence starts, so
  that a timeout cannot have the handler asking a container for a service while the work it walked away
  from is disposing it. One `using` added (`Enigma.Logging.Abstractions`).
- `Services/SessionService.cs` — `ConfigureAwait(false)` on the two write awaits, plus a `<para>` in the
  class remarks saying why this service makes the opposite choice to `WorkspaceService`.

**Created — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Services/AppShutdownTests.cs` — **6 new tests**: work that awaits finishes even though the thread
  that started it is blocked (the regression test, under `UiThread`); work that finishes immediately is
  reported done and throws nothing; work that never finishes is abandoned rather than waited for; work
  that overruns is asked to stop; work that throws surfaces the exception it actually threw; a null
  sequence is refused.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Services/SessionServiceTests.cs` — **1 new test**: a flush handed an already-cancelled token stops
  quietly rather than throwing at the exit handler. New surface, because `FlushAsync` was previously
  only ever called with `default`.

Nothing was added to `Enigma.MarkdownEditor.Core` or its test project. No new dependency, no settings
change, no change to `ISessionService`'s public surface, the session file format, or the window's
`OnClosing` placement recording.

## Deviations & follow-ups

- **`AppShutdown` is `public static`, not `internal` as the plan wrote.** There is no
  `InternalsVisibleTo` anywhere in the solution, so an internal type cannot be tested — and the house
  convention for a tested static helper is already public (`SplashTiming`, `SessionStartupNotice`).
  One-word change, no design consequence.
- **The plan's step 5 is not the secondary hygiene it was billed as — it is what unblocked the flush.**
  The plan says `ConfigureAwait(false)` on `SessionService` "is not sufficient on its own (it cannot
  govern what `Enigma.Configuration` captures internally)". Measured on the real application, it is
  sufficient for this call site: with the old inline exit handler and *only* that change, three of
  three runs exited cleanly, which means `Enigma.Configuration` does not capture a context of its own.
  The capture that deadlocked was `SessionService`'s. `AppShutdown` still earns its place and stays:
  `host.StopAsync().GetAwaiter().GetResult()` is the same wrong shape and survives only because
  `Microsoft.Extensions.Hosting` happens to use `ConfigureAwait(false)` internally, so without the
  structural fix the hang is one dependency update away from coming back — and only `AppShutdown`
  supplies the timeout the acceptance criteria ask for.
- **Cancellation is raised when the wait gives up, not by a timer on the source.** The plan described a
  `CancellationTokenSource` linked to the same timeout. A source constructed with a timeout races the
  wait — both fire at the same instant off different clocks — so `Run` waits, and then calls `Cancel()`
  itself. Same moment, deterministically ordered, and it makes "the work's token is signalled when the
  timeout elapses" a test that cannot flake.
- **`Run` waits on the task's wait handle rather than calling `Task.Wait`.** Two reasons, both in the
  code: `Wait` is permitted to execute the task inline on the calling thread, which would put the work
  straight back onto the context the method exists to keep it off; and a step that faulted then reaches
  the crash handler as the exception it actually threw rather than wrapped in an `AggregateException`.
- **The timeout source is deliberately left undisposed on the abandoned path.** The work walked away
  from still holds its token, and there is no timer or wait handle behind it, so disposing would buy
  nothing and risk faulting a thread nobody is watching.
- **No new `SessionServiceTests` case for "`FlushAsync` still writes the file".** The plan asks for one;
  `Flush_WritesNowAndDropsThePendingTimer` already is exactly that test, and it passes unchanged. The
  cancelled-token case was added instead, because that is the behaviour this phase newly depends on.
- **The timeout branch is covered by unit tests only.** Making the real application overrun five
  seconds on the way out would mean shipping a fault to provoke it. `AppShutdownTests` covers the
  return value, the promptness and the cancellation; the log warning and the `IFlushableLoggerHandle`
  flush beside it are three lines of straight-line code in `App.axaml.cs` and were read, not executed.
- **No line-ending noise.** Every file touched is uniformly CRLF, which is what the rest of the working
  tree is, and the diff contains no whole-file churn. Nothing to recommend.
- **Follow-up, out of scope:** `App.axaml.cs`'s `ComposeAndHandOver` is now long enough that the exit
  handler would read better as a named private method. Worth a look when something else touches the
  file; not worth a diff of its own here.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -v normal` (Debug): **succeeded, 0 errors, 0 warnings**, no
  `AVLN*` diagnostic (no XAML was touched).
- `dotnet test Enigma.MarkdownEditor.slnx`: **1947 passed, 0 failed, 0 skipped** across
  `Enigma.MarkdownEditor.Core.UnitTests` and `Enigma.MarkdownEditor.Desktop.UnitTests`. **7 of them are
  new** (1940 before this phase).
- **The deadlock was reproduced first, at the unit level.** With `AppShutdown.Run` temporarily reduced
  to the inline shape (`work(token)` instead of `Task.Run(() => work(token))`),
  `WorkThatAwaits_FinishesEvenThoughTheThreadThatStartedItIsBlocked` blocked for the full 30 s and
  failed; the other five passed. Restored, all six pass in 1.1 s.
- **And in the running application, three times out of three.** Against the true pre-fix code (inline
  exit handler *and* `SessionService` without `ConfigureAwait`), a Debug build was launched, its window
  closed with `CloseMainWindow()`, and after 15 s `Get-Process` still found it — window handle `0`,
  process alive — on all three runs. This is the reported symptom exactly.
- **Against the fix, three times out of three it ends.** Same procedure: exited in ~140 ms, exit code
  `0`, `Get-Process` finds nothing, and `session.json` was rewritten each time. Its contents hold the
  placement, the panes and the open document, and the next start came up restored.
- **The log distinguishes the three states, which is the clearest evidence of the mechanism.** Every
  hung run stops after `Enigma Markdown Editor is shutting down.` and never reaches the host's own
  `Application is shutting down...`, i.e. it died inside the flush — as do all seven of the user's real
  exits recorded in `logs/app_02.log` before today. The `ConfigureAwait`-only runs log both lines, the
  second on `ThreadId=2`, the UI thread. The fixed runs log both with the second on a pool thread
  (`ThreadId=8`, `10`, `12`) — the sequence is off the UI thread, which is the whole point.
- No `Shutting down did not finish within` warning was logged on any fixed run: the sequence completes
  well inside the five seconds.
