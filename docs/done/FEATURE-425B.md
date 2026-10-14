# FEATURE-425B — No success or info InfoBars

**Status: DONE** · single phase · Branch: `feature/feature-425b-drop-info-infobars`
Plan: `docs/plan/FEATURE-425B.md`

## Summary

The shell's InfoBar now only ever opens for a warning or an error.

- **There was one informational bar, and it is gone**: after a delete that went to the trash, the
  tree said `'<name>' was moved to the recycle bin` (`trash` off Windows). A successful delete is now
  silent — its row leaving the tree is the report. A delete that fails, a trash that breaks, and a
  permanent delete that is refused all behave exactly as before.
- **No success bar existed** anywhere in the application, so nothing else changed. Every remaining
  `IInfoBarService.ShowAsync` call site sets `Warning` or `Error`.

## Files/modules touched

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/ViewModels/FileTreeViewModel.cs` — the `Info` report at the end
  of `DeleteAsync` removed, with the `TrashName` property that only existed to word it; a short comment
  in its place says why a delete that worked reports nothing; the class remarks now say the file
  operations report their *failures* through the InfoBar.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` —
  `Deleting_SendsItToTheTrashAndTakesTheRowWithIt` asserts no InfoBar was shown, and says why.

## Decisions taken at build time

None beyond the plan's.

## Deviations & follow-ups

- None from the plan. `grep -rn "InfoBarSeverity.Info\b\|InfoBarSeverity.Success" src` returns nothing.
- **Follow-up for the next release item:** a "Changes" line — deleting to the trash no longer shows a
  notice.
- **Line endings:** nothing to recommend; LF throughout, no churn in this diff.
- **Documentation freshness sweep:** nothing edited. README's file-tree bullet already says "Deletes go
  to the system trash, not to nowhere", which was the one fact the notice repeated; nothing in README
  describes the notice itself.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 warnings, 0 errors**; no `AVLN` line.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3586 passed, 0 failed, 0 skipped**
  (unchanged count: one assertion flipped, no test added or removed). Green on the first run; no fix
  cycle used.
- Acceptance criteria: no `Info`/`Success` severity left in `src/`; a trashed delete shows no bar
  (asserted); the failure-path tests (`ATrashThatBreaks_IsSurfacedRatherThanSwallowed`, the move and
  read failures) still assert theirs.
