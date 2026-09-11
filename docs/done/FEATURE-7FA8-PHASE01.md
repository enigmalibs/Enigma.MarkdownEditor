# FEATURE-7FA8 PHASE01 — About dialog & version plumbing

**Status: DONE** · Branch: `feature/feature-7fa8-phase01-about-dialog` · Plan: `docs/plan/FEATURE-7FA8.md`

## Summary

The application can now state what it is. An info button in the toolbar, immediately left of the
settings gear, opens an About dialog carrying the app icon, the product name, the version, the commit
the build was cut from, the copyright and the attribution list.

**The version had to be plumbed, not found.** Nothing in the running process knew it: `AppInfo` held
two constants, and `<Version>1.0.0</Version>` lived only in the two csprojs. The single source at
runtime is `AssemblyInformationalVersionAttribute`, which the SDK stamps as `1.0.0+<full sha>` — this
build reports `1.0.0+00a5176…`. Reading it is now `AppInfo.Version` and `AppInfo.BuildSha`, taken from
**Core's own** assembly rather than the entry assembly: the two projects carry one `<Version>`, and
under the test host the entry assembly is the runner.

**The parsing lives apart from the reading, because only one of the two is testable.** `AppInfo`'s
properties report whatever assembly the process happens to be running, which no test can vary. Every
decision — where the version ends, how long a short sha is, what counts as unusable — is in
`ProductVersion.From(informational, fallback)`, a pure function over two strings, and that is what the
tests exercise. "Malformed" is defined narrowly and on purpose: null, blank, or nothing before the
`+`. A version is **not** re-parsed through `System.Version`, because `1.1.0-rc.1` is a legitimate
product version that `System.Version` rejects outright.

**There was no bitmap to show.** `Assets/` held `app.ico` alone — four uncompressed 32-bpp BMP frames,
no PNG frame inside — and which frame Skia would pick out of an `.ico` is unspecified. The 256×256
frame was decoded out of the icon (bottom-up BGRA to top-down RGBA, its all-zero AND mask correctly
ignored in favour of the real alpha channel) and written as `Assets/app-256.png`: 3,972 bytes, 8-bit
RGBA, non-interlaced. The existing `Assets/**` glob picks it up, so no csproj changed, and `app.ico`
is untouched — it is still what `ApplicationIcon` and the window `Icon` use.

**The dialog goes through the house seam.** `IAboutDialogService` / `AboutDialogService` mirrors
`WorkspaceManagerDialogService`: the service owns the view, builds a fresh ViewModel per showing, and
calls `IContentDialogService.ShowAsync`. No ViewModel sees a `Window`. It is reached from the toolbar
rather than from a settings row for the reason the plan gives — the `ContentDialog` host is a control
in the main window, so a dialog raised behind the modal settings window renders behind it.

**The credits are the point of the credits.** Every licence below was read out of the package it names
— the `<license>` element of its `.nuspec`, or the licence file the package ships — at build time,
not recalled. This is the attribution the MIT, BSD and OFL terms ask for.

| Component | Version resolved | Licence | Read from |
|---|---|---|---|
| Avalonia (+ Desktop, Themes.Fluent, Fonts.Inter) | 12.1.1 | MIT | nuspec `<license type="expression">` |
| AvaloniaEdit (+ TextMate) | 12.0.0 | MIT | nuspec |
| TextMateSharp (+ Grammars) | 2.0.3 | MIT | nuspec |
| Markdig | 1.3.2 | BSD-2-Clause | nuspec |
| NLog | 6.1.4 | BSD-3-Clause | nuspec |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | nuspec |
| Enigma libraries (Avalonia.Desktop, Icons, Icons.Phosphor, Configuration, Logging.NLog) | 1.0.0 | MIT | the packaged `LICENSE.md` — MIT text, © 2026 Josué Clément |
| Phosphor Icons artwork | 2.1.1 | MIT (© 2020 Phosphor Icons) | the `Enigma.Icons.Phosphor` nuspec |
| Inter | ships in `Avalonia.Fonts.Inter` | SIL Open Font License 1.1 | the typeface's own terms |
| Microsoft.Extensions.* | 10.0.11 | MIT | nuspec |

## Files/modules touched

**Created — Core**

- `ProductVersion.cs` — the `readonly record struct` and `From(string?, string?)`, the pure parse the
  two new `AppInfo` properties are built on.

**Modified — Core**

- `AppInfo.cs` — `Version`, `BuildSha` and `Copyright`, plus the private `FallbackVersion()` that
  trims the four-component assembly version to three.

**Created — Desktop**

- `Assets/app-256.png` — the 256×256 frame of `app.ico`, as a PNG. Binary, and already covered by
  `.gitattributes`' `*.png binary`.
- `ViewModels/CreditEntry.cs` — name and licence.
- `ViewModels/AboutViewModel.cs` — the read-only ViewModel, `DefaultCredits`, and the explicit-values
  constructor the tests need.
- `Views/AboutView.axaml` / `.axaml.cs` — icon, name, version, build line, copyright, credits box.
  Logic-free code-behind, like `WorkspaceManagerView`.
- `Services/IAboutDialogService.cs` / `Services/AboutDialogService.cs`.

**Modified — Desktop**

- `Views/MainWindow.axaml` — the `Info` toolbar button, immediately left of the gear.
- `ViewModels/MainWindowViewModel.cs` — the `IAboutDialogService` dependency, `OpenAboutCommand`, and
  `OpenAboutAsync` with the same broad catch, log and InfoBar that `OpenSettingsAsync` uses.
- `Extensions/ServiceCollectionExtensions.cs` — `AboutViewModel` (transient) and `IAboutDialogService`
  (singleton).

**Created — tests**

- `Core.UnitTests/ProductVersionTests.cs` — 15 cases over the parsing.
- `Desktop.UnitTests/ViewModels/AboutViewModelTests.cs` — 8 cases.
- `Desktop.UnitTests/Views/AboutViewTests.cs` — 7 headless cases over the real view.
- `Desktop.UnitTests/Fakes/FakeAboutDialogService.cs`.

**Modified — tests**

- `Core.UnitTests/AppInfoTests.cs` — 4 cases for what can be said about the running assembly's
  identity, asserted for shape rather than for a literal a release would have to edit.
- `Desktop.UnitTests/ViewModels/MainWindowViewModelTests.cs` — the new dependency threaded through
  `Create` and `EveryDependency_IsRequired` (now 11 arguments), plus 2 cases for the About command.

**Modified — docs**

- `docs/RELEASE.md` — §6's "The application has no About box…" replaced by an About-dialog version and
  build-line check, and a §2 *Manual Linux verification* item for the dialog and its icon. Folded in
  from the documentation freshness sweep, at your choice; `README.md` was offered and declined.

## Deviations & follow-ups

- **`Copyright` was put on `AppInfo`, not read inside the ViewModel.** The plan says only that it
  "comes from `AssemblyCopyrightAttribute`". It does — but from the same place `Version` and
  `BuildSha` do, so all three assembly attributes are read once, in the type whose whole job is
  product identity, and the ViewModel stays a ViewModel.
- **The parsing was extracted into `ProductVersion` rather than left inline on `AppInfo`.** The plan
  puts both properties on `AppInfo` and then asks for tests of `1.1.0+abcdef1234` → `1.1.0` /
  `abcdef1`. Those two cannot both be true of static properties over the running assembly. The record
  struct is the seam that makes that acceptance criterion testable at all.
- **The credits list is 9 rows, not the plan's 7.** Added: **Phosphor Icons** (© 2020 Phosphor Icons,
  MIT — the artwork is redistributed inside `Enigma.Icons.Phosphor` but is not the Enigma libraries'
  copyright) and **Inter** (SIL OFL 1.1 — a different licence family from everything else, with its
  own attribution clause). Deliberately **not** added: the .NET runtime, which the self-contained
  build does redistribute under MIT. That one is the platform rather than a package, and the plan's
  out-of-scope list rules out a licence-text viewer, so it seemed the wrong side of the line — say the
  word and it is one more row.
- **The `ContentDialog`'s own icon looks redundant, and I built it as planned anyway.** The plan asks
  for `IconData = {ei:IconGeometry Info}` — done in C# via
  `PhosphorIconSet.Instance.GetGlyph(PhosphorIcon.Info).ToGeometry()`, because the dialog is
  configured in code rather than in markup. The Enigma template places it in the title area on its own
  row, so a generic outlined "i" sits alone under the word *About* with the application's own 96 px
  icon 20 px below it, and the header band ends up largely empty. It works, and it renders in both
  variants — it just reads as two icons where one was meant. Deleting the `dialog.IconData` line is a
  one-line change if you agree; I did not take it, because the plan named it explicitly.
- **`ei:Icon Kind="Info"` in the toolbar, `PhosphorIcon.Info` in C#.** The latter needs
  `Enigma.Icons.Phosphor`, which arrives transitively through `Enigma.Icons.Avalonia` rather than as a
  direct `PackageReference`. No package was added, per the plan — but it is worth knowing that the
  Desktop project now names a type from a transitive dependency in C#, which its XAML has always done
  implicitly through `Kind="…"`.
- **No sub-agents.** One version property, one ViewModel, one view and one service, all pulling on the
  same two new `AppInfo` members — nothing here separates cleanly enough to be worth the handover.
- **Line endings:** no CRLF/LF churn appeared in this dev's diff, and nothing was done about line
  endings either way, per the workflow's recommendation-only rule. The new PNG is already covered by
  `.gitattributes`' `*.png binary`.
- **Out of scope by the plan, and not done:** an update check, a licence-text viewer, an "open log
  folder" button, a build date, and localization. The splash screen is PHASE02, which depends on this
  phase for `AppInfo.Version` and `Assets/app-256.png` — both now exist.

## Build/test evidence

- `dotnet build -c Debug -p:ArtifactsPath=<temp>` and the same in `-c Release` — both **succeeded with
  zero warnings** (`TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on solution-wide, so a
  warning would have failed the build). The output was redirected to a temp artifacts path because
  your copy of the application, built earlier from `bin/Release`, is running and holds a lock on its
  own `.dll`; the redirect builds the same sources elsewhere rather than closing it.
- `dotnet test` — **1872 passed, 0 failed, 0 skipped**, identically in Debug and Release. The suite
  stood at 1836 before this dev, so it adds **36 cases**.
- `ProductVersionTests` — **15 cases**: a stamped build splitting into version and 7-character sha; an
  unstamped one reporting a null sha; a pre-release version (`1.1.0-rc.1+…`) kept whole; a sha shorter
  than seven kept as it is; `1.1.0+` and `1.1.0+   ` reporting *no* sha rather than an empty one; null
  / empty / blank / `+abcdef1234` all falling back to the assembly version and dropping the sha with
  it; no fallback either landing on `0.0.0`; and surrounding white space trimmed off both halves.
- `AppInfoTests` — **4 new cases**: the version starting with the assembly version's three components
  (so a release bump does not have to edit a test), the version never empty, `BuildSha` being either
  null or 1–7 characters and never `""`, and the copyright carrying the name `Directory.Build.props`
  sets.
- `AboutViewModelTests` — **8 cases**: the default ViewModel matching `AppInfo` on all four values and
  returning a non-empty credits list; `HasBuildSha` true for a sha and false for both null and `""`;
  `HasCopyright` both ways; every credit row carrying a non-empty name *and* licence with no duplicate
  names; and all three non-nullable arguments rejected.
- `AboutViewTests` — **7 headless cases through the real view**, each inside a shown window: the
  display name, version, sha and copyright all present as rendered text; the `Image` resolving a
  non-null `Source` (a missing `AvaloniaResource` fails as the markup loads, so this is the guard on
  the asset existing); the build line visible with the sha in the `SelectableTextBlock`; the build line
  **collapsed** when `BuildSha` is null; the copyright line collapsed when there is none; the
  `ItemsControl`'s `ItemsSource` being the ViewModel's own list; and every credit's name and licence
  appearing as drawn text.
- `MainWindowViewModelTests` — **2 new cases**: the command opening the dialog exactly once, and a
  failure being logged and surfaced through the InfoBar rather than parked on the `AsyncRelayCommand`'s
  faulted task. `EveryDependency_IsRequired` now covers all 11 arguments.
- **Rendered through real Skia, not headless drawing.** A throwaway offscreen probe — in the session
  scratchpad, not in the repository — hosted the real `AboutView` and the real `ContentDialog` under
  `UsePlatformDetect()` and saved PNGs. It reported `AppInfo.Version = 1.0.0`,
  `AppInfo.BuildSha = 00a5176` and the correct copyright, and produced five images: the view in
  **Light** and **Dark** (428×474 each), the same view with no sha (428×**457** — the build line is
  *absent* rather than blank, and the layout closes up by exactly its height), and the whole dialog
  with title, icon, content, `Close` button and scrim in both variants. The application icon draws
  correctly in every one of them, which is what proves the ICO→PNG decode was right and that Skia
  reads the file. Every colour resolved in both variants; there is no `StaticResource` in the view.
- **Not covered, honestly.** `app-256.png` on **linux-x64** could not be verified here — this is a
  Windows machine. The file is a plain 8-bit RGBA non-interlaced PNG that Skia decodes the same way on
  both platforms, but "the same decoder" is an argument, not a run; that check is now a line item in
  `docs/RELEASE.md` §2, which is the release's mandatory Linux pass. Also not verified by running: the
  toolbar button's placement and the click / `Escape` / scrim dismissal. The button is markup, the
  dismissal is `ContentDialog`'s own unchanged behaviour, and your copy of the application was running
  throughout — I did not stop it to drive the real window.
