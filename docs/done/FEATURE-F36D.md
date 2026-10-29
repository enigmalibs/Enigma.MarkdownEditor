# FEATURE-F36D — Publish scripts for Windows and Linux

## Summary

Three one-command publish scripts, laid out as in Enigma.GitClient. Each runs exactly the spec's
command against the Desktop project, resolved from the script's own folder, and exits with
`dotnet`'s exit code:

- `packaging/windows/publish.ps1` and `packaging/windows/publish.bat` run
  `dotnet publish <Desktop project> -c Release -r win-x64 -p:PublishReadyToRun=true`;
- `packaging/linux/publish.sh`, next to `install.sh` and `uninstall.sh`, runs the same with
  `-r linux-x64`.

`build/publish.ps1`, the self-contained release pipeline behind the MSI and the tarball, is
unchanged. The README and `docs/RELEASE.md` §4 now say which is which.

## Files/modules touched

- Created:
  - `packaging/windows/publish.ps1`
  - `packaging/windows/publish.bat` (CRLF)
  - `packaging/linux/publish.sh` (mode 100755)
- Modified:
  - `README.md` (*Build from source*: the local publish scripts; *Repository layout*)
  - `docs/RELEASE.md` (§4: these scripts are not the artifact publish)
  - `docs/roadmap.md`, `docs/plan/FEATURE-F36D.md`

## Deviations & follow-ups

- `pack-linux.ps1` needed no change. It lists the tarball's files explicitly (`install.sh`,
  `uninstall.sh`, `LICENSE.md`, the desktop template, icons, `app/`), so `publish.sh` never ships in
  the tarball.
- The scripts pass the `.csproj` path rather than the project folder. The command is the same, and the
  explicit file cannot be ambiguous.

## Build/test evidence

- **Each script run from the scratch directory**, not the repo:
  - `cmd /c …\packaging\windows\publish.bat` exited **0**, writing
    `src/…Desktop/bin/Release/net10.0/win-x64/publish/` (90 files, with
    `Enigma.MarkdownEditor.Desktop.exe` and `app.ico`).
  - `powershell -ExecutionPolicy Bypass -File …\publish.ps1` exited **0**, writing the same folder.
  - `bash …/packaging/linux/publish.sh` (Git Bash; cross-OS ReadyToRun) exited **0**, writing
    `…/linux-x64/publish/` (86 files, with the extensionless `Enigma.MarkdownEditor.Desktop`,
    `libSkiaSharp.so` and `app.ico`).
- **Failure passes through** (`dotnet` removed from `PATH`): `publish.sh` exited 127, `publish.bat`
  exited 9009 (shown as 49 once truncated to a byte), and `publish.ps1` exited 1.
- `git ls-files -s packaging/linux/publish.sh` shows `100755`. `publish.bat` is CRLF in the working
  tree (`file` reports "with CRLF line terminators").
- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3648 passed, 0 failed, 0 skipped**.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `.bat` header | `rem` lines restating the command and the output folder | The same information as the `.ps1` help, for a reader who opens the `.bat` |
| Documentation sweep (§7) | README *Build from source* and *Repository layout*, RELEASE.md §4 | The layout listed `build/` as "publish scripts" and `packaging/linux` without `publish.sh` |
