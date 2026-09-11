# BUG-7AB2-PHASE05 — Preview code font size & weight

**Status: DONE** · Phase 05 of 5 · Branch: `bugfix/bug-7ab2-phase05-preview-code-font`
Plan: `docs/plan/BUG-7AB2.md`

## Summary

Code in the preview was too big and looked bold. Two independent causes, both as the plan described
them, and both confirmed against the code before anything was changed.

**Size.** `MarkdownPreviewTypography.CodeFontSize` was `BodyFontSize - 1` — 17pt against the 18pt
`PreviewFontSize` default — and inline spans were worse: `MarkdownVisualBuilder.Code(CodeInline)`
deliberately set only the family and let the size inherit, so `` `code` `` inside a sentence came out
at the full 18pt, the largest code in the preview. Code is now sized by a setting of its own,
`AppSettings.PreviewCodeFontSize`, an absolute `int` in `[6, 72]` defaulting to **14**, carried
through `MarkdownPreviewTypography` as a record parameter instead of a computed member. Absolute
rather than an offset from prose, because how much smaller a fixed-width face has to be set to read
level with the prose beside it depends on the two families the reader picked — which is not a
subtraction. `AsideFontSize` stays derived from the body size: an aside is prose, not code.

**Weight.** `MarkdownCodeBlock.Run` applied `FontWeight.Bold` whenever the TextMate token said so,
and Dark+/Light+ mark whole scopes bold, so fenced keywords came out heavier than the same text in
the editor, which paints its tokens through a different path. That branch is gone. Colour, italic and
underline are kept, and `MarkdownCodeToken.IsBold` and the highlighter that fills it are untouched —
the token stays a faithful reading of the theme; the renderer simply declines to act on that one
field, with a comment at the render site saying why.

The new setting reaches an open preview through the seam that already existed:
`MarkdownPreviewViewModel.Typography` is computed per read and `OnSettingsChanged` re-raises it, so
no new plumbing was needed for a live change.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Core`**

- `Settings/AppSettings.cs` — new `PreviewCodeFontSize` (`int`, `[Range(6, 72)]`, default 14), placed
  after `PreviewCodeFontFamily` so the two code settings sit together, with an XML doc stating why it
  is absolute rather than derived. `PreviewFontSize`'s own doc no longer claims code is derived from
  it.
- `Settings/AppSettingsValidator.cs` — **unchanged.** The plan allowed for a cross-property rule; none
  is needed. The range is one property's business and `[Range]` already states it, and adding a rule
  here would give one broken value two messages, which is exactly what that class documents itself as
  avoiding.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Controls/MarkdownPreviewTypography.cs` — `CodeFontSize` is now a record parameter between
  `BodyFontSize` and `LineHeight`; the computed member is gone. `Default` passes `CodeFontSize: 13`,
  a point under its 14pt body, so a preview built by hand — a designer, a test — looks exactly as it
  did before the setting existed. Doc comments updated on the type, the two size parameters and
  `Default`.
- `Controls/MarkdownVisualBuilder.cs` — `Code(CodeInline)` now sets `run.FontSize = _type.CodeFontSize`
  alongside the family. The comment that explained the old exception is replaced by one explaining the
  new rule.
- `Controls/MarkdownCodeBlock.cs` — the `if (token.IsBold)` branch is removed and replaced by a comment
  saying the weight is deliberately not applied. `Typography`'s doc no longer says the sizes are
  derived from the body size.
- `ViewModels/MarkdownPreviewViewModel.cs` — `Typography` passes `_settings.Value.PreviewCodeFontSize`.
- `ViewModels/SettingsWindowViewModel.cs` — new `PreviewCodeFontSize` (`int?`), mirroring
  `PreviewFontSize` exactly, including the null-assignment behaviour.
- `Views/SettingsWindow.axaml` — a "Code font size" `SettingsCard` with an
  `IntEditor Width="110" Unit="pt"`, directly after "Code font" in the Preview group. One stale
  comment above the group ("these four") generalized.

**Modified — `README.md`**

- The preview bullet listed the group's settings and was one short. It now says code has a font *and*
  a size, and why the size is set on its own. (Accepted in the documentation freshness sweep.)

**Modified — `tests/Enigma.MarkdownEditor.Core.UnitTests`**

- `Settings/AppSettingsTests.cs` — the default is 14.
- `Settings/AppSettingsValidatorTests.cs` — `PreviewCodeFontSize` added to both range theories: 6 and
  72 accepted, 5 and 73 reported against their own property.
- `Settings/AppSettingsRoundTripTests.cs` — the key round-trips (written as 12, deliberately not the
  default, so the assertion cannot pass by asserting the default against itself); the written file
  contains the key; a settings file from before the preview settings existed loads it at 14 with no
  failure and no quarantine.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Controls/PreviewTree.cs` — the shared typography carries `CodeFontSize: 13`.
- `Controls/MarkdownPreviewTypographyTests.cs` — the shared `Chosen` record now carries
  `CodeFontSize: 15` against a 20pt body, chosen so that neither the body size nor a derived 19 can
  satisfy an assertion. An inline code span is asserted to be **at** the code size and different from
  its paragraph's (this is the assertion that fails before the fix); a fenced block at 15/30 instead
  of 19/38. **1 new test**: the code size is carried, not derived — moving the body size to 30 leaves
  the code size at 15 while the aside size follows to 28.
- `Controls/MarkdownCodeBlockTests.cs` — **1 new test**: what the theme marks bold renders at
  `FontWeight.Normal` with `FontWeightProperty` unset, and keeps its foreground. It guards against
  being vacuous by first asserting the highlighter still reports a bold token, in **both** variants —
  if Dark+ and Light+ ever stopped marking a scope bold there would be nothing left for the renderer
  to ignore and the test would prove nothing.
- `ViewModels/MarkdownPreviewViewModelTests.cs` — the code size reaches `Typography` at its default,
  and a change to it is republished by `OnSettingsChanged` as part of the same single notification.
- `ViewModels/SettingsWindowViewModelTests.cs` — **1 new test**: the property reads and writes its own
  setting and leaves the prose size alone. The existing null-assignment test covers the new editor too.
- `Views/SettingsPreviewSectionTests.cs` — the group has five rows in the planned order, and the new
  `IntEditor` is wired to the new property.

## Deviations & follow-ups

- **The inline-span and fenced-block size assertions went into `MarkdownPreviewTypographyTests`, not
  `MarkdownPreviewTests`.** The plan named the latter. `MarkdownPreviewTypographyTests` is the file
  whose entire subject is where each value of the record lands, and it already owned the two tests
  that had to change anyway (`ACodeSpan_…` asserted the size was *unset*; `AFencedBlock_…` asserted
  the derived 19). Putting the new assertions anywhere else would have left that file asserting the
  old contract next to a second file asserting the new one. `MarkdownPreviewTests` was not touched.
- **`AppSettingsValidator` was not changed**, as recorded above. The plan anticipated this ("if the
  range needs a cross-property rule; otherwise unchanged").
- **Two aside-like visuals moved from 17pt to 14pt at the defaults, and this was left as it is.**
  `MarkdownVisualBuilder.ErrorBlock` and `ImagePlaceholder` read `CodeFontSize`, not `AsideFontSize`,
  and always have; with code now set by its own setting they follow it rather than the prose size.
  Both are quiet, non-document text where smaller is if anything more correct, and the plan's file
  list does not include changing them. **Follow-up:** they are asides by the plan's own description of
  what `AsideFontSize` is for ("the front-matter chip, an image placeholder, the inline error block"),
  so moving those two onto `AsideFontSize` would make the code match that sentence. Out of scope here.
  `Literal` also reads `CodeFontSize` and *should* — it is monospace literal text and it takes the
  code family too.
- **One unexplained test failure that did not reproduce.** The second full-suite run reported
  `failed: 1`; the run's output had been filtered to the summary lines, so the test's name was lost.
  **Twelve subsequent full runs were green** (1958/1958), including six consecutive runs specifically
  looking for it with full output captured. It was not reproduced and cannot be attributed. Nothing in
  this phase's changes is timing- or order-dependent, and the suite uses per-test temp folders, which
  is the likeliest source of a one-off. Recorded here rather than left out: a flake seen once is still
  a flake seen. Worth watching on the next few builds.
- **The by-eye check was not performed and is left to the user** — the plan's last acceptance
  criterion ("confirm by eye that code no longer reads heavier than the prose around it, light and
  dark"). The application has no command-line file argument and resolves its data folder through the
  Windows known-folder API, so `%APPDATA%` cannot be redirected for a throwaway run; showing a
  document in the preview means backing up and restoring the user's real
  `%APPDATA%\Enigma.MarkdownEditor`. Offered and declined in favour of the user doing the pass. Every
  *mechanical* part of that criterion is proven by test — no run is bold, both code paths take the
  size — but the aesthetic judgement itself has not been made.
- **No line-ending noise.** All nineteen files touched are uniformly CRLF like the rest of the tree
  and the diff carries no whole-file churn. Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -v normal` (Debug): **succeeded, 0 errors, 0 warnings**, and
  no `AVLN*` diagnostic anywhere in the normal-verbosity log. The `AVLN` check matters here because
  `SettingsWindow.axaml` gained a compiled binding to a new property.
- `dotnet test` across the solution: **1958 passed, 0 failed, 0 skipped**
  (`Enigma.MarkdownEditor.Core.UnitTests` + `Enigma.MarkdownEditor.Desktop.UnitTests`). **3 tests are
  new** and roughly a dozen existing ones were retyped or extended; 1951 before this phase. See the
  one-off failure recorded under *Deviations & follow-ups* — thirteen runs, one red, not reproduced.
- **Both defects fail the new assertions against the unfixed code**, which is what makes them
  regression tests rather than descriptions:
  - the bold branch — `Assert.False(run.IsSet(TextElement.FontWeightProperty))` cannot hold while
    `MarkdownCodeBlock.Run` assigns `FontWeight.Bold`;
  - the inline span — `Assert.Equal(15, code.FontSize)` cannot hold while the property is left unset,
    and the old test asserted exactly that it *was* unset;
  - the fenced block — 15/30 cannot hold while the size is `BodyFontSize - 1` = 19.
- **Acceptance criteria, one by one.** A fenced block and an inline span both at `PreviewCodeFontSize`
  ✓ (`MarkdownPreviewTypographyTests`); changed in Settings without a restart ✓
  (`OnSettingsChanged_RepublishesTheTypographyAsOneChangeRatherThanOnePerSetting` and
  `AssigningTheTypography_RebuildsTheDocumentAtTheNewSize`); default 14 and a file with no key loading
  at 14 with no error and no quarantine ✓ (`AppSettingsTests`, `AppSettingsRoundTripTests`); 5 and 73
  rejected, 6 and 72 accepted ✓ (`AppSettingsValidatorTests`); no run in a fenced block bold in either
  variant with colours unchanged ✓ (`MarkdownCodeBlockTests`); the Settings row present and
  round-tripping through the file ✓ (`SettingsPreviewSectionTests`, `AppSettingsRoundTripTests`); build
  and suite ✓ as above. The one criterion **not** met by this dev is the by-eye reading, above.
