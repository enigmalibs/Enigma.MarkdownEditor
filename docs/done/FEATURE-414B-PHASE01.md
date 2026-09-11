# FEATURE-414B-PHASE01 — Preview labels & editor default

**Status: DONE** · Phase 01 of 2 · Branch: `feature/feature-414b-phase01-preview-labels`
Plan: `docs/plan/FEATURE-414B.md`

## Summary

Two independent changes, both presentational in the sense that matters: no `AppSettings` property was
renamed, no value was migrated, and nothing new is persisted.

**One vocabulary across the two font groups.** The Preview group called its first two rows *Text
font* and *Text size* while the Editor group, two groups above it in the same scrolling list, calls
the same two things *Font family* and *Font size*. The editor's names are the ones that say what the
value actually is — a family name and a point size — so the preview's rows take them. *Code font*
becomes *Code font family* for the same reason, and by the same token: the row holds a family, and
*Code font size* right beneath it already said "size" out loud.

The two labels that were already right — *Code font size* and *Line height* — are untouched, and so
is every `IntEditor` / `DoubleEditor` / `ComboBox` and every binding behind them. Row order is
unchanged.

One description had to move with the labels: *Code font size* explained itself as "Set on its own
rather than under **the text size**", which was a reference to the row now called *Font size*. It
reads "under the font size above" instead. The other four descriptions never leaned on the old
labels — *Text font*'s said "What prose is set in", which is still exactly the distinction between
that row and the code one — so they are as they were.

**The editor's default font size drops from 18 to 16.** One character in `AppSettings`, plus an XML
doc remark saying what the change deliberately does *not* do: nothing migrates. A `settings.json`
that already holds 18 keeps 18, because a default governs a fresh profile and a file that does not
mention the key — not a value the user can already see and edit in the dialog. The remark is there so
the next reader does not go looking for a migration that was never written. `[Range(6, 72)]` is
unchanged.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Core`**

- `Settings/AppSettings.cs` — `EditorFontSize` default `18` → `16`, and a new `<remarks>` block on
  the property recording that the lowered default is not a migration, and why.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Views/SettingsWindow.axaml` — the Preview `SettingsCardExpander`'s row headers: `Text font` →
  `Font family`, `Text size` → `Font size`, `Code font` → `Code font family`. Plus the one
  description that named the old *Text size* row. Markup only; no binding, control, icon or row order
  changed.

**Modified — `tests/Enigma.MarkdownEditor.Core.UnitTests`**

- `Settings/AppSettingsTests.cs` — the defaults assertion takes 16. Note the `PreviewFontSize`
  assertion three lines below it is *also* 18 and is deliberately unchanged: the preview's default did
  not move.
- `Settings/AppSettingsRoundTripTests.cs` — two changes, for opposite reasons. In
  `APartialFile_KeepsTheDefaultsForEverythingItDoesNotMention` the expected default becomes 16. In
  `ASettingsFileWrittenBeforeThePreviewSettingsExisted_LoadsWithTheirDefaults` the fixture's
  `"EditorFontSize"` becomes **18** and the assertion with it — see *Deviations*.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/SettingsPreviewSectionTests.cs` — `ThePreviewGroup_HasARowPerSetting` takes the new header
  list. This is the test that pins the first acceptance criterion, through the real window: the five
  rows, in order, by the names they now carry.

## Deviations & follow-ups

- **One deliberate deviation, in `AppSettingsRoundTripTests`.** The plan said
  `ASettingsFileWrittenBeforeThePreviewSettingsExisted_LoadsWithTheirDefaults` "reads 16 from a file
  and must keep asserting 16 for a reason that is now 'the file said so', not 'that is the default'".
  Kept literally, that assertion goes vacuous the moment 16 *becomes* the default: it would pass
  unchanged if the loader ignored the key outright, which is precisely what the test exists to catch.
  The file itself already states the standard that would be broken — 30 lines up, on the round-trip
  test: *"Deliberately not 14: a round trip that happens to assert the default proves nothing about
  the key having been written and read back at all."*
  So the fixture's value moved to **18** instead, and the assertion with it. That is not a
  substitution of convenience: the fixture is by its own name a file written *before* this work, and
  18 is exactly what such a file held, because 18 was the default when it would have been written.
  The change therefore keeps the "what the file says is kept" force the plan wanted to preserve
  **and** encodes the phase's second acceptance criterion directly — a profile holding 18 still opens
  at 18 — which no test covered otherwise. The comment above the assertion says all of this. The
  plan's intent (do not blind-replace this 16) is honoured; only its conclusion changed.
- **The plan's own text now names a file that PHASE02 renames.** `docs/plan/FEATURE-414B.md`'s
  PHASE01 section refers to `Views/SettingsWindow.axaml`, which is correct as built and becomes
  `Views/SettingsView.axaml` in PHASE02. Left as-is: the plan records what each phase was, not a live
  index.
- **`RELEASENOTES.md` names a label this phase renamed.** The shipped v1.2.0 notes say the code size
  row "sits directly under *Code font* in the preview group of Settings" — a row now called *Code font
  family*. Deliberately not edited: those notes describe what 1.2.0 shipped, when the row *was* called
  *Code font*, and rewriting a released document to match later code makes it wrong about its own
  release. The rename belongs in the next release's notes. Raised in the documentation freshness sweep
  and declined there.
- **`README.md` needed nothing.** It describes both font groups at length but never quotes a row
  label and never states the editor's default size, so neither half of this phase touched it.
  Confirmed by grep, not by assumption.
- **Not verified by eye.** The label change is a reading, not a measurement, and the acceptance
  criteria are met by test: the five headers in order through the real window, and each row still
  bound to the property it was bound to. What a human has not done is look at the two groups side by
  side and agree the shared vocabulary reads better — the intent of the phase, which only the user can
  confirm. Doing so means backing up and restoring the real `%APPDATA%\Enigma.MarkdownEditor`, as
  recorded for `BUG-7AB2-PHASE05`.
- **No line-ending noise.** All six touched files are LF, as `.gitattributes` (`* text=auto eol=lf`)
  requires, and every diff is line-scoped with no whole-file churn. Nothing to recommend.
- **Follow-up:** none of substance. PHASE02 is the next dev and is unaffected by anything here beyond
  the file it renames.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -v normal`: **succeeded, 0 errors, 0 warnings**, and no
  `AVLN*` diagnostic anywhere in the normal-verbosity log. XAML *was* touched this phase, so the
  `AVLN` sweep was the real check the plan asks for and not a formality.
- `dotnet test Enigma.MarkdownEditor.slnx`: **1968 passed, 0 failed, 0 skipped**
  (`Enigma.MarkdownEditor.Core.UnitTests` + `Enigma.MarkdownEditor.Desktop.UnitTests`). No test cases
  were added or removed — 1968 before and after. This phase changes what four existing assertions
  expect rather than adding behaviour to cover, which is the honest shape of a labels-and-a-default
  change; both acceptance criteria that needed a test already had one pointed at them.
- **The changed assertions do fail against the unchanged code**, which is what makes them the
  criteria rather than a description of them. Verified by reverting `AppSettings.cs` and
  `SettingsWindow.axaml` alone — the two test files left as built — and re-running the whole suite:
  **3 failed, 1965 passed**, and the 3 are exactly
  `AppSettingsTests.Defaults_AreTheDocumentedOnes`,
  `AppSettingsRoundTripTests.APartialFile_KeepsTheDefaultsForEverythingItDoesNotMention` and
  `SettingsPreviewSectionTests.ThePreviewGroup_HasARowPerSetting`. Nothing else moved, which is the
  useful half of the result: a default and three labels, and no behaviour behind them.
  The fourth changed assertion — the 18-from-a-file one — passes against the old code as well, and
  necessarily so: it asserts what the file says, and the file now says what the old default was. It is
  not a regression detector for this change but the standing guard on the criterion going forward,
  which is what it was moved to 18 to become.
- **Acceptance criteria, one by one.**
  - The Preview rows read `Font family`, `Font size`, `Code font family`, `Code font size`,
    `Line height`, in that order, each still editing what it edited before ✓ —
    `SettingsPreviewSectionTests.ThePreviewGroup_HasARowPerSetting` asserts the ordered header list
    through a real shown window, and `EachRow_IsWiredToItsOwnSetting` (untouched, still green) asserts
    each row's control is the same control bound to the same property.
  - A fresh profile starts the editor at 16pt ✓ — `AppSettingsTests` on a bare `new AppSettings()`,
    and `AppSettingsRoundTripTests.APartialFile_…` through a real file that omits the key.
  - A profile whose file holds `"EditorFontSize": 18` still opens at 18 ✓ —
    `ASettingsFileWrittenBeforeThePreviewSettingsExisted_…`, now the test that says so.
  - No `AppSettings` property name changes; an existing `settings.json` round-trips unchanged ✓ — no
    identifier in `AppSettings` was touched, and `WrittenSettings_AreReadBackByAFreshConfiguration`
    plus `TheWrittenFile_IsHandEditableJson` pass untouched.
  - Build clean with zero warnings, no `AVLN`, whole suite passes ✓ — as above.
