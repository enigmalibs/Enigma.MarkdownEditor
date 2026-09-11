# FEATURE-7FA8 PHASE02 — Splash screen & startup sequence

**Status: DONE** · Branch: `feature/feature-7fa8-phase02-splash-screen` · Plan: `docs/plan/FEATURE-7FA8.md`

## Summary

The application now says what it is while it starts. A 420×260 undecorated window appears centred on
the screen about 0.8 s into the process, carrying the icon PHASE01 decoded, the product name and the
version, and the main window replaces it once composition is finished — never sooner than one second
after it appeared.

**The work was the restructure, not the window.** `App.OnFrameworkInitializationCompleted` ran three
config loads, built and started the host, installed the crash handlers, applied the theme, resolved
the main window and registered five hosts, all before returning — and the Avalonia lifetime does not
start the message loop until it *has* returned. A window shown inside that method paints nothing,
because nothing renders. So the method is now two halves: the theme and the splash inline, everything
else posted back to the dispatcher.

**Posted at `Background`, and that priority is the whole point.** Layout and render run at priorities
*above* `Normal`, so a continuation posted at the default would have taken its turn before the splash
drew a single frame — the white rectangle the exercise exists to avoid. `Background` sits below
`Render`, so the splash is painted before composition starts. `EditorPaneView` and
`FileNamePromptView` already post at `Background` for the same "let the UI catch up first" reason.

**The lifetime no longer shows the main window; the hand-over does.**
`ClassicDesktopStyleApplicationLifetime` shows its `MainWindow` on its way into the message loop,
which it has already entered by the time the posted job runs — so `desktop.MainWindow = mainWindow`
alone would now leave the window assigned and invisible. `HandOver` shows it, then closes the splash,
in that order: under the default `ShutdownMode.OnLastWindowClose`, closing the splash while it is the
only window on screen ends the process.

**The minimum display is a floor, not a duration.** `SplashTiming.RemainingDelay(elapsed, minimum)`
answers what is still owed; zero means hand over now. On this machine composition takes 1.1–1.4 s, so
every ordinary start takes the immediate branch and the splash simply goes away when the editor is
ready. The held branch is the *first-run* path — an empty session composes in well under a second —
and it is the one that would strand a user behind a splash that never left, so it was exercised for
real rather than reasoned about (see the evidence below).

**A failure cannot leave the splash on screen.** The composition job's `finally` closes it if the
hand-over was never scheduled, and `HandOver`'s own `finally` closes it even if showing the main window
throws. Composition also runs as a plain dispatcher job rather than as an `async` continuation, so an
exception reaches `Dispatcher.UIThread.UnhandledException` — `CrashReporter`'s handler, which logs and
flushes synchronously — instead of sitting in an unobserved task until a garbage collection notices it.

## Files/modules touched

**Created — Desktop**

- `Views/SplashWindow.axaml` / `.axaml.cs` — the window: undecorated, 420×260, centred, off the
  taskbar, topmost, `EnigmaSurfaceBrush` behind a 1 px `EnigmaBorderSubtleBrush` border, and the icon,
  name and version. No ViewModel and no `DataContext`: the two strings come straight off `AppInfo`
  through `x:Static`, because they are constants for the life of the process. Logic-free code-behind,
  like `AboutView`.
- `Views/SplashTiming.cs` — `MinimumDisplay` (1000 ms) and the pure `RemainingDelay`.

**Modified — Desktop**

- `App.axaml.cs` — the restructure: the theme and the splash inline, `ComposeAndHandOver` posted at
  `Background`, `ScheduleHandOver` for the floor, and `HandOver` for the swap. Every ordering guarantee
  the plan lists is preserved, in the same relative order as before: `CrashReporter.Install()` before
  the main window is constructed, the three config-failure warnings, the five `RegisterHost` /
  `SetStorageProvider` calls before the window is shown, and the `Exit` handler.
- `App.axaml` — the `RequestedThemeVariant` comment, which said `IThemeService` applies the variant
  before the window is shown. It is now applied before the *splash* is shown, by `App` itself.

**Modified — docs**

- `docs/RELEASE.md` — a **Splash** check in §2's Manual Linux verification list (the window is
  undecorated, topmost and owned by a hidden parking window, and all three are compositor-dependent),
  and, in §6, the fresh-install item now says to watch the splash on *that* start specifically: a
  first run has no session to restore, so it is the one that takes the held branch. Folded in from the
  documentation freshness sweep, at your choice; `README.md` was offered and declined.

**Created — tests**

- `Desktop.UnitTests/Views/SplashTimingTests.cs` — 13 cases over the arithmetic.
- `Desktop.UnitTests/Views/SplashWindowTests.cs` — 5 headless cases over the real window.

## Deviations & follow-ups

- **`SystemDecorations="None"` is `WindowDecorations="None"`.** The plan names the former; in Avalonia
  12 it is `[Obsolete]` in favour of `WindowDecorations`, and the XAML compiler raises AVLN5001 for it
  — a warning, and this solution's Definition of Done is a zero-warning build. Same enum values, same
  effect.
- **The theme is applied through `ThemeService.ToVariant`, not `IThemeService.Apply`, and the later
  `Apply` call is gone.** The plan asks for the variant to be applied before the splash is shown *and*
  keeps `IThemeService.Apply` in the posted remainder. The first cannot go through the container — the
  container is built in the posted half — so the static mapping `ThemeService` already exposes is used
  instead, and the second call became a redundant re-assignment of a value already set. The guarantee
  the plan asks for ("the theme is applied before the main window is shown") is now strictly stronger:
  it is applied before *any* window is shown. `IThemeService` is untouched and still what the settings
  window drives the live switch through.
- **The hold happens before the main window is shown, not after.** The plan's step 4 shows the main
  window and then closes the splash, and its step 5 holds the splash before closing it; taken
  together, the only sequence that satisfies both is hold → show → close, which is also the one that
  matches the acceptance criterion "the main window replaces it without a visible gap". Showing the
  main window first would leave it sitting under a topmost splash for the rest of the hold.
- **`DispatcherTimer.RunOnce` instead of `await Task.Delay`.** The plan says to post the remainder and
  hold for the remaining time; an `async` continuation would have been the obvious way, and it is the
  wrong one here — nobody awaits that task, so a start-up failure inside it would become an unobserved
  task exception rather than a crash. A one-shot timer keeps every step a dispatcher job, which is
  what `CrashReporter`'s dispatcher handler covers.
- **Composition no longer runs under a non-desktop lifetime.** It used to happen before the
  `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)` check, so the XAML previewer
  built the whole host — starting NLog against the real log file — and then used none of it. It now
  lives inside the branch that has a window to compose. The three config loads still happen either
  way. Nothing consumed the host outside that branch, so this is a narrowing, not a regression.
- **`x:Static` over a ViewModel, for two constants.** `AppInfo.DisplayName` is a `const` and
  `AppInfo.Version` a static property; both bind directly. A `SplashViewModel` forwarding them would
  have been a type whose only job was to exist. The trade-off is that the splash cannot be shown with
  different text in a test, which is why `SplashWindowTests` asserts against `AppInfo` itself.
- **The splash is the one window not resolved from the container**, by necessity: it has to be on
  screen before the container exists. `new SplashWindow()` in `App`, no DI registration.
- **The startup InfoBar notice was not exercised.** It only appears when a config file fails to load,
  and none did in any of the runs below. Its code is unchanged and reached the same way — through
  `MainWindow.OnOpened` → `MainWindowViewModel.OnOpenedAsync` — and the *first* thing that method does,
  session restore, demonstrably ran (see the evidence), so the path to the notice ran with it.
- **No log entries were produced by these runs.** Your own copy of the application has been running
  since 27 August and holds `logs\app.log` open, so NLog in a second process could not write to it —
  the same lock that made PHASE01 build to a temp artifacts path. The logging calls are unchanged and
  in the same order; what is *not* evidenced by a log line is that they wrote anything on this run.
- **Your session file was rewritten by the test runs, with equivalent content.** Each run restored
  your session and the window's own placement recorder then wrote it back — same workspace, same open
  document, same view mode, geometry within a few pixels. Nothing was lost, and your running instance
  will write its own state over it on exit. Worth knowing that running the app to verify it is not a
  read-only act.
- **No sub-agents.** One method restructured, one small window and one arithmetic helper, all coupled
  through the same start-up sequence — nothing separable enough to be worth a handover.
- **Line endings:** no CRLF/LF churn appeared in this dev's diff, and nothing was done about line
  endings either way, per the workflow's recommendation-only rule.
- **Out of scope by the plan, and not done:** dedicated splash artwork distinct from the app icon, a
  progress or status line, a build date, localization, and any transparency or blur (the plan leaves
  `TransparencyLevelHint` alone deliberately — a translucent splash behaves differently on every Linux
  compositor).

## Build/test evidence

- `dotnet build` in **Debug** and **Release**, both **succeeded with zero warnings**
  (`TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on solution-wide). Built to a temp
  artifacts path, as in PHASE01: your copy of the application is running and holds a lock on its own
  `.dll`.
- `dotnet test` — **1890 passed, 0 failed, 0 skipped**, identically in Debug and Release. The suite
  stood at 1872 after PHASE01, so this dev adds **18 cases**. Run again after the temporary
  instrumentation described below was reverted: still 1890.
- `SplashTimingTests` — **13 cases**: the floor still owed from 0 ms and from 400 ms; one millisecond
  short of it still holding one millisecond; exactly at it, one past it and five seconds past it all
  holding nothing; a floor of zero holding nothing whatever has elapsed; never negative across a sweep
  of elapsed values, because the value goes straight to a timer that throws on a negative interval;
  and `MinimumDisplay` being the 1000 ms the plan names.
- `SplashWindowTests` — **5 headless cases through the real window**: the product name and version
  present as rendered text (which is also what proves `x:Static` resolved); the `Image` resolving a
  non-null `Source`, the guard on the asset still existing; the five window flags a splash depends on
  (`WindowDecorations.None`, `CenterScreen`, no taskbar, no resize, topmost); and the surface and
  border brushes both resolving under `ThemeVariant.Light` *and* `ThemeVariant.Dark`.
- **Six real start-ups, instrumented at the window level.** A throwaway probe — in the session
  scratchpad, not in the repository — launched the built executable and polled `EnumWindows` every
  25 ms for the process's visible top-level windows, with their rectangles and extended styles, and
  captured the screen at intervals. Debug and Release both reported the same sequence:

  | Build | Splash shown | Main window shown | Splash closed | Splash lifetime |
  |---|---|---|---|---|
  | Debug | 1198 ms | 2554 ms | ≤2636 ms | ~1356 ms |
  | Debug | 826 ms | 1967 ms | ~1967 ms | ~1141 ms |
  | Release | 1028 ms | 2153 ms | ≤2183 ms | ~1125 ms |
  | Release | 895 ms | 2054 ms | ~2054 ms | ~1159 ms |

  Every splash reported **420×260 at (750, 386)** on a 1920×1080 screen — centred in the working area.
  One poll caught both windows visible at once (the Debug 2554 ms row), which is the hand-over: the
  main window is up before the splash goes, never the reverse.
- **The splash is painted, not a white rectangle.** A frame taken the instant the window became
  visible is empty — the window exists before it has drawn — and one taken 400 ms later shows the
  icon, `Enigma Markdown Editor` and `Version 1.0.0` over the Enigma surface with its border. The
  version shown is `1.0.0`, which is `<Version>` in the csproj.
- **Both variants, rendered through real Skia.** The dark runs above are your persisted theme. For the
  light one, the binary was temporarily instrumented — `RequestedThemeVariant` forced to `Light` — and
  the splash rendered correctly: near-white surface, dark product name, quiet grey version line, the
  border still visible. Both instrumentation edits were reverted and the suite re-run afterwards.
- **The held branch was exercised, not reasoned about.** Composition on this machine always exceeds
  1000 ms, so no ordinary run takes it. With `MinimumDisplay` temporarily set to **5000 ms**, the same
  probe reported the splash shown at 826 ms and the main window at **5988 ms** — composition had
  finished around 2 s and the hand-over waited for the floor, which is `DispatcherTimer.RunOnce`
  firing and `HandOver` running from it. This is the first-run path, and it works.
- **Never in the taskbar, never in Alt-Tab.** No taskbar button appears while the splash is up
  (screenshots include the taskbar). The reason is in the probe's numbers: Avalonia's
  `ShowInTaskbar="False"` gives the window a hidden owner (`GW_OWNER` is non-null) and clears
  `WS_EX_APPWINDOW` — `ex=0x00200008`, i.e. `WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP`, with neither
  `WS_EX_APPWINDOW` nor `WS_EX_TOOLWINDOW`. An owned window without `WS_EX_APPWINDOW` is not a
  switcher entry, so it is excluded from Alt-Tab as well as from the taskbar.
- **Closing the splash never terminates the application.** In every run the process stayed alive after
  the splash closed and kept reporting `MainWindowTitle = "Enigma Markdown Editor"` for the rest of the
  probe's window — the hand-over's ordering doing its job.
- **Session restore still behaves as before**: every run came up with the workspace, the open document
  and the split view the session file held, which is `MainWindow.OnOpened` → `OnOpenedAsync` running
  exactly as it did. The crash handler was not made to fire; it is installed at the same point in the
  sequence as before, still ahead of the main window's construction.
- **Not covered, honestly.** Linux (`linux-x64`) — this is a Windows machine, and both the splash's
  window flags and the `x:Static`-bound asset are exactly the kind of thing that can differ there;
  `docs/RELEASE.md` §2's Manual Linux verification is where that check belongs. The startup InfoBar
  notice and the crash dialog were not triggered, as noted above. And no log line evidences these
  runs, because the log file was locked by your running instance.
