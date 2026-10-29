# FEATURE-3185 — Remove the splash screen

## Summary

The application now starts straight into the main window: no splash screen, and no one-second
minimum display.

`App.OnFrameworkInitializationCompleted` now does three things in order:

1. reads the three config files;
2. applies the theme variant;
3. composes the host and the main window inline (`Compose`), then assigns `desktop.MainWindow`.

The lifetime shows that window as it enters the message loop. This replaces the Background-priority
post, the splash hand-over timer and the `try/finally` that closed the splash on failure.

## Files/modules touched

- Deleted:
  - `src/Enigma.MarkdownEditor.Desktop/Views/SplashWindow.axaml`
  - `src/Enigma.MarkdownEditor.Desktop/Views/SplashWindow.axaml.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Views/SplashTiming.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/SplashWindowTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/SplashTimingTests.cs`
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/App.axaml.cs`
  - `src/Enigma.MarkdownEditor.Desktop/App.axaml` (comment)
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/AppIconTests.cs` (remarks; test renamed to
    `TheAboutImage_Is256PixelsSquare`)
  - `README.md` (the splash bullet removed from *Start-up*)
  - `docs/RELEASE.md` (§1 icon check, §2 *Splash* item removed, §5 fresh-install check)
  - `docs/roadmap.md`, `docs/plan/FEATURE-3185.md`

## Deviations & follow-ups

- `README.md:11`, the 1.9.0 what's-new callout, still names the splash screen. It describes what 1.9.0
  shipped. `FEATURE-B260` rewrites the callout for 1.10.0, so it is left alone here. Every other
  tracked hit under `src/`, `tests/`, `README.md` and `docs/RELEASE.md` is gone. History
  (`RELEASENOTES.md`, `docs/plan`, `docs/done`) is untouched.
- **Start-up failures are unchanged in kind.** Before this dev, composition ran as a dispatcher job
  before `CrashReporter.Install()`. Now it runs inline. Either way, a failure before `Install()` is
  neither logged nor shown. `FEATURE-E31C` closes that gap.
- No test helper was used only by the splash tests, so nothing else was removed.
- The real app was not launched, to avoid writing to the user's own `%APPDATA%` session. Composition
  is unchanged apart from where it runs.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3618 passed, 0 failed, 0 skipped**.
  The baseline was 3636; the 18 removed tests are those of the two deleted splash test files.
- `git grep -i splash -- src tests` returns nothing.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Method name for the composition | `Compose` (was `ComposeAndHandOver`) | There is no hand-over any more |
| Leave the 1.9.0 README callout | Yes | It is release history, rewritten by `FEATURE-B260` |
| Documentation sweep (§7) | README *Start-up* bullet and three `docs/RELEASE.md` passages edited | They described the removed splash |
