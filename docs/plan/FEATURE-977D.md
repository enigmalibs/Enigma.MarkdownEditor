# FEATURE-977D — Release 1.9.0

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md` · **Branch:** `feature/feature-977d-release-1-9-0`, cut from the run branch at build time.
**Run:** feature/2026-10-28-new-icon-release

## Objective

Cut version **1.9.0**, carrying everything merged since the `1.8.0` tag — `FEATURE-EA38`, the new
application icon, from this run. Set the version, refresh the dependencies the release skill takes,
write the release documentation, and commit the versioned MSI profile. **No artifacts are built**:
no MSI, no Linux tarball, no publish. This run never tags or pushes.

Same shape as `FEATURE-23F7` (1.8.0).

## Version choice — why 1.9.0

The user's rule: `x.y.z → x.(y+1).0`, so 1.8.0 → **1.9.0**. It is also a defensible SemVer MINOR. The
application's identity changes visibly, and nothing a user depends on changes incompatibly.

| Shipped since 1.8.0 | Kind |
|---|---|
| `FEATURE-EA38` — new application icon (window, `.exe`, MSI shortcuts, splash, About, Linux launcher) | change |

## Context & constraints

- **The version lives in three places** and they move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field.
- **This is an app, not a library**: no `PackageId`, no pack, no NuGet push.
- **Tag convention is a bare `X.Y.Z`**: `1.6.0`, `1.7.0`, `1.8.0` exist; `1.9.0` must not.
- **`RELEASENOTES.md` is the single release-notes source.** Sub-section order as 1.8.0, only where
  non-empty: *New Features · Changes · Fixes · Dependencies · Compatibility · Version*.
- **MSI profile convention**: clone 1.8.0 — `upgradeCode` verbatim, a new `productId` from a real
  generator, `version` set.
- **`dotnet list package --outdated`** (read at planning time, 2026-10-28):
  - Markdig 1.4.0 → 1.5.1 (Core);
  - Microsoft.Extensions.Hosting 10.0.13 → 10.0.14;
  - the coupled Desktop set: Avalonia, .Desktop, .Themes.Fluent, .Fonts.Inter, .Headless,
    .Headless.XUnit 12.1.2 → 12.1.4, and Enigma.Avalonia.Desktop 1.2.0 → 1.3.0, which
    `Directory.Packages.props` places in the same set;
  - xunit.v3 3.2.2 → 4.0.2.
- **README screenshots stay pending**, as in every release since 1.3.0.
- `FEATURE-EA38` must be `DONE` first.

## Design

1. **Version**: `<Version>1.9.0</Version>` in both `src/*` csproj files. Grep for `1.8.0` afterwards
   and account for every remaining tracked hit.
2. **Dependencies** (`Directory.Packages.props`): take the non-coupled bumps, Hosting 10.0.14 and
   Markdig 1.5.1. Hold back the coupled Desktop set and xunit.v3 4.x. A bump that turns the suite
   red and does not yield to a small fix is held back too, and recorded.
3. **MSI profile**: `msiProfiles/Enigma.MarkdownEditor.Desktop.1.9.0.msipkg.json`, the 1.8.0 profile
   with `version` and a freshly generated `productId`; `upgradeCode` kept byte-for-byte.
4. **`RELEASENOTES.md`**: a `# Enigma Markdown Editor v1.9.0 Release Notes` section prepended, written
   from `docs/done/FEATURE-EA38.md`. Earlier sections byte-identical.
5. **`README.md`**: the what's-new callout for 1.9.0.
6. **`docs/RELEASE.md`**: read against the repository. Add a standing §1 check that the icon reads
   on a dark and a light taskbar/launcher, if no existing item covers it; record the edit or
   "no change".
7. **Pre-flight, run here**:
   - `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`, with
     **0 Warning(s)** and no `AVLN` in the log;
   - `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`, green;
   - both `AssemblyInfo.cs` files read `1.9.0`.

## Acceptance criteria

- `<Version>` reads `1.9.0` in both csproj files, and nothing tracked outside history still claims
  1.8.0 as current.
- `Directory.Packages.props` carries the Hosting and Markdig bumps, or a bump is held back with a
  recorded reason. The `--outdated` report is in the completion doc.
- The 1.9.0 MSI profile exists, is valid JSON, keeps the `upgradeCode` byte-for-byte, and carries a
  fresh `productId` differing from all earlier profiles'.
- `RELEASENOTES.md` has a 1.9.0 section above 1.8.0; every earlier section is unmodified.
- The README what's-new callout reads 1.9.0.
- Release build: **0 warnings**, no `AVLN` output, full suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content left; the run creates no tag and pushes nothing.

## Dependencies

Built **last**, after `FEATURE-EA38`.

## Out of scope

Building the MSI or the tarball · the manual Linux verification checklist · a GitHub Release · README
screenshots · the coupled Avalonia 12.1.4 / Enigma.Avalonia.Desktop 1.3.0 set · xunit.v3 4.x · a
`CHANGELOG.md`.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which version? | 1.9.0 | The user's rule (MINOR + 1, patch reset to 0), and a valid SemVer MINOR | 1.8.1 (does not follow the rule) |
| Dependency refresh | Take Hosting 10.0.14 and Markdig 1.5.1; hold the coupled Desktop set (Enigma.Avalonia.Desktop 1.3.0 with it) and xunit.v3 4.x | `dotnet-release`'s routine-release rule, the 1.8.0 precedent, and `Directory.Packages.props` naming the Enigma control library as part of the coupled set | Taking nothing; moving Enigma.Avalonia.Desktop alone |
| Where does the icon go in the notes? | *Changes* | It changes something every user sees but adds no capability | *New Features* |
| Build artifacts here? | No | Every release since 1.1.0 was cut this way; `docs/RELEASE.md` §4 is the user's | Producing the tarball here |

## Beyond-the-draft checklist

| Dimension | Resolution |
|---|---|
| Security & auth | N/A — version and docs only, plus two dependency patches/minors |
| Input validation & abuse | N/A |
| Performance & scalability | N/A |
| Concurrency | N/A |
| Error handling & resilience | A dependency bump that turns the suite red is held back |
| UX edge cases | The icon on dark/light shells: a `docs/RELEASE.md` manual check |
| Accessibility & i18n | N/A |
| Observability | `AssemblyInformationalVersion` checked as `1.9.0+<sha>` |
| Data migration & compatibility | None: no data shape changes; recorded under *Compatibility* |
| Deployment & operations | New MSI profile; tarball and installer unchanged apart from version and icons |
| Licensing & dependencies | Markdig (BSD-2) and Hosting (MIT) keep their licences |
| Testing | Full Release suite; no new tests (no new behaviour) |
| Documentation | Release notes, README callout, `docs/RELEASE.md` |
