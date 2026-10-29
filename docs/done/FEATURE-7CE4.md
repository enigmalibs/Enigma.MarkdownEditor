# FEATURE-7CE4 — App icon in the output directory

## Summary

`Assets/app.ico` is now also copied to the output root as `app.ico`, next to
`Enigma.MarkdownEditor.Desktop(.exe)`. Every build and every publish therefore carries the icon as a
plain file that shortcuts, launchers and installers can point at. The file stays an
`AvaloniaResource`, so the windows' `avares://` icon is unchanged.

## Files/modules touched

- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj`: adds
    `<None Update="Assets/app.ico" CopyToOutputDirectory="PreserveNewest" TargetPath="app.ico" />`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/AppIconTests.cs`: adds
    `TheIcon_IsAlsoAFileBesideTheExecutable`
  - `docs/roadmap.md`, `docs/plan/FEATURE-7CE4.md`

## Deviations & follow-ups

- None from the plan. `dotnet msbuild -getItem:None` confirmed that the SDK's default glob already
  lists `Assets\app.ico`, so the item is an `Update`, as the plan anticipated.
- The file now also lands in the release artifacts that `build/publish.ps1` produces: the MSI harvest
  and the Linux tarball's `app/`. That is harmless, and `pack-linux.ps1` gives it mode 0644 like any
  non-executable file.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3621 passed, 0 failed, 0 skipped**,
  which is 3620 plus the new test.
- Where `app.ico` lands:
  - `src/Enigma.MarkdownEditor.Desktop/bin/Release/net10.0/app.ico` exists, and `cmp` shows it
    identical to `Assets/app.ico`. There is no `Assets/` folder in the output.
  - `tests/…Desktop.UnitTests/bin/Release/net10.0/app.ico` exists, through the project reference.
  - `dotnet publish … -c Release -r win-x64 -o <scratch>` puts `app.ico` beside
    `Enigma.MarkdownEditor.Desktop.exe`, `cmp`-identical. The scratch folder was deleted afterwards.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `TargetPath` vs `Link` | `TargetPath` | The SDK honours it: the file lands at the root without making it look linked in the IDE |
| Documentation sweep (§7) | No change | No doc describes the output folder's contents |
