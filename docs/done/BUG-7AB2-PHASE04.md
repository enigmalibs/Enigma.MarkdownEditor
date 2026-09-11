# BUG-7AB2-PHASE04 — Editor font family never applied

**Status: DONE** · Phase 04 of 5 · Branch: `bugfix/bug-7ab2-phase04-editor-font-family`
Plan: `docs/plan/BUG-7AB2.md`

## Summary

Choosing a font family for the editor did nothing, restart or no restart, while the font *size* right
beside it worked and both preview families worked. The cause is the one the plan named, and it was
reproduced before anything was changed: `DocumentManagerViewModel.FontFamily` was a `string`, the
`TextEditor.FontFamily` it is bound to is an `Avalonia.Media.FontFamily`, and compiled bindings — on
solution-wide — do not run the reflection binding's implicit string conversions. The value simply
never arrived. Read off the real control before the fix, the editor's `FontFamily.Name` was
`$Default`: not the fallback family, not the chosen one, but the inherited default of a property
nothing ever wrote to. Size worked because `int` → `double` is a numeric conversion a compiled binding
*does* handle, which is exactly the reported split in behaviour.

The fix is the one-line type change the plan called for: the property is now an
`Avalonia.Media.FontFamily`, built from the same resolved name as before. The resolution rules,
`IFontCatalog`, the fallback for an uninstalled family and what the settings file holds are all
untouched — only the type crossing the binding changed. The XAML is untouched too, because the
binding type-matches once the property does.

The interesting part of this bug is that it could not fail loudly. A compiled binding whose source and
target types disagree is not a build error and not a runtime exception; it is a binding-error trace
line and a control left on its default. That is what made the ViewModel-only tests useless here — they
were asserting the string the ViewModel produced, which was always correct — and it is why this phase
is tested through the real pane in a shown window instead.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `ViewModels/DocumentManagerViewModel.cs` — `FontFamily` is now
  `public FontFamily FontFamily => new(_fonts.ResolveEditorFamily(_settings.Value.EditorFontFamily));`
  and its remarks gained a `<para>` saying why it is a `FontFamily` and not the name it is built from,
  so the next person is not tempted to simplify it back to a string. One `using` added
  (`Avalonia.Media`).

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/EditorAppearanceTests.cs` — **4 new tests** through the real pane: a chosen family is the one
  the control renders with (the regression test); changing the setting moves the control onto the new
  family; with nothing chosen the control shows the resolved monospace fallback and not the interface
  font; a stored family this machine does not have leaves the control on the fallback and the file
  unchanged. The class doc was widened from "the theme half of the editor's appearance" to cover both
  halves, with a note on why the font half needs a real control and the theme half does not.
- `Views/TabStrip.cs` — three additions to the harness: `Settings` (the settings behind the shown pane),
  `Fonts` (the imaginary machine's catalog) and `ApplySettings()`, which republishes the appearance and
  settles the layout — what closing the settings window amounts to. Two `using`s added.
- `ViewModels/DocumentManagerViewModelTests.cs` — the three existing assertions that read the family as
  a string now read `.Name`, as planned. No case was added, removed or weakened.

Nothing was added to `Enigma.MarkdownEditor.Core` or its test project. No converter class, no new
dependency, no settings change, no XAML change.

## Deviations & follow-ups

- **The real-view tests went into `EditorAppearanceTests`, not `EditorTabStripTests`.** The plan left
  the choice open ("whichever the harness fits"). `EditorTabStripTests` is explicitly about the strip;
  a font family is the editor's appearance, which is what `EditorAppearanceTests` is named for. The
  harness is the same `TabStrip` either way.
- **Two more tests than the plan asked for.** The plan named the chosen-family and the changed-family
  cases. The fallback cases (nothing chosen, and a stored family this machine lacks) were added
  alongside them because they are the two paths that produce a family the *user* never typed, and the
  ViewModel-level versions of them prove only that the ViewModel resolved correctly — which was never
  in doubt. Now both are asserted where it matters, on the control.
- **Follow-up, out of scope — the same defect is still live in the Settings window's font pickers.**
  The sweep the plan asked for (step 5) found exactly one other instance of the pattern, in three
  places: `FontChoice.PreviewFamily` is a `string`
  (`src/Enigma.MarkdownEditor.Desktop/ViewModels/FontChoice.cs:56`), bound to `TextBlock.FontFamily`
  inside `DataTemplate`s that carry `x:DataType="vm:FontChoice"` — `Views/SettingsWindow.axaml:77`,
  `:118`, `:141`, the editor-font, preview-font and preview-code-font dropdowns. Identical mechanism,
  identical consequence: each row in those lists is meant to preview itself in the font it names, and
  cannot. The fix is the same one-line type change on `PreviewFamily`. Left alone here per the plan
  ("a recommendation in the completion doc, not a change") and **not verified by a test**, since
  writing one would be building the out-of-scope fix. Worth its own small bug item.
- **A stale comment left in place, same neighbourhood.** `SettingsPreviewSectionTests`' class remarks
  claim that because the bindings are compiled, "a wrong property name or a wrong type is a build
  failure rather than a runtime one". The first half is true; the second half is what this phase
  disproves. Not corrected here — it belongs with the follow-up above, which is the change that would
  make someone read it.
- **Nothing else in the sweep.** Every other `{Binding}` onto a typed target across the eight `.axaml`
  files is an `ItemsSource` onto a collection, which matches. No brush, geometry, thickness or image
  source is bound from a `string` anywhere.
- **No line-ending noise.** The four files touched are uniformly CRLF like the rest of the tree, and
  the diff carries no whole-file churn. Nothing to recommend.

## Build/test evidence

- `dotnet build --no-incremental -v normal` (Debug): **succeeded, 0 errors, 0 warnings**, and no
  `AVLN*` diagnostic in the normal-verbosity log. No XAML was changed, but the check was run anyway
  because the binding on the other side of it did.
- `dotnet test` across the solution: **1951 passed, 0 failed, 0 skipped**
  (`Enigma.MarkdownEditor.Core.UnitTests` + `Enigma.MarkdownEditor.Desktop.UnitTests`). **4 of them are
  new** (1947 before this phase).
- **The defect was reproduced first, on the real control.** Against the unfixed code all four new tests
  failed, and every one of them failed the same way — `Expected: "Another Mono" / Actual: "$Default"`.
  `$Default` is the tell: the control was never written to at all. Against the fix, all four pass in
  2.5 s.
- **Manual, in the running application, twice.** Launched Debug over a scratch markdown file holding a
  pangram, `ILlij1 O0o`, digits and `== != -> =>`, with vim on (the default, and the user's own
  setting).
  - `EditorFontFamily: "Courier New"`, Dark: the editor pane renders in Courier New — slab serifs, the
    serifed `1`, typewriter spacing — while the interface around it stays on the UI sans. Captured.
  - Restarted with `EditorFontFamily: "JetBrainsMono NF"` (the family in the reporter's own settings
    file, i.e. the exact case reported) and `Theme: "Light"`: the editor renders in JetBrains Mono NF,
    unmistakable from its ligatures — `==` joined, `!=` as `≠`, `->` as `→`, `=>` as `⇒` — and the
    dotted zero. Captured.
  - Two different chosen families, two different renderings, each surviving a restart, in both theme
    variants, under vim. Before the fix the editor rendered in neither.
  - The application's real `settings.json` and `session.json` were copied aside before this and
    restored byte-for-byte afterwards; the scratch file lives outside the repository.
- **"As soon as the settings window closes" is covered by test rather than by hand.**
  `ChangingTheEditorFont_MovesTheControlOntoTheNewOne` drives exactly that path —
  `OnSettingsChanged` over a real, shown `EditorPaneView` — and asserts the control moved. Driving the
  settings window's own combo box by simulated clicks would have exercised the picker, which is not
  what this phase changed, and would have run into the very follow-up recorded above.
