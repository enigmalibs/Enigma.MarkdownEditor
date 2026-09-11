# FEATURE-E340-PHASE01 — Editor font picker, defaults & line numbers

Branch: `feature/feature-e340-phase01-editor-font` · Plan: `docs/plan/FEATURE-E340.md`

## Summary

The editor's font is now a choice made from the fonts this machine actually has, and the default lands on a code font at a readable size.

`AppSettings.EditorFontFamily` stored a comma-separated fallback stack, which Avalonia never resolved as one — it reads the first name and ignores the rest, and a stack that fails to convert leaves the editor on the inherited proportional UI font. That is the reported "default font has variable character size", and it is fixed at the root: the setting now holds a single family name, and empty means "the best monospace family installed here", resolved at runtime.

Because `Core` holds `AppSettings` and must stay free of Avalonia, the resolution lives in `Desktop` as a new `IFontCatalog`. It enumerates the platform's families once, classifies each as fixed-width or not by comparing the advance width of `i` against `W`, and answers the three questions the rest of the application has: what to offer in a picker, what "automatic" means, and what to render with when the stored family is not here.

The Settings window's free-text font box is now a `ComboBox` of installed monospace families, each row drawn in the font it offers, with "Automatic (best available monospace)" first. A stored family this machine does not have — an old comma stack, or a font from another machine — is listed and pre-selected as `<value> (not installed)`, so closing the window cannot silently replace it.

`EditorFontSize` default moves from 14 to 18. Line numbers get 4px of clear space on each side.

## Files/modules touched

Created:
- `src/Enigma.MarkdownEditor.Desktop/Services/IFontCatalog.cs`
- `src/Enigma.MarkdownEditor.Desktop/Services/FontCatalog.cs`
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/FontChoice.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fakes/TestFonts.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/FontCatalogTests.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/EditorLineNumbersTests.cs`

Modified:
- `src/Enigma.MarkdownEditor.Core/Settings/AppSettings.cs` — `DefaultEditorFontFamily` retired; `EditorFontFamily` defaults to empty and relaxes `[Required(AllowEmptyStrings = false)]` to `[Required(AllowEmptyStrings = true)]` (empty accepted, `null` still rejected), `[MaxLength(200)]` unchanged; `EditorFontSize` default 14 → 18.
- `src/Enigma.MarkdownEditor.Desktop/Extensions/ServiceCollectionExtensions.cs` — `IFontCatalog` registered as a lazy singleton via `FontCatalog.ForCurrentSystem()`.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/DocumentManagerViewModel.cs` — takes `IFontCatalog`; `FontFamily` now resolves the stored value instead of passing it through.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/SettingsWindowViewModel.cs` — takes `IFontCatalog`; adds `EditorFontChoices` / `EditorFontChoice`; `EditorFontFamily` kept as the canonical string the picker writes through.
- `src/Enigma.MarkdownEditor.Desktop/Views/SettingsWindow.axaml` — font row is a `ComboBox` with a per-row font preview.
- `src/Enigma.MarkdownEditor.Desktop/Views/EditorPaneView.axaml.cs` — `LineNumberSpacing` applied to the `LineNumberMargin`.
- `README.md` — one bullet added under **Editor** for the font picker (documentation freshness sweep; the file named no font and nothing in it had become wrong, so this is an addition rather than a correction).
- Test files listed under *Build/test evidence*.

## Deviations & follow-ups

1. **`FontCatalog` takes its font list as a constructor argument**, with the platform probe factored out into `FontCatalog.ForCurrentSystem()`. The plan described one class that enumerates `FontManager.Current.SystemFonts` itself. It had to be split: Avalonia's headless platform installs `HeadlessFontManagerStub`, which reports a single synthetic family, so a catalog that could only be built from the running platform could not have its fallback order asserted at all — and on a real machine it would only ever assert whatever fonts that machine happened to have. The plan's acceptance criterion ("`FontCatalog` classification and default probing") is met over a fixed, made-up font list; the probe itself is covered by an `[AvaloniaFact]` asserting the properties that hold on any machine.

2. **Line-number spacing is applied in code, in `EditorPaneView`'s constructor**, not as a XAML style. The plan left the mechanism open pending whether the margin honours `Margin` inside `TextArea.LeftMargins`. It does — but `ShowLineNumbers` builds the margin in code, so a locally-assigned value and a style setter are in a race a style cannot win. Assigning it directly is the one way that is deterministic.

3. **`DocumentManagerViewModel` and `SettingsWindowViewModel` both gained an `IFontCatalog` constructor parameter.** Not called out in the plan, but implied by it — resolution happens in `Desktop`, and these are the two consumers.

4. **Not covered by an automated test, and confirmed instead by a standalone probe against the real Windows font manager** (throw-away, not committed): the classification over an actual font list. It reported 203 installed families, 22 classified monospace — Cascadia Code/Mono, Consolas, Courier New, the JetBrainsMono variants, Lucida Console/Sans Typewriter, MingLiU/SimSun/MS Gothic/NSimSun, OCR A, Ubuntu Mono — with Segoe UI, Arial, Times New Roman and Calibri all correctly rejected, `DefaultMonospaceFamily` = Cascadia Mono, and the old comma stack resolving to Cascadia Mono.

5. **Still a manual reading:** that the picker's rows *look* right (each drawn in its own font) and that the line numbers *look* like they have breathing room. Avalonia's headless drawing platform never measures a margin inside `LeftMargins` — the column comes out of a layout pass with a desired size of zero — so the test asserts the margin value survives a real layout pass and no more. Noted in the test class's own remarks.

6. **Follow-up, out of scope here:** `SettingsWindow.axaml` still has a `SelectableTextBlock.path` style whose `FontFamily` is the same kind of comma stack this phase retired from the settings (`"Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"`). It renders the two read-only paths in the Advanced group, so the cost is cosmetic, but it has the same defect. `MarkdownPreviewTheme.MonospaceFontFamily` is the same case and is PHASE02's business by plan.

7. **Line endings:** no CRLF/LF churn observed in this dev's diff; no action taken (recommendation-only per the workflow).

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx` — **succeeded, 0 warnings** (the solution builds with `TreatWarningsAsErrors`).
- `dotnet test Enigma.MarkdownEditor.slnx` — **1770 passed, 0 failed, 0 skipped** (both suites green; the pre-change baseline was not recorded, so no delta is claimed).

Tests added:
- `Core.UnitTests` — `AppSettingsTests`: defaults assert empty family and size 18; `TheDefaultEditorFontFamily_IsEmptyRatherThanAFamilyNameOrAStack` replaces the retired-constant test. `AppSettingsValidatorTests`: empty family accepted, an uninstalled family (including the old comma stack) accepted, a 201-character family rejected. `AppSettingsRoundTripTests`: the round-trip's font size moved off 18 so it is not asserting the default against itself; the partial-file default is 18.
- `Desktop.UnitTests` — `FontCatalogTests` (17 cases): sorting/dedup, monospace-only listing, a monospace name with no family behind it, the preferred-list order, fallback to any fixed-width family, fallback to the interface font, `IsAvailable` case-insensitivity, resolution of empty/null/uninstalled/comma-stack, and two `[AvaloniaFact]`s over the real probe. `SettingsWindowViewModelTests` (+7): the picker's shape and previews, a fresh file starting on automatic, an uninstalled stored family shown marked and selected and left in the file, no duplicate row for an installed family, picking a font, picking automatic, and a null selection not overwriting the setting. `DocumentManagerViewModelTests` (+2): no font chosen resolves to the best monospace; an uninstalled stored font renders with the fallback and stays in the file. `EditorLineNumbersTests` (2): the margin survives a real layout pass, horizontal only. `ServiceCollectionExtensionsTests` (+1): `IFontCatalog` is a singleton.
