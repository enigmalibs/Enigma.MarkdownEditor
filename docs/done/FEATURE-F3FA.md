# FEATURE-F3FA — Tab strip polish

**Status: DONE** · Branch: `feature/feature-f3fa-tab-strip` · Plan: `docs/plan/FEATURE-F3FA.md`

## Summary

Three things about the tab strip that were wrong, and are now not. A modified tab can be closed
without saving it first; the tab you are editing is the one that looks it; and nothing is drawn on
top of the tabs you are trying to click.

**The close button is reachable on a modified tab.** The dot and the close button used to decide
their own visibility from inside the item template — `IsVisible="{Binding HasUnsavedChanges}"` and
its negation — and that is a local value, which in Avalonia outranks every style setter. No
`:pointerover` rule could have overridden it, which is why the comment above them promised a
behaviour the code could not deliver. So the state is re-expressed as a class the template binds
(`Classes.dirty="{Binding HasUnsavedChanges}"` on the item's root panel) and the two visibilities
become style rules keyed on that class and on the ancestor `ListBoxItem`'s `:pointerover`. Stated as
rules there are only two of them, because the close button showing is the resting state and the one
case that departs from it is a modified tab the pointer is *not* over. Hover is the whole tab rather
than the 8px dot, which is what every editor does and what the old comment already said.

**The slot is now genuinely one slot.** The plan says the dot and the button share it so the tab
never changes width; the old markup only said so in a comment. The dot was 8px with an 8px margin
and the button 18px with a 6px margin, so a tab silently grew 8px the moment you typed into it. Both
now live in a `Panel.tabSlot` of a fixed 18×18 with the 6px margin on the panel: 24px whichever of
the two is in it, and 24px when the pointer arrives and swaps them. The width an unmodified tab has
is unchanged from before; it is the modified one that stopped being narrower.

**The selected tab is told apart by foreground alone.** `TextBlock.tabTitle` is
`EnigmaForegroundSecondaryBrush` by default and a `ListBoxItem:selected` rule raises it to
`EnigmaForegroundBrush`. Both are palette keys, so the pair follows the theme variant with no second
definition to keep in step. Deliberately nothing else: an accent bar would compete with the 1px
border the strip already draws under itself, and bold would make the selected tab wider than it was
a moment ago and slide the whole strip out from under the pointer that just clicked it.

**The scrollbar is gone and the wheel replaced it.** Avalonia's Fluent `ScrollViewer` floats its
scrollbars over the content by design and grows them on hover; there is no supported way to make one
reserve space. On a horizontal strip 30px tall that is a bar across the bottom of the very tabs it
exists to reach. So the strip is `ScrollBarVisibility.Hidden` — which stops it being drawn without
stopping it scrolling — and `OnTabsPointerWheelChanged` turns the wheel's vertical delta into a
horizontal offset on `ListBox.Scroll`, clamped to the overflow, at the same 50px per notch Avalonia
itself uses. It declines the event when every tab already fits, so the wheel still belongs to
whatever is under the strip in the ordinary case. Bringing a newly selected tab back into view
needed nothing: `SelectingItemsControl.AutoScrollToSelectedItem` already does it, and the plan said
to verify rather than reimplement — so it is verified by a test instead of replaced by code.

## Files/modules touched

**Modified — Desktop**

- `Views/EditorPaneView.axaml` — the tab title's two-brush pair, the `Panel.tabSlot` and `Ellipse.dot`
  styles, the two `:not(:pointerover)` visibility rules, `Classes.dirty` on the item template's root
  panel with both `IsVisible` bindings dropped, the item template's new slot panel, the hidden
  horizontal scrollbar and the wheel handler's wiring.
- `Views/EditorPaneView.axaml.cs` — `TabWheelStep` and `OnTabsPointerWheelChanged`.

**Created — tests**

- `Desktop.UnitTests/Views/TabStrip.cs` — a real `EditorPaneView` in a shown 320px window over
  `EditorHarness`, plus the readings a test takes off its strip: the containers, the dot, the close
  button, the title, the widths, the scroll offset and the two headless input gestures.
- `Desktop.UnitTests/Views/EditorTabStripTests.cs` — the eight cases below.

**Modified — docs**

- `README.md` — the *Editor* section's `Tabs, …` bullet, which described the tab strip as it was
  before this dev. Folded in from the documentation freshness sweep, at your choice.

## Deviations & follow-ups

- **The dot's class was renamed `dirty` → `dot`.** The plan puts the `dirty` class on the item
  template's root panel, which is where the document's state belongs; leaving the ellipse's own
  styling class called `dirty` too would have made every selector read `StackPanel.dirty
  Ellipse.dirty` for two unrelated meanings of the word.
- **A `Panel.tabSlot` wrapper was added, which the plan does not mention.** The plan's design section
  asserts the two "share a slot, so the tab does not change width", and the acceptance criteria
  require it; the markup as written did not actually deliver that (16px versus 24px — see *Summary*).
  The wrapper is what makes the plan's own claim true, and it is asserted by
  `ATab_KeepsItsWidthThroughEveryOneOfThoseStates`.
- **The pointer states are covered by real input, not by inspection.** The plan allowed for the
  harness not being able to drive them; it can. `Avalonia.Headless`'s `MouseMove` and `MouseWheel`
  against a shown window raise the real `:pointerover` and the real wheel event, so every criterion
  in this dev is asserted by a test.
- **What the colours look like is still a manual pass.** The tests assert that the selected and
  unselected titles resolve to two *different* palette brushes in each of the Dark and Light
  variants; whether that difference reads as "visibly darker/lighter" on a screen is a judgement no
  headless test makes. Worth a look: open three notes, check the active tab's title stands out in
  both variants, hover a modified tab and watch the dot become a close button without the tab moving,
  and open enough notes to overflow the strip, then wheel over it.
- **Every mutation was checked.** Each of the four behaviours was deliberately broken in turn
  (`Hidden` back to `Auto`, `:selected` weakened, `:not(:pointerover)` dropped, the wheel's offset
  computation neutered) and the matching test failed each time, so none of the eight cases is
  vacuously green. The implementation and the tests were restored before the final build.
- **`OnTabsPointerWheelChanged` leaves the event unhandled when the tabs all fit.** That is on
  purpose — the strip has nothing to scroll, and swallowing the wheel there would make it a dead
  region — and it is asserted by `TheWheel_LeavesAStripWhoseTabsAllFitAlone`.
- **The Release build had to be redirected.** A copy of the app built earlier was running from
  `src/Enigma.MarkdownEditor.Desktop/bin/Release` and holding a lock on its own output, so the
  Release build and test run were done with `-p:ArtifactsPath=<temp>` rather than by closing the
  user's editor. Same sources, same result; only the output folder differs. The Debug build in the
  repository's own `bin/` was clean as well.
- **No sub-agents were used.** One XAML file, one handler and one test file, all of them tied to the
  same three styles — nothing here decomposes cleanly enough to be worth splitting.
- **Line endings:** no CRLF/LF churn was observed in this dev's diff, and nothing was done about line
  endings either way, per the workflow's recommendation-only rule.
- **Follow-up, not done:** everything the plan lists as out of scope is still out of scope — tab
  reordering by drag, an overflow dropdown or chevron buttons, and pinned tabs. Middle-click to close
  was already there before this dev.

## Build/test evidence

- `dotnet build -c Release -p:ArtifactsPath=<temp>` — succeeded, **zero warnings**
  (`TreatWarningsAsErrors` is on solution-wide, so a warning would have failed it). `dotnet build`
  (Debug, in-repo output) — also succeeded with zero warnings.
- `dotnet test -c Release -p:ArtifactsPath=<temp>` — **1730 passed, 0 failed, 0 skipped** (1722
  before this dev, so **+8 cases**). The same 1730 pass in Debug.
- `EditorTabStripTests` — **8 cases**, every one driven through a real pane in a shown window:
  - a modified tab showing the dot at rest, the close button under the pointer, and the dot again
    when the pointer leaves;
  - an unmodified tab showing the close button wherever the pointer is;
  - one tab keeping the same width at rest, modified, hovered and hovered-then-left;
  - the selected title resolving to `EnigmaForegroundBrush` and the others to
    `EnigmaForegroundSecondaryBrush`, in **both** the Dark and Light variants, and the two differing;
  - a strip with more tabs than room overflowing with its horizontal scrollbar declared `Hidden` and
    actually not visible;
  - the wheel moving the overflowing strip sideways, and back again the other way;
  - the wheel leaving a strip whose tabs all fit at offset zero;
  - selecting the last tab of an overflowing strip scrolling it into view, with the container proved
    to be inside the viewport.
- **Not covered:** how the two title brushes actually look on a screen — see *Deviations &
  follow-ups*.
