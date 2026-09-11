# FEATURE-0F9A-PHASE02 — Avalonia bootstrap & 3-pane shell

**Status: DONE** · Branch: `feature/feature-0f9a-phase02-shell` · Plan: `docs/plan/FEATURE-0F9A.md`

## Summary

Turned the empty `WinExe` from PHASE01 into a running, Enigma-themed Avalonia 12 application: the
Avalonia 12.1.1 package set, the `Program` → `App` → `IHost` bootstrap, the Enigma theme merge and
service wiring, and the fixed three-pane shell (slim toolbar / sidebar · editor · preview with
splitters / status bar) with a placeholder app icon.

Everything inside the panes is deliberately placeholder text naming the work item that fills it
(`FEATURE-0DE1`, `FEATURE-3C44`, `FEATURE-6054`). Settings persistence, logging and crash handling are
PHASE03 and are not started here — the theme variant follows the OS for now.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `App.axaml` | `FluentTheme` in `Application.Styles`; the Enigma dictionary as a `ResourceInclude` in `Application.Resources` (a `StyleInclude` would fail with `AVLN2000`). `RequestedThemeVariant` deliberately unset so the app follows the OS until PHASE03 drives it from the setting. |
| `App.axaml.cs` | Composition root: `HostApplicationBuilder` with `ContentRootPath = AppContext.BaseDirectory`, `AddEnigmaServices()` + `AddShell()`, `host.Start()` (never `Run` — the Avalonia lifetime owns the message loop), then `RegisterHost` ×3 + `SetStorageProvider` ×2 before the window is shown, and `host.StopAsync()`/`Dispose()` on `desktop.Exit`. |
| `Extensions/ServiceCollectionExtensions.cs` | C# 14 extension block. `AddEnigmaServices()` registers the six `Enigma.Avalonia.Desktop` services as singletons (the package ships no such extension); `AddShell()` registers `MainWindow` + `MainWindowViewModel`. |
| `ViewModels/MainWindowViewModel.cs` | `ObservableObject`, no source generators: `field`-keyword `SetProperty` properties. Owns `Title`, `IsSidebarVisible`, `IsPreviewVisible` and the three status-bar slots. |
| `Views/MainWindow.axaml` | The shell. Root `Panel` with the three Enigma host controls last (so they overlay everything), holding a 3-row `Grid`: 38 px toolbar, pane grid, 26 px status bar. Compiled bindings throughout via `x:DataType`. |
| `Views/MainWindow.axaml.cs` | Mirrors the ViewModel's pane flags onto the pane grid's column definitions — see *Deviations* 2 for why this is not a binding. |
| `Assets/app.ico` | Placeholder icon, 4 frames (16/32/48/256, 32bpp). **User-owned art — replace it.** |

### Modified

| File | Change |
|---|---|
| `Enigma.MarkdownEditor.Desktop.csproj` | Avalonia 12.1.1 set + `AvaloniaUI.DiagnosticsSupport` (Debug-only assets) + `Enigma.Avalonia.Desktop` + `Enigma.Icons.Avalonia` + `Microsoft.Extensions.Hosting`; `BuiltInComInteropSupport`, `AvaloniaUseCompiledBindingsByDefault`, `ApplicationIcon`, `AvaloniaResource` glob. |
| `Program.cs` | Placeholder `Main` replaced with `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`; `UsePlatformDetect().WithInterFont().LogToTrace()` and `.WithDeveloperTools()` under `#if DEBUG`. Kept `public` so the XAML previewer finds `BuildAvaloniaApp` and PHASE01's smoke test still compiles. |
| `Directory.Packages.props` | Added the `Microsoft.Extensions.Hosting` 10.0.11 pin (see *Deviations* 1). |
| `docs/roadmap.md`, `docs/plan/FEATURE-0F9A.md` | PHASE02 → `IN PROGRESS` → `DONE`. The item stays `IN PROGRESS` (PHASE03 remains). |
| `tests/…/ProgramSmokeTests.cs` | XML comment only — it pointed at PHASE02 as future work. |
| `README.md` | Documentation freshness sweep (you chose the status block + run command, and declined the Conventions additions): the status note no longer claims there is no shell — it now says the shell runs with placeholder panes — and `dotnet run --project src/Enigma.MarkdownEditor.Desktop` was added to *Build & test*. |

### Created — tests

| File | Notes |
|---|---|
| `tests/…Desktop.UnitTests/ViewModels/MainWindowViewModelTests.cs` | 6 tests: title, pane defaults, status-slot placeholders, and that toggling a pane notifies exactly once (a repeat assignment stays silent — the view rewrites column widths on every notification). |
| `tests/…Desktop.UnitTests/Extensions/ServiceCollectionExtensionsTests.cs` | 9 tests: each of the six Enigma services and both shell types registered as singletons, plus the composed container resolving the shell ViewModel. |

## Deviations & follow-ups

1. **`Microsoft.Extensions.Hosting` 10.0.11 pin added** — the plan's CPM list does not include it, but its
   own *Design* section calls for a `HostApplicationBuilder`, and `Enigma.Avalonia.Desktop` does not
   bring `Microsoft.Extensions.DependencyInjection` transitively. Pinned in its own group with a
   comment: it tracks the runtime band, not the version-coupled Avalonia set.
2. **Pane collapse is done in `MainWindow.axaml.cs`, not by binding column widths.** Hiding a pane is
   not sufficient — a `Grid` column keeps the width it was given, so a collapsed pane would leave a
   gap. Binding `ColumnDefinition.Width` is not an option either: a `ColumnDefinition` is not part of
   the visual tree, so no `DataContext` reaches it. The code-behind captures the pane defaults *from
   the XAML* (so the XAML stays the single source of them), resolves the two collapsible columns via
   `Grid.GetColumn` on the named panes (so inserting a column cannot silently break it), and restores
   a splitter-dragged width after a hide/show round trip. It unsubscribes in `OnClosed` because the
   ViewModel is a container singleton and outlives the window.
3. **Pane toggles are `ToggleButton` + two-way `IsChecked`, not commands.** The plan says "view toggles
   in toolbar" without prescribing the mechanism. Two-way `IsChecked` gives one mutation path and the
   theme's checked visual for free; adding `Toggle…Command` properties now would mean two ways to
   change one flag with nothing yet consuming the second. When keyboard shortcuts or a menu arrive,
   that is the moment to add commands.
4. **No settings gear button in the toolbar.** The plan places it in PHASE03, together with the
   settings window it opens; adding a dead or disabled button now would be worse than adding both
   together.
5. **`MainWindow` registered as a singleton**, where the Enigma setup reference registers page *views*
   as transient. This app has exactly one window for its lifetime; transient would only make it
   possible to build a second by accident. Page views introduced later still go in transient.
6. **`Avalonia.Headless` is pinned but not referenced by any project.** It was used from a throwaway
   probe outside the repo (see *Build/test evidence*), not from the committed test suite. Follow-up
   worth considering once there is real UI to regress: headless UI tests in
   `Desktop.UnitTests` (needs an `[AvaloniaTestApplication]` harness and `Avalonia.Skia` for real
   frames). Not done here — the plan's acceptance criteria ask only for ViewModel tests.
7. **The app icon is placeholder art and should be replaced.** It is a generated blue plate with a
   white "M" and a down arrow at 16/32/48/256 px. `.gitattributes` already marks `*.ico binary`.
8. **Line endings (recommendation only, no action taken).** The new files are CRLF in the working tree
   while `.editorconfig` says `end_of_line = lf`; `.gitattributes` (`* text=auto eol=lf`) normalizes
   them in the index on commit, so no separate pass is expected. If you want it applied explicitly,
   the git-repo-hygiene command set — **not run here** — is `git add --renormalize .`, `git status`,
   `git commit -m "Normalize all line endings to LF"`.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet build -c Release -t:Rebuild -v:n     (src/Enigma.MarkdownEditor.Desktop)
    grep -iE "AVLN[0-9]+" over the full normal-verbosity log → no matches
    0 Warning(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 18   failed: 0   succeeded: 18   skipped: 0
```

18 tests = PHASE01's 3 smoke tests + 6 `MainWindowViewModel` + 9 `ServiceCollectionExtensions`.

### How the visual criteria were verified

The Release executable was launched on Windows and reached a real top-level window (non-zero
`MainWindowHandle`, host logging `Application started` then a clean `Application is shutting down`).
**On-screen capture of that window was not possible: the machine was at the Windows lock screen**, so
the shell's appearance and interaction were verified instead by rendering the same visual tree through
`Avalonia.Headless` + `Avalonia.Skia` (`UseHeadlessDrawing = false`, `CaptureRenderedFrame()`) from a
throwaway probe project outside the repository, driving **real pointer input** at the toolbar
coordinates rather than setting properties directly. Per variant (`Dark`, `Light`):

| Step | Observed |
|---|---|
| Initial frame | Three panes, headers, placeholders, both toggles checked, status bar with all three slots; all colours from the `Enigma*` brushes, correct in both variants. |
| Click sidebar toggle | `IsSidebarVisible=False`; sidebar and its splitter gone, **no leftover gap** — the editor took the width. |
| Click it again | `IsSidebarVisible=True`, pane back at its 280 px default. |
| Click preview toggle | `IsPreviewVisible=False`; preview and its splitter gone, editor expanded. |
| Click it again | `IsPreviewVisible=True`. |
| Drag the sidebar splitter +160 px | Sidebar column width `280` → `440`. |

Acceptance criteria, one by one:

| Criterion | Result |
|---|---|
| App launches on Windows showing the themed 3-pane shell; dark and light variants verified | Met — launch confirmed against the real Windows backend; the themed shell and both variants verified by headless render (see the caveat above: no photograph of the on-screen window, the session was locked). |
| Splitters resize panes; sidebar/preview collapse toggles work | Met — verified by simulated pointer input, table above. |
| Zero compiler warnings **and** zero `AVLN*` warnings | Met — 0 warnings; no `AVLN` match in the normal-verbosity rebuild log. |
| Desktop.UnitTests: `MainWindowViewModel` construction/behaviour test(s) green | Met — 6 ViewModel tests plus 9 composition-root tests, all passing. |
