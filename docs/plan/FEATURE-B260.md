# FEATURE-B260 — Release 1.10.0

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md` · **Branch:** `feature/feature-b260-release-1-10-0`, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Cut version **1.10.0**, carrying everything this run merged:

- `FEATURE-3185` (no splash);
- `FEATURE-982C` (uniform About);
- `FEATURE-7CE4` (`app.ico` in the output);
- `FEATURE-2877` (packages);
- `FEATURE-E31C` (unhandled exceptions);
- `FEATURE-F36D` (publish scripts).

The release sets the version, re-checks the dependencies, writes the release documentation, and
commits the versioned MSI profile. **No artifacts are built**: no MSI, no Linux tarball. The run
never tags or pushes.

This is the same shape as `FEATURE-977D` (1.9.0).

## Version choice: why 1.10.0

The user's rule is `x.y.z → x.(y+1).0`, so 1.9.0 → **1.10.0**. It is also a defensible SemVer MINOR:
new behaviour, and nothing a user depends on breaks.

## Context & constraints

- **The version lives in three places**, which move together:
  - `<Version>` in `src/Enigma.MarkdownEditor.Desktop/…csproj`;
  - `<Version>` in `src/Enigma.MarkdownEditor.Core/…csproj`;
  - the versioned MSI profile's file name and its `version` field.
- **This is an app, not a library**: no pack, no NuGet push.
- **Tags are a bare `X.Y.Z`**. `1.10.0` must not exist; the run does not create it.
- **`RELEASENOTES.md` is the single release-notes source.** Sub-sections appear in this order, and
  only when non-empty: *New Features · Changes · Fixes · Dependencies · Compatibility · Version*.
- **MSI profile**: clone 1.9.0, keep `upgradeCode` verbatim, generate a new `productId` with a real
  generator (`uuid.uuid4()`), and set `version`.
- **The `dotnet-release` skill** is the house release procedure. Load it at build time and follow it
  as far as it applies to an app.
- **README screenshots stay pending**, as in every release since 1.3.0.
- Every other item of this run must be `DONE` first. A quarantined item is left out of the notes,
  and that is recorded.

## Steps

1. **Version**: `<Version>1.10.0</Version>` in both `src/*` csproj files. Then grep for `1.9.0` and
   account for every remaining tracked hit.
2. **Dependencies**: re-run `dotnet list … package --outdated`. `FEATURE-2877` should have left
   nothing. Take any new stable bump that has appeared since, under the same rules, or record it.
3. **MSI profile**: `msiProfiles/Enigma.MarkdownEditor.Desktop.1.10.0.msipkg.json`. It is the 1.9.0
   profile with a new `version` and a freshly generated `productId`; `upgradeCode` stays byte for
   byte.
4. **`RELEASENOTES.md`**: prepend a `# Enigma Markdown Editor v1.10.0 Release Notes` section,
   written from this run's `docs/done/*.md`. Earlier sections stay byte-identical.
5. **`README.md`**: the what's-new callout for 1.10.0.
6. **`docs/RELEASE.md`**:
   - add standing §1 checks where nothing covers them yet: the About dialog's look; the error dialog
     on a forced UI-thread exception, if it can be triggered by hand; `app.ico` beside the `.exe`;
   - record each edit, or "no change".
7. **Pre-flight, run here**:
   - `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`, with
     **0 Warning(s)** and no `AVLN` in the log;
   - `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`, green;
   - both `AssemblyInfo.cs` files read `1.10.0`.

## Acceptance criteria

- `<Version>` reads `1.10.0` in both csproj files, and nothing tracked outside history still claims
  1.9.0 as current.
- The 1.10.0 MSI profile exists, is valid JSON, keeps `upgradeCode` byte for byte, and carries a
  fresh `productId` different from every earlier profile's.
- `RELEASENOTES.md` has a 1.10.0 section above 1.9.0, and every earlier section is unmodified.
- The README what's-new callout reads 1.10.0.
- The Release build has **0 warnings** and no `AVLN` output, and the full suite is green.
- No `.msi`, no `.tar.gz` and no `artifacts/` content is left behind. The run creates no tag and
  pushes nothing.

## Out of scope

- Building or publishing the artifacts, tagging, and pushing (all yours, per `docs/RELEASE.md`).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.10.0 | The user's rule: x.(y+1).0 | 2.0.0 (nothing breaks) |
| Build artifacts here | No | The run never tags or pushes, and RELEASE.md §2 needs a Linux machine | Building the MSI and tarball |
| A quarantined item | Left out of the notes and recorded | Notes describe what ships | Releasing it as "known issue" |
