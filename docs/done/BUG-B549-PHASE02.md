# BUG-B549-PHASE02 — Settings dialog chrome

**Status: DONE** · Phase 02 of 2 · Branch: `bugfix/bug-b549-phase02-settings-chrome`
Plan: `docs/plan/BUG-B549.md`

## Summary

Three complaints against the settings content dialog — a wide empty gutter down its left, too much
padding, and expanders that do not sit well on the dialog's own background — fixed exactly as the
plan specifies, entirely inside the app's own view and dialog service.

- **The gutter is gone** because the heading icon is gone. The library's `ContentDialog` template
  gives `IconData` its own grid column (a 48 × 48 `Viewbox` plus a 16px margin) ahead of the content
  presenter, and with it in place the dialog wanted 760 + 48 + 48 + 16 = 872px against the host's
  `DialogMaxWidth="860"`, so the view was also being squeezed by ~14px. Without it the dialog asks
  for 760 + 24 + 24 = 808px and gets the width it declares.
- **One layer of padding instead of two.** The root `StackPanel`'s `Margin="20"` sat on top of the
  template's own `Margin="24"`; it is gone, which also brings the view into line with `AboutView`,
  `FirstRunView` and `WorkspaceManagerView`, none of which set an outer margin.
- **The depth order is the right way round.** The dialog card is painted `EnigmaSurfaceHigh` and the
  `SettingsCardExpander`s on it are `EnigmaSurface` — lighter beneath darker, in both variants, which
  is what read as "does not fit". The groups now sit in a `Border` painted `EnigmaBackgroundBrush`
  (the app's ground colour), inset by 16, with the same 8px corner radius as the dialog card and the
  same 1px `EnigmaBorderSubtleBrush` edge the settings cards carry. In dark mode the well is darker,
  as asked; in light mode it is lighter, which is the correct direction there.

Nothing outside the app changed: no `App.axaml` style, no `/template/` selector into the library's
template, and no change to `Enigma.Avalonia.Desktop`. Every brush is a `{DynamicResource Enigma…Brush}`.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Services/SettingsDialogService.cs` — 10 lines removed, none added. The `dialog.IconData = Icon;`
  line, the `private static readonly Geometry Icon` field with its doc comment, and the three
  now-unused using directives (`Avalonia.Media`, `Enigma.Icons.Avalonia`, `Enigma.Icons.Phosphor`).
  `Title`, `CloseButtonText` and the save-after-close sequence are untouched; `AboutDialogService`
  and `FirstRunDialogService` keep their own icons.
- `Views/SettingsView.axaml` — the root `StackPanel` is wrapped in a `Border`
  (`Background="{DynamicResource EnigmaBackgroundBrush}"`,
  `BorderBrush="{DynamicResource EnigmaBorderSubtleBrush}"`, `BorderThickness="1"`,
  `CornerRadius="8"`, `Padding="16"`), and the panel loses `Margin="20"` while keeping
  `Spacing="12"`. `Width="760"` stays on the `UserControl`; all five groups keep `IsExpanded="True"`.
  The existing "no ScrollViewer of its own" comment gains the well's consequence, and a new comment
  above it records why the well exists.

**Added — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/SettingsChromeTests.cs` — five `[AvaloniaFact]`s over the real `SettingsView` and a real
  `SettingsViewModel`, in the shape of `SettingsPreviewSectionTests`: the well's `Padding`,
  `CornerRadius` and `BorderThickness`; its `Background` and `BorderBrush` being the *same instances*
  the theme resolves for the current variant (a hard-coded brush would compare equal in one variant
  and then stop following the switch); the background actually changing across a Dark → Light switch;
  the group panel carrying no margin of its own; and the five groups still being `Appearance`,
  `Editor`, `Preview`, `Behaviour`, `Advanced` in order, now re-checked through the new root.

**Modified — workflow artifacts**

- `docs/roadmap.md` — `PHASE02` → `DONE` and `BUG-B549` → `DONE` (final phase). The Status column
  re-padded from 11 to 9 characters across the whole table, per the workflow's table-formatting rule:
  with no `IN PROGRESS` row left, `ABANDONED` is now the widest status.
- `docs/plan/BUG-B549.md` — item status → `DONE`, PHASE02 status → `DONE`.

## Deviations & follow-ups

- **No deviation from the plan's design.** The icon removal, the well's five properties, the dropped
  margin and the three assertions the plan asked for are as specified.
- **The test lives in its own file** (`SettingsChromeTests.cs`) rather than inside
  `SettingsPreviewSectionTests`, which is named for the Preview group. It reuses that file's harness
  *pattern* — a real view in a plain `Window` under `[AvaloniaFact]` — and the existing `TempSettings`
  fake for the settings root, so no second harness was built. Two assertions beyond the plan's three:
  the well's brushes resolving from the theme, and following a variant switch. They are what turn "no
  hard-coded colour" from a code-review reading into a test.
- **The `SettingsView.axaml` diff looks far bigger than it is.** Wrapping the panel in a `Border`
  re-indents 188 lines by two spaces; `git diff -w` shows the real change as 15 insertions and 2
  deletions.
- **The visual criteria are a manual reading, and were not observed in this session.** The app was
  not launched: whether the well's contrast against the cards is right in each variant, whether the
  dialog's proportions now look correct, that the content scrolls to the bottom of *Advanced* against
  `DialogMaxHeight="640"`, and that *Close*, `Escape` and a scrim click still dismiss it. Nothing in
  this change touches the host, the dialog's buttons or its dismissal paths, and the width claim is
  arithmetic (808 ≤ 860), but the appearance itself needs one look in each theme.
- **Known consequence, accepted in the plan:** the template wraps the content in its own
  `ScrollViewer`, so the well scrolls with the content — its top edge shows at scroll-top and its
  bottom edge at scroll-end — rather than framing a fixed viewport. This is the trade for keeping the
  change inside the view.
- **Follow-up (library):** `Enigma.Avalonia.Desktop`'s `ContentDialog` template hard-codes
  `Background="{DynamicResource EnigmaSurfaceHighBrush}"` on the dialog border instead of binding the
  control's `Background`, so a host application cannot set a dialog's own colour. Fixing that in the
  library is the proper long-term move and would make this well unnecessary; it is out of scope here.
- **Line endings:** the touched files are LF throughout and the diff shows no CRLF/LF churn; no
  action taken and none recommended.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **succeeded, 0 Warning(s), 0 Error(s).**
- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal | grep -i AVLN` —
  **no output**, so the XAML change raises no `AVLN` diagnostic (which `TreatWarningsAsErrors` would
  not have caught, since they come from an MSBuild task).
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3295 passed, 0 failed,
  0 skipped**, up from 3290 at the end of PHASE01: exactly the five new chrome tests.
