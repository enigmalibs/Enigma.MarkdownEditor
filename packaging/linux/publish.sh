#!/usr/bin/env bash
#
# Publishes Enigma Markdown Editor (Enigma.MarkdownEditor) for Linux x64:
#
#   dotnet publish <Desktop project> -c Release -r linux-x64 -p:PublishReadyToRun=true
#
# A Release build for linux-x64, compiled ReadyToRun so it starts faster, which runs on an installed
# .NET 10 runtime. The result lands in
#
#   src/Enigma.MarkdownEditor.Desktop/bin/Release/net10.0/linux-x64/publish/
#
# and dotnet's last line names that folder. Run Enigma.MarkdownEditor.Desktop in it.
#
# This publishes and nothing more: install.sh, beside it, builds and installs the application for
# the current user, and the release tarball comes from build/publish.ps1 (see docs/RELEASE.md).

set -euo pipefail

# Resolved from this script rather than the current directory, so it runs from anywhere.
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
readonly script_dir
readonly project="${script_dir}/../../src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj"

exec dotnet publish "${project}" -c Release -r linux-x64 -p:PublishReadyToRun=true
