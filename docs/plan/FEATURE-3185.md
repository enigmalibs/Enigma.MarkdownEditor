# FEATURE-3185 — Remove the splash screen

**Status: TODO** · single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-3185-remove-splash-screen`, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

The application starts right away: no splash screen and no minimum display time. The main window is
the first thing on screen. This is part of the uniform baseline for every Enigma app. Enigma.GitClient
removes its own splash in the same way (its `FEATURE-0DED`).

## Context & constraints

- The splash came with `FEATURE-7FA8` PHASE02:
  - `Views/SplashWindow.axaml(.cs)`: the borderless window showing the icon, name and version;
  - `Views/SplashTiming.cs`: `MinimumDisplay` and `RemainingDelay`;
  - `App.OnFrameworkInitializationCompleted`: shows the splash, then posts `ComposeAndHandOver` at
    `DispatcherPriority.Background` so the splash paints first;
  - `ScheduleHandOver` / `HandOver`: hold the splash until its minimum time has passed, show the
    main window, then close the splash.
- `ShutdownMode` is the default `OnLastWindowClose`. With no splash there is no hand-over, and the
  standard pattern applies: assign `desktop.MainWindow` inside `OnFrameworkInitializationCompleted`,
  and the lifetime shows it as it enters the message loop.
- `Assets/app-256.png` stays, because the About dialog shows it. `AppIconTests` pins it.
- Text that names the splash:
  - `App.axaml:20` (the theme comment);
  - the `App` class remarks and the `ComposeAndHandOver` remarks;
  - `AppIconTests` (line 19, and the `TheSplashAndAboutImage_Is256PixelsSquare` test name);
  - `README.md:118` (the feature list);
  - `docs/RELEASE.md`: line 121 (icon check), line 155 (the §2 *Splash* smoke item) and lines
    310–312 (the first-run watch).
- `RELEASENOTES.md` is history and stays untouched. The release item `FEATURE-B260` records the
  removal.

## Steps

1. Delete `Views/SplashWindow.axaml`, `Views/SplashWindow.axaml.cs` and `Views/SplashTiming.cs`.
2. In `App.OnFrameworkInitializationCompleted`:
   - set the theme variant;
   - compose the host and the main window inline, without a dispatcher post;
   - assign `desktop.MainWindow`.

   Drop `ScheduleHandOver`, `HandOver` and the splash parameters. Keep everything else in its current
   order: crash handlers first, then the load warnings, the dialog hosts and the exit sequence.
   Reword the class remarks so they no longer describe a two-part start-up.
3. Reword the comments that name the splash (listed above).
4. Tests:
   - delete `Views/SplashWindowTests.cs` and `Views/SplashTimingTests.cs`;
   - rename `AppIconTests.TheSplashAndAboutImage_Is256PixelsSquare` so its name refers to the
     About image only;
   - drop any test helper that only the splash tests used.
5. Docs: in `README.md`, take the splash out of the feature list. In `docs/RELEASE.md`, remove the
   §2 *Splash* item and reword the other two mentions so they name the main window and About dialog.

## Acceptance criteria

- No `Splash*` type, file or test remains under `src/` or `tests/`.
- `grep -ri splash src tests README.md docs/RELEASE.md` returns nothing.
- `App` assigns `desktop.MainWindow` inside `OnFrameworkInitializationCompleted`, with no dispatcher
  post and no timer.
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green.

## Out of scope

- The About dialog (`FEATURE-982C`) and crash handling before the reporter exists (`FEATURE-E31C`).
- `RELEASENOTES.md` (`FEATURE-B260`).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Hide the splash or delete it | Delete every splash type and test | Code that is never shown is dead weight; "starts right away" | A setting to turn it off; leaving the types unused |
| How the main window starts | Composed inline; `desktop.MainWindow` assigned before `OnFrameworkInitializationCompleted` returns | The Background-priority post existed only so the splash could paint first. This is the standard Avalonia pattern | Keeping the post at Background or Normal priority |
| Start-up failure path | Unchanged here (it still propagates); `FEATURE-E31C` covers it | One concern per dev | Adding a start-up error window in this dev |
| Tests | Delete the splash tests; add no new test | The behaviour is removed, and the headless smoke tests already build the app | A test asserting that a type is absent |
