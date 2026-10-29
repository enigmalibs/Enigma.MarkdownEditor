# FEATURE-B260 — Release 1.10.0

## Summary

Version **1.10.0** is cut, carrying this run's six items:

- `FEATURE-3185`: no splash screen;
- `FEATURE-982C`: the uniform About dialog;
- `FEATURE-7CE4`: `app.ico` in the output;
- `FEATURE-2877`: the package updates;
- `FEATURE-E31C`: unhandled exceptions and the start-up failure dialog;
- `FEATURE-F36D`: the publish scripts.

The release edits:

- `<Version>` set in both csproj files;
- the 1.10.0 MSI profile;
- a 1.10.0 `RELEASENOTES.md` section;
- the README what's-new callout;
- two new standing `docs/RELEASE.md` §1 checks.

No artifact was built, no tag created, nothing pushed.

## Files/modules touched

- Created:
  - `msiProfiles/Enigma.MarkdownEditor.Desktop.1.10.0.msipkg.json`
  - `docs/done/FEATURE-B260.md`
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj`
  - `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj`
  - `RELEASENOTES.md`, `README.md`, `docs/RELEASE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-B260.md`

## Deviations & follow-ups

- **No standing runbook check for the error dialogs.** The application has no way to provoke an
  unhandled exception by hand, so a check would be unverifiable. `CrashReporterTests`,
  `StartupGuardTests` and `StartupErrorWindowTests` cover them headlessly.
  - **Follow-up:** a Debug-only "throw" command would make the dialog's look checkable by eye.
- **MSI profile file names** keep the repository's convention (`<App>.<version>.msipkg.json`). The
  release skill's default would be `<App>.msiprofile.<version>.json`; the repo's own convention wins.
- `FEATURE-3185`'s open note is resolved: the 1.9.0 callout that named the splash is replaced.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**.
  - `CoreCompile` ran in all four projects (6 entries).
  - `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3648 passed, 0 failed, 0 skipped**.
- Both `AssemblyInfo.cs` files read `AssemblyFileVersion("1.10.0.0")` and
  `AssemblyInformationalVersion("1.10.0+bf3ac9a…")`.
- **The MSI profile:**
  - `diff` against 1.9.0 shows exactly the `version` and `productId` lines;
  - the new `productId` is `353d70f3-bfbd-4744-aa15-8c385cf2f735`, from Python's `uuid.uuid4()`;
  - all twelve profiles parse, with twelve distinct `productId`s and one shared `upgradeCode`
    (`a7b550b9-1338-458f-bf2b-66dcf1d25380`).
- **`1.9.0` no longer claimed as current.** `git grep 1.9.0` outside `docs/plan/`, `docs/done/`,
  `docs/roadmap.md`, `RELEASENOTES.md` and `msiProfiles/` returns nothing.
- **`RELEASENOTES.md`:** everything from the 1.9.0 heading down is byte-identical to `HEAD`'s file
  (`diff` is empty).
- `dotnet list package --outdated`: only `xunit.v3` 3.2.2 → 4.0.2 remains, held back by
  `FEATURE-2877` and recorded in the notes' *Dependencies*.
- **Nothing produced, nothing tagged.** `artifacts/` does not exist. `git tag --list` ends at
  `1.9.0`.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Notes sections | *New Features · Changes · Fixes · Dependencies · Compatibility · Version* | Every one is non-empty this time. The open-dialog takeover was a defect, so it is a *Fix* |
| Mention the About licence list's removal | Yes, under *Changes* | A visible removal; saying the terms are unchanged prevents a wrong reading |
| New RELEASE.md checks | Two, placed before the MSI-profile item | They are Windows checks on the release build, like the icon check before them |
