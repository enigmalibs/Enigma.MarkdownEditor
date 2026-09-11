# BUG-B549 — Settings dialog polish & test hint

**Status: DONE** · 2 phases · Roadmap: `docs/roadmap.md`
Planned on `develop`; each phase cuts its own branch from `HEAD` at build time.

## Objective

Two unrelated small fixes reported against **1.3.0**, one per phase: an analyzer hint Rider shows in
`FileTreeViewModelTests`, and the settings content dialog's chrome — its heading icon, its padding
and its background. Both are confined to the **Desktop** project and its test project; no `Core`
change, no new package, no setting, no ViewModel behaviour change.

Root causes were established by reading the code and by running the build during the interview, and
are recorded per phase below. Any deviation from what is written here goes in that phase's
completion doc.

## Context & constraints

- **`TreatWarningsAsErrors` is on solution-wide** (`Directory.Build.props`) with
  `EnforceCodeStyleInBuild` beside it. The bar for both phases is a **0-warning** build.
- **`AVLN*` diagnostics are emitted by an MSBuild task** and are **not** promoted by
  `TreatWarningsAsErrors`, so `dotnet build … -v normal | grep -i AVLN` is part of PHASE02's check —
  it touches XAML.
- **Theme brushes are always `{DynamicResource Enigma…Brush}`**, never `StaticResource` and never a
  literal colour: the `Enigma*` colours are theme-scoped and the brushes are not, so a
  `StaticResource` freezes the colour and stops following a variant switch.
- **`dotnet test --solution Enigma.MarkdownEditor.slnx`** — the `--solution` flag is not optional
  under the Microsoft Testing Platform runner pinned in `global.json`.
- **The real-view harness already exists** for PHASE02:
  `tests/…Desktop.UnitTests/Views/SettingsPreviewSectionTests.cs` shows a real `SettingsView` over a
  real `SettingsViewModel` under `[AvaloniaFact]` and walks its logical tree. Extend that pattern
  rather than building a second harness.
- **PHASE02 is built before `FEATURE-53E4`** (Release 1.4.0), which documents it. `FEATURE-53E4`
  also bumps the Avalonia set 12.1.1 → 12.1.2, so this item is verified against 12.1.1 and the
  release's own pre-flight re-runs the whole suite against 12.1.2.

---

## PHASE01 — xUnit cancellation-token hint

**Status: DONE**

### Symptom

Rider reports, on each of the five `tree.MoveAsync(…)` calls in
`tests/…Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` (≈ lines 767, 787, 809, 828, 844):

> xUnit1051: Calls to methods which accept CancellationToken should use
> `TestContext.Current.CancellationToken` to allow test cancellation to be more responsive

### Root cause

Two findings, both established during the interview:

1. **The rule's premise is wrong here.** `FileTreeViewModel.MoveAsync(string, string)`
   (`src/…/ViewModels/FileTreeViewModel.cs:329`) accepts **no** `CancellationToken`. It defers to
   `RunAsync` (`:867`), which hands the work the ViewModel's own `_work.Token`, so a move is
   superseded by a later refresh or by disposal rather than by a caller's token. Its only real caller
   is the drop handler (`Views/FileTreeView.axaml.cs:220`), fire-and-forget from a pointer event with
   no token to give. What the analyzer is pointing at is the **private** three-argument overload at
   `:507` — a method the test assembly cannot even call.
2. **The diagnostic does not reproduce in the build.** With
   `dotnet_diagnostic.xUnit1051.severity = warning` forced on — and `TreatWarningsAsErrors` already
   solution-wide — a full rebuild of the Desktop test project reported **0 warnings** on those five
   calls, while a probe call (`File.WriteAllTextAsync` with no token) failed the build with
   `error xUnit1051` as expected. So the analyzer shipped in the pinned `xunit.v3` **3.2.2** does not
   flag them; Rider's report is the IDE's own. `xunit.v3` **4.0.0** is available and may well be the
   version that does flag them, which is why the fix is chosen to hold either way.

### Design

A **targeted suppression in the test file**, with the reasoning written down — not a production
change, and not a severity override in `.editorconfig` (the rule is genuinely useful and
`Core.UnitTests/Files/FileOperationsTests.cs` already follows it).

1. A single `#pragma warning disable xUnit1051` … `#pragma warning restore xUnit1051` pair around the
   five move tests — the contiguous region from `MovingAFileIntoAFolder_ReReadsBothEnds` through
   `AMoveThePlannerRejects_IsReportedRatherThanAttempted`. Tighter than a class-level
   `[SuppressMessage]`, which would cover a ~900-line file and mute the rule for tests added later.
2. A comment on the `disable`, in the shape of the existing `SYSLIB1054` suppression at
   `src/…Core/Files/WindowsTrashService.cs:120`, stating: the public `MoveAsync` takes no token by
   design, because a drop has none to give and the work runs under the ViewModel's own `_work` CTS
   like every other tree operation; the analyzer is keying on the private three-argument overload;
   and the rule is not raised by the pinned 3.2.2 analyzer at build time.

**Explicitly not done here:** renaming the private overload, and adding an optional token to the
public method. Both were considered and turned down — the first cannot be verified from a build that
never raises the diagnostic, and the second would add a linked-CTS path to production code for a
caller that has no token.

### Acceptance criteria

- The five `tree.MoveAsync` calls no longer report `xUnit1051` in Rider.
- The suppression covers exactly those five tests — no other test in the file is inside the pragma
  region.
- The `disable` carries the explanation above.
- No file under `src/` is modified by this phase.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 Warning(s)**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — green, with the same test count as
  before (no test added or removed).

---

## PHASE02 — Settings dialog chrome

**Status: DONE**

### Symptom

Three complaints against the settings content dialog:

1. The heading icon sits far from the title and pushes the content right, leaving a wide empty gutter
   down the left of the dialog.
2. The margins are too big and make the dialog look strange.
3. The dialog's background does not sit well behind the expanders in it — darker would be better.

### Root cause

- **The gutter is 64px of template**, and it is also clipping the content. The `ContentDialog`
  template puts `IconData` in its own grid column — a `48 × 48` `Viewbox` with `Margin="0,0,16,0"` —
  ahead of the content presenter. `SettingsView` declares `Width="760"`, the template's grid adds
  `Margin="24"` on both sides, and the host caps the card at `DialogMaxWidth="860"`
  (`Views/MainWindow.axaml:508`): 760 + 48 + 48 + 16 = 872 > 860, so the settings view is **squeezed
  by ~14px** and never gets the width it asks for. Removing the icon brings it to 808 and unclamps
  it.
- **The padding is applied twice.** `SettingsView`'s root `StackPanel` carries `Margin="20"` on top
  of the template's own `Margin="24"` — 44px all round. It is the **only** dialog view that does
  this: `AboutView`, `FirstRunView` and `WorkspaceManagerView` all set no outer margin and rely on
  the template's 24.
- **The depth order is inverted.** The dialog card is painted `EnigmaSurfaceHighBrush` — dark
  `#313335`, light `#DFE1E5` — and the `SettingsCardExpander`s on it are `EnigmaSurfaceBrush` — dark
  `#2B2D30`, light `#EBEDF0`. In **both** variants the cards are on the wrong side of the card
  holding them, which is what reads as "does not fit": nowhere else in the app does a surface sit on
  something lighter than itself.
- **The dialog card's colour cannot be set from the app.** The library template hard-codes
  `Background="{DynamicResource EnigmaSurfaceHighBrush}"` on the dialog border rather than binding
  the control's `Background`, and the host is a **single shared control**, so a per-showing value
  would leak into the next dialog anyway.

### Design

Everything stays inside the app's own view and its dialog service — no `App.axaml` style, no
`/template/` selector into the library's template, and no change to
`Enigma.Avalonia.Desktop`. Fixing the library so `ContentDialog` honours `Background` is the right
long-term move and is recorded as a follow-up, not done here.

**1 — Drop the heading icon** (`src/…/Services/SettingsDialogService.cs`)

Remove `dialog.IconData = Icon;` (`:59`) and, with it, the now-unused `private static readonly
Geometry Icon` field (`:26`) and its `using Avalonia.Media;`, `using Enigma.Icons.Avalonia;` and
`using Enigma.Icons.Phosphor;` directives — `ImplicitUsings` is disabled solution-wide, so an unused
using is a real leftover. The title `"Settings"` and `CloseButtonText = "Close"` are unchanged, and
`AboutDialogService` and `FirstRunDialogService` keep their own icons: this is Settings only.

**2 — A darker well, and one layer of padding** (`src/…/Views/SettingsView.axaml`)

The view's root becomes a `Border` wrapping the existing group `StackPanel`:

| Property | Value | Why |
|---|---|---|
| `Background` | `{DynamicResource EnigmaBackgroundBrush}` | The app's ground colour — dark `#1E1F22`, light `#F7F8FA`. The cards then sit on it exactly as the file tree and the editor pane sit on the window, which is the relationship the rest of the app already uses. Darker in dark mode, as asked; lighter in light mode, which is the correct direction there. |
| `Padding` | `16` | The well's own inset. Total around the cards becomes 24 (light frame) + 16 (dark well) = 40 versus 44 today — but only 24 of it is now dead light space. |
| `CornerRadius` | `8` | Matches the dialog card's own `CornerRadius="8"`. |
| `BorderBrush` / `BorderThickness` | `{DynamicResource EnigmaBorderSubtleBrush}` / `1` | The same 1px edge the settings cards themselves carry, so the well reads as designed rather than as a colour accident. |

The group `StackPanel` loses `Margin="20"` and keeps `Spacing="12"`; `Width="760"` stays on the
`UserControl`. All five groups keep `IsExpanded="True"`.

**Known consequence, accepted:** the template wraps the content in its own `ScrollViewer`, so the
well scrolls with the content — its top edge shows at scroll-top and its bottom edge at scroll-end,
rather than framing a fixed viewport. That is inherent to keeping the change inside the view, and it
is the trade taken deliberately over restyling the shared dialog template.

**3 — Test** (`tests/…Desktop.UnitTests/Views/`)

Extend the existing `[AvaloniaFact]` view-test pattern — `SettingsPreviewSectionTests` already shows
a real `SettingsView` over a real ViewModel and walks its logical tree. Assert, through the real
view:

- the view's root is a `Border` whose `Background` resolves the `EnigmaBackgroundBrush` resource,
  with `Padding` 16, `CornerRadius` 8 and `BorderThickness` 1;
- the group `StackPanel` inside it has no outer margin (`Margin` is `0`), which is the regression a
  future XAML edit is most likely to undo;
- the five groups are still `Appearance`, `Editor`, `Preview`, `Behaviour`, `Advanced` in that order
  — the existing assertion, re-checked through the new root so the restructure cannot silently drop a
  group.

Whether it *looks* right — the contrast of the well against the cards in each variant, and the
dialog's proportions — is a manual reading, stated as such in the completion doc, in line with the
note already at the top of `SettingsPreviewSectionTests`.

### Acceptance criteria

- The settings dialog shows **no heading icon**, and no unused field or using is left behind in
  `SettingsDialogService`.
- The dialog's content is no longer clipped: with the icon gone the view gets its declared 760px
  inside the host's 860px cap.
- The expanders sit on a visibly darker well in **dark** mode and read as raised in **light** mode;
  both variants are checked, and neither uses a hard-coded colour — every brush is a
  `{DynamicResource Enigma…Brush}`.
- The only padding around the cards is the template's 24 plus the well's 16; `Margin="20"` is gone.
- The dialog still scrolls to the bottom of *Advanced* against `DialogMaxHeight="640"`, and *Close*,
  `Escape` and a scrim click all still dismiss it.
- `AboutView` and `FirstRunView` are unchanged, icons included.
- New view test green; `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 Warning(s)**;
  `dotnet build … -v normal | grep -i AVLN` — no output;
  `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — green.

### Out of scope

Restyling the shared `ContentDialog` template from `App.axaml` · changing
`Enigma.Avalonia.Desktop` so `ContentDialog` honours `Background` (follow-up) · overriding the
`EnigmaSurfaceHigh` colour key app-wide · the About and First-run dialogs' icons · any change to
what a settings row edits, to the group order, or to `AppSettings` · the dialog's width and height
bounds on the host.
