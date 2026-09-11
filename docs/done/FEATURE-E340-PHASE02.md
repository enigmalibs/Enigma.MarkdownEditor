# FEATURE-E340-PHASE02 — Preview typography & Settings "Preview" section

Branch: `feature/feature-e340-phase02-preview-typography` · Plan: `docs/plan/FEATURE-E340.md`

## Summary

The preview is now set in type the reader chose. Four settings — a prose family, a size, a code family and a line height — reach the renderer as one `MarkdownPreviewTypography` record, and the Settings window has a **Preview** group beside **Editor** to pick them from.

The renderer's type scale used to be three `const`s: a monospace comma stack, a body size of 14, and six absolute heading sizes. All three are gone. The families follow PHASE01's rule — one installed family name or empty for "automatic" — and both automatics are resolved by `IFontCatalog` in `MarkdownPreviewViewModel`, because a control has no business probing installed fonts. Prose falls back to the font the interface is drawn in; code to the best fixed-width family this machine has. The stored value is left exactly as the file holds it either way, so a settings file carried between two machines still names the font the other one has.

The heading scale had to stop being absolute. At an 18pt body — what a fresh settings file now gives the preview — an h3 fixed at 17 would have been a heading set smaller than the paragraph under it. `HeadingFontSize` takes the body size and scales the six reference sizes against 14, so at 14 the document comes out at exactly the sizes it always did and at 18 it gives 31/26/22/20/19/18.

Colour was deliberately left where it was. Every brush is still a `DynamicResource` key on `MarkdownPreviewTheme`, so a theme switch still repaints without rebuilding the tree; type is the opposite — a size decides how much room a block takes, which decides where every block after it sits — so assigning `MarkdownPreview.Typography` re-renders, exactly like assigning `Markdown`. That is why the two are separate rather than one "theme" object.

The settings reach the pane through the seam the editor already uses: the settings window closes, `MainWindowViewModel` tells the panes, and `MarkdownPreviewViewModel.OnSettingsChanged` raises one property change rather than four — because each one the renderer heard would be a walk of the whole document.

## Files/modules touched

Created:
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownPreviewTypography.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Controls/MarkdownPreviewTypographyTests.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/SettingsPreviewSectionTests.cs`

Modified:
- `src/Enigma.MarkdownEditor.Core/Settings/AppSettings.cs` — `PreviewFontFamily` and `PreviewCodeFontFamily` (`[Required(AllowEmptyStrings = true)]`, `[MaxLength(200)]`, empty by default), `PreviewFontSize` (`[Range(6, 72)]`, 18), `PreviewLineHeight` (`[Range(1.0, 2.5)]`, 1.5). `AppSettingsValidator` is untouched: all four are range/length rules, which stay on the properties.
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownPreviewTheme.cs` — `MonospaceFontFamily` and `BodyFontSize` retired; `HeadingFontSize(int)` becomes `HeadingFontSize(int, double)` over the new `HeadingSizesAtReferenceBody` / `ReferenceBodyFontSize`. The brush keys, `BrushKeys` and `BindBrush` are unchanged.
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownVisualBuilder.cs` — takes the typography; every size and family it sets now comes from it. `ErrorBlock` is still static and takes the record as a second argument.
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownCodeBlock.cs` — new `Typography` styled property; the fence's family and size are applied from it rather than fixed in the constructor.
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownPreview.cs` — new `Typography` styled property; sets the pane's own `FontFamily`/`FontSize` and re-renders.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/MarkdownPreviewViewModel.cs` — takes `IConfig<AppSettings>` and `IFontCatalog`; exposes `Typography` and `OnSettingsChanged()`.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/MainWindowViewModel.cs` — the settings-close seam now tells the preview pane as well as the editor pane and the file tree.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/SettingsWindowViewModel.cs` — `PreviewFontChoices`/`PreviewFontChoice`/`PreviewFontFamily`, `PreviewFontSize`, `PreviewCodeFontChoices`/`PreviewCodeFontChoice`/`PreviewCodeFontFamily`, `PreviewLineHeight`. `BuildEditorFontChoices` generalised to `BuildFontChoices`, shared by all three pickers.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/FontChoice.cs` — `AutomaticReading` / `AutomaticReadingLabel`, for a picker where "automatic" means the reading font rather than the best monospace.
- `src/Enigma.MarkdownEditor.Desktop/Views/SettingsWindow.axaml` — the **Preview** group and its four rows.
- `src/Enigma.MarkdownEditor.Desktop/Views/MainWindow.axaml` — `Typography="{Binding Preview.Typography}"`.
- `README.md` — one bullet added under **Preview** for the four settings (documentation freshness sweep). Nothing in that section had become wrong; it was simply the only feature section left saying nothing about type, after PHASE01 gave **Editor** its own font bullet.
- Test files listed under *Build/test evidence*.

## Deviations & follow-ups

1. **The heading scale is stored as the six reference sizes and divided by 14 at the end, not as six ratios multiplied by the body size.** The plan wrote the ratios as `[1.714, 1.429, 1.214, 1.107, 1.036, 1.0]`. Both orderings describe the same scale, but neither the rounded decimals nor the exact fractions satisfy the acceptance criterion as stated: at body 14, `24d/14 * 14` is 24 but `14.5d/14 * 14` is 14.500000000000002, and the rounded decimals put h1 at 23.996. Computing `reference * body / 14` gives back exactly 24/20/17/15.5/14.5/14 at body 14, which is what "unchanged from today" has to mean if a test is going to assert it. Same numbers at 18 as the plan predicted: 31/26/22/20/19/18.

2. **"Code spans, fenced blocks and table cells all follow `PreviewCodeFontFamily`" is implemented as: code spans and fenced blocks follow the code family wherever they appear, including inside a table cell; the cells themselves are prose.** A table of words set in the code font is not a thing anyone asks a markdown preview for, and reading the criterion that way would have made `PreviewFontFamily` not apply to tables. `ATableCell_ReadsAsProseWhileTheCodeInsideItReadsAsCode` asserts both halves, so whichever reading was meant is covered.

3. **An empty family in the record means "inherit", and `MarkdownPreviewTypography.Default` names neither family.** "The best monospace font here" is not a name that can be written down in a control — it needs the installed fonts, which is `IFontCatalog`'s business — so the record carries the answer rather than the question, and its fallback instance inherits rather than guessing. The application always supplies a resolved record; `Default` is what a preview built by hand (a designer, a test) gets, and it reproduces the pane's pre-change rendering exactly apart from the line height.

4. **Line height is applied to the document's text and not to the preview's own asides.** Paragraphs, headings, list markers, table cells, raw-HTML literals and fenced blocks get it, each at their own size; the front-matter chip, the image placeholder and the inline error block do not. Those three are the renderer talking about the document rather than the document itself, and spacing them out reads as a layout bug.

5. **A code span takes the code family but not the code size.** `CodeFontSize` is a point under the body, which is right for a fenced block sitting on its own but wrong for a run inside a sentence — it would ride visibly low on the line it is part of. Asserted, so it does not drift.

6. **A latent trap in the headless test platform, found the hard way and worked around in `PreviewTree`.** Avalonia's headless platform has one synthetic typeface and hands it back for any family name asked of it — but only for a name that was *asked*. A visual that inherits its family measures sensibly; the same visual with a family explicitly assigned comes out of the same measure pass eighteen lines tall, and a wrapped paragraph containing a hard line break never converges at all — a live lock that took the suite to 18 GB of working set before it was killed. Nothing about it is a defect in this code: the application only ever names families the catalog says are installed. `PreviewTree.Typography` therefore names no family, and `PreviewTree.NamedFamilies` is offered separately for the tests that assert *which* family a visual asks for rather than measuring one. The remark on `PreviewTree.Typography` records this so the next person does not rediscover it.

7. **Still a manual reading:** that the four settings *look* right — a document set in a chosen face at a chosen size with chosen leading, and the Preview group's own rows. The tests assert that each value reaches the visual that consumes it and that the settings window's four controls are bound to the four properties; what that adds up to on screen is not something the headless platform can answer.

8. **Follow-up, still out of scope (carried from PHASE01):** `SettingsWindow.axaml`'s `SelectableTextBlock.path` style still sets `FontFamily` to a comma stack (`"Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"`) for the two read-only paths in the Advanced group. It has the same defect this feature retired from the settings themselves; the cost is cosmetic, and it is the last of the three the item started with.

9. **Line endings:** no CRLF/LF churn in this dev's diff — every changed file shows only the lines that changed. No action taken (recommendation-only per the workflow).

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx` — **succeeded, 0 warnings** (the solution builds with `TreatWarningsAsErrors`). The Settings and Main windows use compiled bindings (`x:DataType` + `AvaloniaUseCompiledBindingsByDefault`), so all six new bindings — including `DoubleEditor.Value` against `double?` and `MarkdownPreview.Typography` against the record — are checked by that build rather than at runtime.
- `dotnet test Enigma.MarkdownEditor.slnx` — **1816 passed, 0 failed, 0 skipped** (Core 1217, Desktop 599). The pre-change baseline on this branch's parent was 1770.

Tests added or updated:
- `Core.UnitTests` — `AppSettingsTests`: the four new defaults, plus `TheDefaultPreviewFontFamilies_AreEmptyForTheSameReason`. `AppSettingsValidatorTests` (+13 cases): both ranges accepted at their bounds and rejected outside them, reported against their own property; an over-long family rejected for either key. `AppSettingsRoundTripTests`: the four keys round-tripped, and `ASettingsFileWrittenBeforeThePreviewSettingsExisted_LoadsWithTheirDefaults` — a file holding exactly the pre-item key set loads without quarantine and picks the new defaults up.
- `Desktop.UnitTests` — `MarkdownPreviewTypographyTests` (11, new): the prose family on the pane and inherited by blocks, the body size and line height on prose, a heading's line height taken at its own size, a code span in the code family but not the code size, a fence in both, a table cell as prose with its code span as code, an assignment rebuilding the tree, no heading smaller than the body at 18, empty families leaving visuals inheriting, the `Default` record, and a null typography falling back rather than throwing. `SettingsPreviewSectionTests` (3, new): the Preview group between Editor and Behaviour, its four rows, and each row's control bound to its own property. `MarkdownPreviewThemeTests` (+8): the scale exact at 14 for all six levels, proportional at 18 with no heading under the body, and the existing shape/clamp tests moved onto the two-argument signature. `MarkdownPreviewViewModelTests` (+4): both automatics resolved, an installed family used as written, an uninstalled one rendered with the fallback and left in the file, and `OnSettingsChanged` raising one change. `SettingsWindowViewModelTests` (+6): the prose picker offering every family with the reading label, the code picker offering only the fixed-width ones, an uninstalled family marked and selected in both, picking and clearing, a null selection leaving the file alone, and the numeric editors' empty state. `MainWindowViewModelTests` (+1): closing the settings window reaches the preview pane. `PreviewTree`, `MarkdownPreviewTests` and `MarkdownCodeBlockTests` updated for the typography parameter (see *Deviations* 6).
