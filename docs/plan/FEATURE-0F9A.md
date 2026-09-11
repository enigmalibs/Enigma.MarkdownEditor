# FEATURE-0F9A — Solution bootstrap & app shell

**Status: DONE** · Multi-phase (3) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-0f9a-phase01-bootstrap`, `feature/feature-0f9a-phase02-shell`, `feature/feature-0f9a-phase03-settings-logging`.

## Objective

Stand up the repository, the solution, and a runnable, Enigma-themed Avalonia 12 application shell with persisted settings, file logging, and crash handling — the foundation every other work item builds on.

**Product identity:** display name **"Enigma Markdown Editor"**; assemblies keep `Enigma.MarkdownEditor.*` names. Platforms: **Windows + Linux**, both first-class.

## Context & constraints

- New solution `Enigma.MarkdownEditor.slnx` at repo root; `src/` + `tests/` + `docs/` layout (dotnet-solution-setup skill).
- Projects — all `net10.0`:
  - `src/Enigma.MarkdownEditor.Core` — class library, **zero Avalonia dependency** (vim engine, workspace/session/search services, markdown pipeline). `net10.0` rather than `netstandard2.0` because it is app-internal and never published — document this deviation in a csproj comment.
  - `src/Enigma.MarkdownEditor.Desktop` — the Avalonia app (WinExe).
  - `tests/Enigma.MarkdownEditor.Core.UnitTests`, `tests/Enigma.MarkdownEditor.Desktop.UnitTests` — xUnit v3, MTP-native (`OutputType Exe`, no `Microsoft.NET.Test.Sdk`).
- **Central Package Management pins** (`Directory.Packages.props`; version-coupled sets bump together):
  - Avalonia set 12.1.1: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter` (+ `Avalonia.Headless` for tests when needed)
  - `AvaloniaUI.DiagnosticsSupport` 2.2.3 (Debug-only assets pattern from the avalonia skill)
  - `Enigma.Avalonia.Desktop` 1.0.0 · `Enigma.Icons.Avalonia` 1.0.0
  - `Enigma.Configuration` 1.0.x (latest 1.x) · `Enigma.Logging.NLog` 1.0.x
  - `Avalonia.AvaloniaEdit` 12.0.0 · `AvaloniaEdit.TextMate` 12.0.0 (consumed from FEATURE-3C44 on; pinned here)
  - `Markdig` 1.3.2 · `xunit.v3` 3.2.2 (+ optional `coverlet.collector`)
- `global.json`: `sdk 10.0.100, rollForward latestFeature` + `"test": { "runner": "Microsoft.Testing.Platform" }` (SDK 10.0.400 is installed and satisfies the pin).
- `Directory.Build.props`: LangVersion 14, Nullable enable, ImplicitUsings disable, TreatWarningsAsErrors true, EnforceCodeStyleInBuild true, Authors/Copyright — mirror the values used by sibling repos (e.g. `C:\Dev\EnigmaLibs\Enigma.Configuration`).
- MVVM: CommunityToolkit.Mvvm (transitive via Enigma.Avalonia.Desktop) **without source generators** — `field`-keyword `SetProperty` properties and explicit `*Command` properties (communitytoolkit-mvvm skill).
- **Namespace-collision constraint:** our root namespace starts with `Enigma.`, and the referenced assemblies define `Enigma.Avalonia.*` — an inline qualified `Avalonia.X` reference inside a `namespace Enigma.MarkdownEditor…` block resolves against `Enigma.Avalonia` and fails to compile. Keep all `using` directives at file scope above the namespace declaration; use `global::Avalonia.…` where an inline qualification is unavoidable.
- `AVLN*` XAML warnings are **not** promoted by TreatWarningsAsErrors — the zero-warning Definition of Done includes reading the warning count on Desktop builds.

## Design

- **Bootstrap** (avalonia + dotnet-solution-setup skills): synchronous `Main` → `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`; `UsePlatformDetect().WithInterFont().LogToTrace()` + `.WithDeveloperTools()` under `#if DEBUG`. `App.OnFrameworkInitializationCompleted` builds a `HostApplicationBuilder`, registers services, starts the host, resolves `MainWindowViewModel` from `host.Services`, stops the host on `desktop.Exit`.
- **Enigma wiring** (enigma-avalonia-desktop skill, `reference/setup.md`): FluentTheme in `Application.Styles`; Enigma theme dictionary as `ResourceInclude` in `Application.Resources`; self-written `AddEnigmaServices()` extension registering the six services as singletons; `ContentDialog`/`Overlay`/`InfoBar` host controls as last children of the window root panel, `RegisterHost(...)` ×3 + `SetStorageProvider(mainWindow.StorageProvider)` ×2 before the window is shown; window `Background="{DynamicResource EnigmaBackgroundBrush}"`.
- **Shell layout** (fixed 3-pane): root Grid rows = slim toolbar / content / status bar. Content = `sidebar (min 180, default 280) | GridSplitter | editor area * | GridSplitter | preview area *`; sidebar and preview collapsible (view toggles in toolbar); placeholders this item — real content arrives with later items. Status bar: vim-mode slot, line:col slot, file-info slot (placeholders). Icons via `<ei:Icon>` (Enigma.Icons.Avalonia).
- **Settings**: `AppSettings` POCO in Core — `Theme` (System/Dark/Light, default System), `VimEnabled` (true), `AutosaveEnabled` (false), `AutosaveDelaySeconds` (2), `ShowAllFiles` (false), `EditorFontFamily` (platform monospace stack default), `EditorFontSize` (14), `WordWrap` (true), `ImageFolderName` ("assets"), `DebugLogging` (false). Persisted with **Enigma.Configuration** (`IConfig<AppSettings>`, per-user app-data path via its `ConfigPaths`, file `settings.json`); registered in DI, warm-up via `InitializeEnigmaConfigurationAsync`. Settings UI = modal settings window of `SettingsCard`/`SettingsCardExpander` rows, opened from a toolbar gear button.
- **Theme switching**: `RequestedThemeVariant` driven by the setting; all app markup uses `{DynamicResource Enigma…}` brushes so a variant switch is live.
- **Logging**: Enigma.Logging.NLog fluent builder → rolling file under `<per-user app-data>/Enigma.MarkdownEditor/logs/`; handle owned by the app lifetime (disposed on exit); bridged into `Microsoft.Extensions.Logging` so everything takes `ILogger<T>`; `DebugLogging` setting lowers the minimum level to Debug.
- **Crash handling**: `AppDomain.CurrentDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` handlers → log, **flush** (`IFlushableLoggerHandle` — the reason NLog was chosen), then best-effort error dialog.

## Phases

### PHASE01 — Repo hygiene & solution skeleton — **Status: DONE**

Steps (bootstrap checklist order from dotnet-solution-setup):
1. `.gitignore` + `.gitattributes` from git-repo-hygiene templates (create-only; print — never run — the `git add --renormalize .` command set for the user).
2. Full C# `.editorconfig`, `Directory.Build.props` (fill Authors/Copyright), `Directory.Packages.props` (CPM on, pins above), `global.json` — from dotnet-solution-config templates.
3. `LICENSE.md` (MIT template, 2026). `RELEASENOTES.md` as 0-byte placeholder (`README.md` already exists — keep it).
4. `Enigma.MarkdownEditor.slnx` from template; the four projects from the library/test templates (Core as plain classlib; Desktop csproj stays minimal this phase — Avalonia packages arrive in PHASE02); one smoke test per test project.
5. Verify: restore, build, test.

Acceptance criteria:
- `dotnet build Enigma.MarkdownEditor.slnx -c Release` succeeds with **zero warnings**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx` passes (≥ 2 smoke tests).
- All root artifacts exist; no `Version=` attributes in any csproj (CPM); no `LangVersion`/`Nullable`/`ImplicitUsings`/`TreatWarningsAsErrors` repeated in csproj files.

### PHASE02 — Avalonia bootstrap & 3-pane shell — **Status: DONE**

Steps: add Avalonia 12.1.1 set + Enigma.Avalonia.Desktop + Enigma.Icons.Avalonia + DiagnosticsSupport to Desktop → Program/App per design → theme merge → `AddEnigmaServices` + host controls + `RegisterHost`/`SetStorageProvider` → MainWindow shell (toolbar, 3 panes + splitters, status bar) → placeholder `.ico`/`.png` app icon (`ApplicationIcon` + `Window.Icon` + `AvaloniaResource` glob; flagged as user-owned art to replace) → `MainWindowViewModel` from DI; compiled bindings with `x:DataType` everywhere.

Acceptance criteria:
- App launches on Windows showing the themed 3-pane shell; dark and light variants verified.
- Splitters resize panes; sidebar/preview collapse toggles work.
- Zero compiler warnings **and** zero `AVLN*` warnings (checked in build output).
- Desktop.UnitTests: MainWindowViewModel construction/behavior test(s) green.

### PHASE03 — Settings, logging & crash handling — **Status: DONE**

Steps: `AppSettings` + `IConfig<AppSettings>` registration + warm-up → theme applied from setting, live switch → settings window (SettingsCards: Appearance / Editor / Behavior / Advanced groups; only settings that already have consumers are shown enabled — others land with their features) → Enigma.Logging.NLog wiring + `ILogger<T>` adoption → crash handlers with flush → debug-only "throw test exception" hook to validate the pipeline.

Acceptance criteria:
- Settings persist across restarts (hand-editable JSON in the per-user app-data folder); invalid file → validation error surfaced, app still starts on defaults.
- Theme setting switches variant live (no restart).
- Log file created; forced test exception produces a logged, flushed entry and an error dialog.
- Core.UnitTests: settings defaults + round-trip against a temp path.

## Dependencies

None — first item.

## Out of scope

Tree/editor/preview/vim functionality (later items) · CI (decided: none) · line-ending renormalization (commands printed for the user; never run) · any git commit (user commits).
