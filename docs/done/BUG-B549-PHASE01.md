# BUG-B549-PHASE01 — xUnit cancellation-token hint

**Status: DONE** · Phase 01 of 2 · Branch: `bugfix/bug-b549-phase01-xunit-hint`
Plan: `docs/plan/BUG-B549.md`

## Summary

Rider reported `xUnit1051` — "should use `TestContext.Current.CancellationToken`" — on the five
`tree.MoveAsync(…)` calls in `FileTreeViewModelTests`. The rule's premise does not hold there: the
public `FileTreeViewModel.MoveAsync(string, string)` accepts no token by design, because its only
real caller is the drop handler, fire-and-forget from a pointer event with nothing to give, and the
work runs under the ViewModel's own `_work` CTS like every other tree operation. The analyzer is
keying on the **private** three-argument overload, which the test assembly cannot call.

The fix is the one the plan chose: a single `#pragma warning disable xUnit1051` … `restore` pair
around exactly the five move tests, with the reasoning written on the `disable` in the shape of the
existing `SYSLIB1054` suppression in `WindowsTrashService`. Nothing under `src/` was touched, and no
severity was overridden in `.editorconfig` — the rule stays live everywhere else, including for any
test added around this region.

**The plan's uncertainty about whether the suppression would hold was settled by a probe rather than
left open.** The plan noted that the pinned `xunit.v3` **3.2.2** analyzer does not raise this rule at
build time — only the IDE does — so a clean build proves nothing about whether the pragma is placed
correctly, and would not have caught a mis-scoped region until the day the package is upgraded. So
the rule was forced on (`dotnet_diagnostic.xUnit1051.severity = warning` in a throwaway
`.editorconfig` under the test project, with `TreatWarningsAsErrors` already solution-wide) and two
deliberate violations were compiled — one **inside** the pragma region, one **outside** it. The build
failed with exactly one `error xUnit1051`, on the outside probe (`FileTreeViewModelTests.cs(872,13)`);
the inside probe was silent. That is direct evidence that the region both suppresses and is bounded
where intended. The probes and the throwaway `.editorconfig` were then removed and the build and full
suite re-run clean.

## Files/modules touched

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `ViewModels/FileTreeViewModelTests.cs` — 11 lines added, none removed or changed. A 9-line comment
  and `#pragma warning disable xUnit1051` immediately before
  `MovingAFileIntoAFolder_ReReadsBothEnds`, and `#pragma warning restore xUnit1051` immediately after
  the closing brace of `AMoveThePlannerRejects_IsReportedRatherThanAttempted`. The region is the
  contiguous run of the five move tests and contains no other test; the next test in the file,
  `ADropIsRefusedUpFront_WhenItCouldOnlyMeanNothing`, is outside it.

**Modified — workflow artifacts**

- `docs/roadmap.md` — `BUG-B549` → `IN PROGRESS`, `PHASE01` → `DONE`.
- `docs/plan/BUG-B549.md` — item status → `IN PROGRESS`, PHASE01 status → `DONE`.

No file under `src/` was modified, as the acceptance criteria require.

## Deviations & follow-ups

- **No deviation from the plan's design.** The pragma pair, its scope and its comment are as
  specified; the two rejected alternatives (renaming the private overload, adding an optional token to
  the public method) were not taken.
- **Added beyond the plan: the suppression was proved, not assumed** — see the Summary. The plan asked
  for a fix "chosen to hold either way" but had no way to check that it does; the forced-severity
  probe supplies it. Nothing from the probe remains in the tree.
- **The Rider-side criterion is verified by inference, not observed.** "The five calls no longer
  report `xUnit1051` in Rider" cannot be checked from this session — the IDE was not run. What is
  established is that the compiler-visible diagnostic is suppressed in exactly that region, which is
  the same mechanism Rider honours; a glance at the file in the IDE will confirm the squiggles are
  gone.
- **`xunit.v3` 4.0.0 is available and unpinned work.** If the analyzer in 4.0.0 is the one that flags
  these calls, this suppression already covers it — that is what the probe shows. Upgrading is not
  part of this item; `FEATURE-53E4` (Release 1.4.0) is the natural place to consider it.
- **Line endings:** no CRLF/LF churn observed in the touched file (the diff is 11 clean insertions);
  no action taken and none recommended.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **succeeded, 0 Warning(s), 0 Error(s).**
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3290 passed, 0 failed,
  0 skipped.** Same count as before the change: the diff adds 11 lines and removes none, so no test
  was added or removed, as the acceptance criteria require.
- **Suppression probe** (temporary, reverted): with `xUnit1051` forced to `warning` and
  `TreatWarningsAsErrors` on, an in-region violation compiled silently and an out-of-region violation
  failed the build with `error xUnit1051` at `FileTreeViewModelTests.cs(872,13)`.
- No XAML was touched, so the `AVLN` sweep does not apply to this phase (it belongs to PHASE02).
