# FEATURE-2877 — Update NuGet packages

## Summary

Every package is now on its latest stable version except xunit.v3, which is held at 3.2.2 for the
reason below:

- the version-coupled Avalonia set moved 12.1.2 → **12.1.4**: Avalonia, Avalonia.Desktop,
  Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Headless and Avalonia.Headless.XUnit;
- Enigma.Avalonia.Desktop moved 1.2.0 → **1.3.0**.

No code change was needed. Enigma.Avalonia.Desktop 1.3.0 is API- and theme-compatible with 1.2.0, as
its release notes state.

## Files/modules touched

- Modified:
  - `Directory.Packages.props`: the pins, the Headless.XUnit comment, and the xunit.v3 hold-back
    comment;
  - `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj` (comment: coupled set
    12.1.4);
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Enigma.MarkdownEditor.Desktop.UnitTests.csproj`
    (comment: coupled set 12.1.4);
  - `docs/roadmap.md`, `docs/plan/FEATURE-2877.md`.

## Deviations & follow-ups

- **xunit.v3 held at 3.2.2, not 4.0.2.** The plan anticipated this. With 4.0.2, the build was clean
  but **275 Desktop tests failed at discovery**:

  `System.MissingMethodException: Method not found: '… Xunit.v3.TestIntrospectionHelper.GetTestCaseDetails(…)'
  at Avalonia.Headless.XUnit.AvaloniaFactDiscoverer.CreateTestCase(…)`

  Avalonia.Headless.XUnit 12.1.4 is compiled against xunit v3 3.x. That is a third-party binary
  break, with no fix on this side short of dropping `[AvaloniaFact]`. CPM keeps one version for the
  solution, so the Core tests stay on 3.2.2 too.
- **Follow-up:** move xunit.v3 to 4.x once an Avalonia.Headless.XUnit built against it ships. The
  props comment says the same.
- `--outdated` now lists xunit.v3 4.0.2 alone, for both test projects.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3621 passed, 0 failed, 0 skipped**.
- Fix cycles: **1**, the xunit.v3 hold-back.
  - First run, with xunit 4.0.2: 3346 passed and 275 failed, all of them `[AvaloniaFact]` /
    `[AvaloniaTheory]` discovery.
  - After the cycle: green.
- `dotnet list package`: requested = resolved for Avalonia* 12.1.4, Enigma.Avalonia.Desktop 1.3.0 and
  xunit.v3 3.2.2.
- `dotnet list package --outdated` after the change:

  ```text
  Enigma.MarkdownEditor.Core:              no updates
  Enigma.MarkdownEditor.Desktop:           no updates
  Enigma.MarkdownEditor.Core.UnitTests:    xunit.v3 3.2.2 → 4.0.2 (held, see above)
  Enigma.MarkdownEditor.Desktop.UnitTests: xunit.v3 3.2.2 → 4.0.2 (held, see above)
  ```

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| xunit.v3 4.0.2 breaks the headless harness | Hold at 3.2.2 for the solution, with the reason in a props comment | Third-party binary break; a per-project `VersionOverride` would break the CPM single-version rule for the Core tests only |
| Documentation sweep (§7) | No change | No doc names the package versions; the remaining `1.2.0` mentions in code comments are historical facts |
