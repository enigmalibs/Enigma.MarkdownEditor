# FEATURE-7FA8 — Splash screen & about dialog

**Status: DONE** · Multi-phase (2) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-7fa8-phase01-about-dialog`, `feature/feature-7fa8-phase02-splash-screen`.

## Objective

Give the application an identity it can state: an About dialog reachable from the toolbar, and a splash screen while it starts. Both show the icon, the product name and the version. `docs/RELEASE.md` §6 currently says *"The application has no About box and its window title carries no version"* — this item is what makes that sentence false.

The repository is **private**, so neither surface carries a link to it.

## Context & constraints

- **There is no version at runtime.** `AppInfo` holds `DisplayName` and `AppDataFolderName` and nothing else. `<Version>1.0.0</Version>` lives in both csprojs, and the SDK stamps it into `AssemblyInformationalVersion` as `X.Y.Z+<sha>` — that attribute is the only source, and reading it is new work.
- **There is no bitmap.** `Assets/` holds `app.ico` alone: 4 frames (16/32/48/256), all uncompressed 32-bpp BMP, **no PNG frame inside**. Avalonia decodes images through Skia, whose ICO handling is not something to rely on across the win-x64 and linux-x64 builds, and which frame it would pick is unspecified. A real bitmap asset is therefore a prerequisite for both phases.
- **Composition is synchronous.** `App.OnFrameworkInitializationCompleted` runs three config loads, builds and starts the `IHost`, installs `CrashReporter`, applies the theme, resolves `MainWindow`, registers three dialog hosts plus two storage providers, and only then assigns `desktop.MainWindow`. A window shown before that block would never paint, because the message loop never gets a turn. PHASE02 is therefore a restructure of this method, not an addition to it.
- **The ContentDialog host lives in `MainWindow`.** `IContentDialogService` throws `InvalidOperationException` until `RegisterHost` has run, and it is unreachable while a modal `Window` (the settings window) is on top — which is why About is opened from the toolbar and not from a settings row.
- **House seam for a dialog:** `WorkspaceManagerDialogService` is the model to follow — a `…DialogService` owns the view and calls `IContentDialogService.ShowAsync`; no ViewModel ever sees a `Window`.
- `using` directives stay at **file scope, above the namespace** (the `Enigma.Avalonia.*` ↔ `Avalonia.*` collision). Every view root and `DataTemplate` needs `x:DataType` or the build fails `AVLN2100`. Theme colours are `{DynamicResource Enigma…Brush}`, never `StaticResource`.
- **No new package.** Everything needed is already referenced.
- English only — the project has no localization infrastructure, and none is introduced here.

## Design

### PHASE01 — About dialog & version plumbing — **Status: DONE**

**`AppInfo` (Core)** gains the product's own version, so the whole application has one answer:

- `Version` — the `X.Y.Z` part of `AssemblyInformationalVersionAttribute` on `typeof(AppInfo).Assembly`, taken up to the first `+`. Falls back to the assembly version, then to `"0.0.0"`, so it is never null and never throws.
- `BuildSha` — `string?`, the part after the `+`, shortened to 7 characters. **Null** when the attribute carries no source revision (a plain local build), and the About view hides the line rather than printing an empty one.
- Read from Core's own assembly rather than `GetEntryAssembly()`: Core and Desktop share one `<Version>`, and an entry-assembly lookup returns null under a test host, which would make the property untestable.

**Icon asset** — `src/Enigma.MarkdownEditor.Desktop/Assets/app-256.png`, decoded from the 256×256 BGRA frame of `app.ico` and committed. Picked up by the existing `<AvaloniaResource Include="Assets/**" />` glob, so the csproj needs no edit. Both this phase and PHASE02 bind `<Image Source="/Assets/app-256.png" Width="96" Height="96" />`. The `.ico` stays as it is — it is what `ApplicationIcon` and the window `Icon` use.

**`AboutViewModel`** (`Desktop/ViewModels`, transient) — `ObservableObject`, read-only: `DisplayName`, `Version`, `BuildSha`, `Copyright`, and `Credits` (an `IReadOnlyList<CreditEntry>` of name + license). The copyright string comes from `AssemblyCopyrightAttribute`, which `Directory.Build.props` already populates from `<Copyright>`, so it is not duplicated in code.

**`AboutView`** (`Desktop/Views`, `UserControl`, `x:DataType="vm:AboutViewModel"`) — icon, name, version, then the SHA beneath it in a small quiet `SelectableTextBlock` so it can be pasted into a report, then the copyright, then the credits block. Styled with `EnigmaForegroundSecondaryBrush` / `EnigmaForegroundTertiaryBrush` like the existing views.

**Credits content** — one entry per package actually redistributed in the self-contained build: Avalonia, AvaloniaEdit, TextMateSharp, Markdig, NLog, CommunityToolkit.Mvvm, and the Enigma libraries. **Each license is verified against the package itself at build time**, not assumed from memory; the verified list goes in the completion doc. This is the attribution the MIT/BSD terms ask for, and the reason it is not skipped.

**`IAboutDialogService` / `AboutDialogService`** (`Desktop/Services`, singleton) — mirrors `WorkspaceManagerDialogService`: builds the view over a fresh ViewModel, calls `IContentDialogService.ShowAsync` with `Title = "About"`, `IconData = {ei:IconGeometry Info}`'s geometry, and a single `CloseButtonText = "Close"`.

**Entry point** — an `Info` button in `MainWindow.axaml`'s right-hand toolbar group, immediately left of the Settings gear, `Classes="toolbar"`, `ToolTip.Tip="About"`, bound to a new `OpenAboutCommand` (`AsyncRelayCommand`) on `MainWindowViewModel`. The command wraps the call in the same broad `try`/`catch` + log that `OpenSettingsAsync` uses, and for the same reason: an `AsyncRelayCommand` parks a faulted task rather than surfacing it.

Acceptance criteria:

- The toolbar shows an About button beside Settings; clicking it opens a dialog carrying the app icon, `Enigma Markdown Editor`, the version, the copyright and the credits, dismissed by `Close`, `Escape` or the scrim.
- The version shown equals `<Version>` in the csproj; the SHA line appears when the build stamped one and is absent when it did not.
- The dialog renders correctly in both themes (no `StaticResource` colour).
- `app-256.png` loads on both `win-x64` and `linux-x64` — verified by running the app, since a resource that fails to decode is silent.
- No repository link anywhere in the dialog.
- `Core.UnitTests`: `AppInfo.Version` / `BuildSha` parsing — `1.1.0+abcdef1234` → `1.1.0` / `abcdef1`, a bare `1.1.0` → null SHA, a malformed value falling back rather than throwing.
- `Desktop.UnitTests`: `AboutViewModel` exposes the version and copyright and returns a non-empty credits list; a headless view test asserts the `AboutView` bindings resolve and the SHA line collapses when `BuildSha` is null.

### PHASE02 — Splash screen & startup sequence — **Status: DONE**

**`SplashWindow`** (`Desktop/Views`) — `SystemDecorations="None"`, `CanResize="False"`, `ShowInTaskbar="False"`, `Topmost="True"`, `WindowStartupLocation="CenterScreen"`, `TransparencyLevelHint` left alone (a transparent splash behaves differently on every Linux compositor). Fixed size around 420×260, `Background="{DynamicResource EnigmaSurfaceBrush}"` with a 1px `EnigmaBorderSubtleBrush` border. Content: the same `app-256.png`, the display name, the version. No progress text — with a ~1s life, a sequence of status messages would only flicker.

**Startup restructure** — `App.OnFrameworkInitializationCompleted` becomes:

1. Apply the persisted theme variant to the `Application` **before** the splash is shown, so the splash itself is painted in the right variant. This needs the settings load to stay where it is (first), which it does.
2. Construct and `Show()` the `SplashWindow`; record `DateTime` (via `Stopwatch`) at that moment.
3. Post the remainder — host build, `CrashReporter.Install()`, the three failure-log branches, `IThemeService.Apply`, `MainWindow` resolution, the five host registrations, `desktop.MainWindow` assignment and the `Exit` handler — to the dispatcher, so the splash gets a frame before the work begins.
4. Assign `desktop.MainWindow` and `Show()` it, **then** close the splash. The order is not cosmetic: under the default `ShutdownMode.OnLastWindowClose`, closing the splash while it is the only window would exit the process.
5. Hold the splash for the remainder of `SplashMinimumDisplay` (a named `TimeSpan` constant, **1000 ms**) before closing it, so a fast start does not flash it.

**Ordering guarantees that must survive the restructure**, and which the phase is judged on:

- `CrashReporter.Install()` still runs before `MainWindow` is constructed — it is what covers the construction.
- The theme is applied before the main window is shown, so it never flashes the OS default.
- All five `RegisterHost` / `SetStorageProvider` calls still run before the main window is shown; any Enigma service asked for a dialog before its host exists throws.
- `MainWindow.OnOpened` still fires exactly once, and `MainWindowViewModel.OnOpenedAsync` still runs after it — `FEATURE-284C` hangs off that method.
- An exception thrown inside the posted continuation must not leave the splash on screen forever: it closes in a `finally`, and the failure goes through the crash path as before.

The splash is shown in **all build configurations**, Debug included — a startup path that differs between Debug and Release is a path that gets tested in only one of them.

Acceptance criteria:

- The splash appears centred, painted (not a white rectangle), for at least 1 second, and the main window replaces it without a visible gap or a second flash.
- The splash never appears in the taskbar or in Alt-Tab, and closing it never terminates the application.
- A start on a cold machine, where composition exceeds 1 second, closes the splash as soon as the main window is up — the minimum is a floor, not a fixed duration.
- Both theme variants paint the splash correctly.
- Session restore, the startup InfoBar notice and the crash handler all still behave exactly as before — verified by running the app, and stated as such in the completion doc.
- `Desktop.UnitTests`: the minimum-display arithmetic is extracted into a pure, testable helper (elapsed → remaining delay) and tested at the boundaries; the window sequencing itself is verified by running the application, which the completion doc records honestly rather than claiming coverage it does not have.

## Dependencies

PHASE02 depends on PHASE01 for `AppInfo.Version` and `Assets/app-256.png`. Both are independent of `FEATURE-284C`. `FEATURE-6EF0` must come after both.

## Out of scope

An update check · a licence-text viewer · an "open log folder" button (the Settings window's Advanced group already shows the paths) · a build date · dedicated splash artwork distinct from the app icon · a progress or status line on the splash · localization.
