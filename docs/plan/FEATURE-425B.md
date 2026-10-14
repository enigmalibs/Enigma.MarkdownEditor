# FEATURE-425B — No success or info InfoBars

**Status: DONE** · single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-425b-drop-info-infobars`, cut from the run branch at build time.
**Run:** feature/2026-10-14-infobars-tree-multi-select

## Objective

The shell's InfoBar is for things the user has to act on or at least read: a warning or an error.
Stop raising it for success and informational messages, so a bar appearing always means something
went wrong.

## Context & constraints

Established by reading the code, not assumed.

- **Every InfoBar in the application goes through `IInfoBarService.ShowAsync`**, from seven call sites:
  `DocumentManagerViewModel`, `FileTreeViewModel`, `MainWindowViewModel` (startup notices, the crash
  and session paths), `WorkspacesViewModel`, `DocumentOpener` and `ImagePasteService`.
- **Exactly one of them is not a Warning or an Error**: `FileTreeViewModel.DeleteAsync`
  (`FileTreeViewModel.cs:546-554`) reports `'<name>' was moved to the recycle bin` / `trash` with
  `InfoBarSeverity.Info` after a successful trash. No call site uses `InfoBarSeverity.Success`.
- The private `TrashName` property (`FileTreeViewModel.cs:622`) exists only to word that message.
  `TreatWarningsAsErrors` and the `.editorconfig` analyzer set make an unused private member a build
  failure, so it goes with the message.
- **The inline bars are not InfoBars**: the external-change bar, the missing-roots notice and the
  workspace remove confirmation are `Border`s painted with the warning fill, each carrying a warning
  rather than a success. They are out of reach of this item by construction.
- The behaviour is pinned by `FileTreeViewModelTests.Deleting_SendsItToTheTrashAndTakesTheRowWithIt`,
  which asserts `Assert.Single(_infoBar.Shown)` with the comment "It does say where it went, though".
- README (*Workspaces and the file tree*) already says "Deletes go to the system trash, not to
  nowhere", so the fact the notice used to repeat on every delete stays documented.

## Steps

1. `FileTreeViewModel.DeleteAsync`: remove the trailing `if (trashed) { ReportAsync(…Info) }` block and
   the `TrashName` property. The trashed/permanent branching above it is unchanged.
2. Update the class `<remarks>` paragraph that says the file operations "report through the same
   InfoBar" so it says they report their *failures* there.
3. `FileTreeViewModelTests.Deleting_SendsItToTheTrashAndTakesTheRowWithIt`: the InfoBar assertion
   becomes `Assert.Empty(_infoBar.Shown)`, with its comment rewritten to say why a successful delete is
   silent. Every failure-path test keeps asserting its bar.
4. Grep `src/` for `InfoBarSeverity.Info` and `InfoBarSeverity.Success` — zero hits afterwards.
5. Release build (0 warnings, no `AVLN`), whole suite.

## Acceptance criteria

- No `InfoBarSeverity.Info` or `InfoBarSeverity.Success` remains anywhere in `src/`.
- A delete that went to the trash shows no InfoBar; a delete that fails still shows its Warning/Error bar.
- Release build: 0 warnings, 0 errors. Whole suite green.

## Out of scope

- Replacing the notice with another surface (status-bar text, a toast). Nothing asked for one.
- Any Warning or Error bar, and the inline warning `Border`s.
- A guard that forbids the two severities in future (an analyzer or a wrapper service): a one-line
  call site does not justify new infrastructure.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Item type | `FEATURE` | A behaviour change the user asked for | `BUG` — the notice worked as designed |
| What counts as a "success or info InfoBar" | Every `IInfoBarService.ShowAsync` with `Info` or `Success` severity — one call site today | Literal reading of the ask; the inline bars are warnings and are not InfoBars | Also removing warning-coloured inline bars |
| Replace the trash notice with something quieter? | No — remove it outright | The user said they do not want them; README already states where deletes go | A status-bar message; a timed InfoBar via 1.1.0's `DisplayDuration` (still an info bar) |
| Prevent future Info/Success bars mechanically | No | One call site; a wrapper or analyzer is infrastructure the prompt did not ask for | A `ReportAsync` that only accepts Warning/Error; an architecture test |
