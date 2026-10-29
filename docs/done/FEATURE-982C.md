# FEATURE-982C — Uniform About dialog

## Summary

The About dialog now has Enigma.GitClient's layout, style and colours:

- the 96 px icon;
- the product name (18, SemiBold, `EnigmaForegroundBrush`);
- `Version X.Y.Z`;
- the build line, quieter and selectable;
- the copyright.

It sits in a content dialog with no title, no icon and a single `Close` button, which is the
default. The "BUILT WITH" credits box is gone, together with `CreditEntry` and
`AboutViewModel.Credits` / `DefaultCredits`. `diff` against GitClient's
`Views/Dialogs/AboutView.axaml` shows only the namespace, the `x:Class` and the header comment.

## Files/modules touched

- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Views/AboutView.axaml`
  - `src/Enigma.MarkdownEditor.Desktop/Views/AboutView.axaml.cs` (summary)
  - `src/Enigma.MarkdownEditor.Desktop/ViewModels/AboutViewModel.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Services/AboutDialogService.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/ViewModels/AboutViewModelTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/AboutViewTests.cs`
  - `README.md`, `docs/RELEASE.md`, `docs/roadmap.md`, `docs/plan/FEATURE-982C.md`
- Created: `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/AboutDialogServiceTests.cs`
- Deleted: `src/Enigma.MarkdownEditor.Desktop/ViewModels/CreditEntry.cs`

## Deviations & follow-ups

- **Follow-up: third-party attribution.** The credits list was the app's only in-product attribution
  for the MIT, BSD and OFL components. A shared third-party notices file for every Enigma app,
  shipped beside the executable and in the MSI and tarball, would restore it without breaking the
  uniform About dialog.
- The `Func<AboutViewModel>` factory and the transient registration stay. They are plumbing; the
  layout is what had to match.
- The dialog host's `DialogMinWidth` stays at 360 (GitClient uses 380). The view is 380 wide, so the
  About card renders the same.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3620 passed, 0 failed, 0 skipped**.
  - The previous count was 3618.
  - −3 credits tests.
  - +2 view tests: the product-name brush, and no list below the copyright.
  - +3 `AboutDialogServiceTests`: no title or icon; Close only, and it is the default; the view over
    the factory's ViewModel.
- `git grep -n "Credits\|CreditEntry" -- src tests` returns nothing.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The view's header comment | States that the view is shared and GitClient's is the reference | The reference's comment names this app, which is circular here |
| How the brush is tested | `TryFindResource("EnigmaForegroundBrush")` is the same instance as `Foreground` | `HeadlessTestApp` merges the Enigma theme, so the key resolves |
| Documentation sweep (§7) | README *Start-up* About bullet and RELEASE.md §2 *About* item: credits replaced by name and copyright | Both described the removed credits |
