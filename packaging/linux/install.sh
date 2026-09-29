#!/usr/bin/env bash
#
# Installs Enigma Markdown Editor (Enigma.MarkdownEditor) for the current user, into the XDG
# directories:
#
#   $XDG_DATA_HOME/enigma-markdown-editor                        the application
#   $XDG_BIN_HOME/enigma-markdown-editor                         a symlink to its launcher
#   $XDG_DATA_HOME/applications/enigma-markdown-editor.desktop   the launcher entry
#   $XDG_DATA_HOME/icons/hicolor/<N>x<N>/apps/…png               the icon, six sizes
#
# The same script runs from two places: from a checkout of the repository, where it builds the
# application first, and from inside the unpacked release tarball, where the application is already
# built and sits beside it in app/.
#
# No root, no sudo, nothing outside $HOME. Run it again to upgrade in place.

set -euo pipefail

readonly APP_ID='enigma-markdown-editor'
readonly APP_EXE='Enigma.MarkdownEditor.Desktop'
readonly ICON_SIZES=(16 32 48 64 128 256)

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
readonly script_dir
readonly repo_root="${script_dir}/../.."
readonly project="${repo_root}/src/${APP_EXE}/${APP_EXE}.csproj"

# The release tarball is this directory plus the self-contained publish in app/ (see
# pack-linux.ps1), so a launcher there means there is nothing to build.
readonly bundled_dir="${script_dir}/app"

data_home=${XDG_DATA_HOME:-$HOME/.local/share}
bin_home=${XDG_BIN_HOME:-$HOME/.local/bin}
config_home=${XDG_CONFIG_HOME:-$HOME/.config}

# Where the 1.x tarball installer put the application. Not derived from XDG_DATA_HOME, because
# that installer never read it.
readonly legacy_dir="${HOME}/.local/opt/${APP_ID}"

from_dir=''
self_contained=true
rid=''
build_flag=''

die() {
    printf 'install: %s\n' "$1" >&2
    exit 1
}

step() {
    printf '  %s\n' "$1"
}

usage() {
    cat <<'USAGE'
Usage: install.sh [options]

Installs Enigma Markdown Editor for the current user. From a checkout of the repository it
builds the application, bundling the .NET runtime, and installs the result; from inside the
unpacked release tarball it installs the application that came with it.

Options:
  --from DIR              Install an already-published directory instead of building one.
                          DIR must contain the Enigma.MarkdownEditor.Desktop launcher.
  --framework-dependent   Build against an installed .NET 10 runtime instead of bundling
                          one: a much smaller install, but the machine then needs the
                          runtime somewhere the application can find it.
  --rid RID               Build for this runtime identifier (default: this machine's).
  -h, --help              Show this and exit.

--framework-dependent and --rid only apply to a build, so they are refused together with
--from or inside the release tarball.

Where things go (XDG_DATA_HOME and XDG_BIN_HOME are honoured):
  ~/.local/share/enigma-markdown-editor                        the application
  ~/.local/bin/enigma-markdown-editor                          a symlink to its launcher
  ~/.local/share/applications/enigma-markdown-editor.desktop   the launcher entry
  ~/.local/share/icons/hicolor/.../enigma-markdown-editor.png  the icon

An installation made by an earlier version's installer, in ~/.local/opt/enigma-markdown-editor,
is replaced by this one and removed.

Your settings, workspaces, session and logs live in ~/.config/Enigma.MarkdownEditor and are
never touched; your notes are wherever you keep them. Remove everything again with
uninstall.sh, in this same directory.
USAGE
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --from)
            [[ $# -ge 2 ]] || die "--from needs a directory."
            from_dir=$2
            shift 2
            ;;
        --framework-dependent)
            self_contained=false
            build_flag=$1
            shift
            ;;
        --rid)
            [[ $# -ge 2 ]] || die "--rid needs a runtime identifier."
            rid=$2
            build_flag=$1
            shift 2
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            die "unknown option '$1'. Try --help."
            ;;
    esac
done

# Installing as root would write this user's application into root's home, or — with sudo -E —
# leave root-owned files in a user's. Neither is what anyone means by "install".
[[ ${EUID:-$(id -u)} -ne 0 ]] || die "do not run this as root; it installs into your own home directory."

# An empty XDG_DATA_HOME or XDG_BIN_HOME counts as unset — the XDG spec's rule, and what the :-
# defaults above already do — so a relative one is all there is left to refuse.
[[ ${data_home} = /* ]] || die "XDG_DATA_HOME must be an absolute path (got '${data_home}')."
[[ ${bin_home} = /* ]] || die "XDG_BIN_HOME must be an absolute path (got '${bin_home}')."

if [[ -z ${from_dir} && -f "${bundled_dir}/${APP_EXE}" ]]; then
    from_dir=${bundled_dir}
fi

if [[ -n ${from_dir} ]]; then
    # Honouring a build flag here would mean building after all, which the source cannot do; and
    # quietly installing the prebuilt copy would hand over something other than what was asked for.
    [[ -z ${build_flag} ]] || die "${build_flag} only applies when building, and '${from_dir}' is already built."
    [[ -d ${from_dir} ]] || die "--from '${from_dir}' is not a directory."
    [[ -f "${from_dir}/${APP_EXE}" ]] || die "--from '${from_dir}' holds no ${APP_EXE} launcher."
else
    [[ -f ${project} ]] || die "there is no application beside this script and no checkout to build one from; use --from DIR."
    command -v dotnet > /dev/null 2>&1 || die "dotnet is not on PATH; install the .NET 10 SDK, or use --from."
fi

readonly install_dir="${data_home}/${APP_ID}"
readonly desktop_dir="${data_home}/applications"
readonly icon_root="${data_home}/icons/hicolor"
readonly launcher="${install_dir}/${APP_EXE}"

if [[ -z ${rid} ]]; then
    case "$(uname -m)" in
        x86_64 | amd64) rid='linux-x64' ;;
        aarch64 | arm64) rid='linux-arm64' ;;
        *) die "cannot guess a runtime identifier for '$(uname -m)'; pass --rid yourself." ;;
    esac
fi
readonly rid

# The staging directory is a sibling of the install directory, so swapping it in is a rename on
# the same filesystem rather than a copy — a build that fails leaves the old install untouched.
staging="${data_home}/.${APP_ID}.staging.$$"
previous=''

cleanup() {
    rm -rf -- "${staging}"

    # A swap that got as far as moving the old install aside, then failed: put it back.
    if [[ -n ${previous} && -d ${previous} && ! -e ${install_dir} ]]; then
        mv -- "${previous}" "${install_dir}"
    fi

    [[ -z ${previous} ]] || rm -rf -- "${previous}"
}
trap cleanup EXIT

printf 'Installing Enigma Markdown Editor\n\n'

# ---------------------------------------------------------------- 1. get the application

if [[ -n ${from_dir} ]]; then
    step "Copying from ${from_dir}"
    mkdir -p -- "${staging}"
    cp -a -- "${from_dir}/." "${staging}/"
else
    if [[ ${self_contained} = true ]]; then
        step "Building for ${rid}, with the .NET runtime bundled"
    else
        step "Building for ${rid}, against an installed .NET runtime"
    fi

    dotnet publish "${project}" \
        --configuration Release \
        --runtime "${rid}" \
        --self-contained "${self_contained}" \
        --output "${staging}" \
        --verbosity quiet \
        --nologo

    [[ -f "${staging}/${APP_EXE}" ]] || die "the build produced no ${APP_EXE} launcher."
fi

# A copy through a filesystem without permission bits, or an over-tight umask, can drop these; the
# application cannot start without the first, nor write a crash dump without the second.
chmod 0755 -- "${staging}/${APP_EXE}"
if [[ -f "${staging}/createdump" ]]; then
    chmod 0755 -- "${staging}/createdump"
fi

# ---------------------------------------------------------------- 2. swap it into place

mkdir -p -- "${data_home}"

if [[ -e ${install_dir} ]]; then
    [[ -d ${install_dir} ]] || die "'${install_dir}' exists and is not a directory; move it aside yourself."

    previous="${install_dir}.previous.$$"
    mv -- "${install_dir}" "${previous}"
    step "Replacing ${install_dir}"
else
    step "Installing into ${install_dir}"
fi

mv -- "${staging}" "${install_dir}"

# ---------------------------------------------------------------- 3. retire a 1.x installation

# The 1.x installer's desktop entry and 256 px icon sit at the paths steps 6 and 5 overwrite, so
# the application directory is all that would be left of it: a full copy nothing launches any
# more. Only a directory that holds the launcher is taken to be one — anything else there is not
# ours to delete.
if [[ ${legacy_dir} != "${install_dir}" && -f "${legacy_dir}/${APP_EXE}" ]]; then
    rm -rf -- "${legacy_dir}"
    step "Removed the previous installation in ${legacy_dir}"
fi

# ---------------------------------------------------------------- 4. the launcher symlink

mkdir -p -- "${bin_home}"
ln -sfn -- "${launcher}" "${bin_home}/${APP_ID}"
step "Linked ${bin_home}/${APP_ID}"

# ---------------------------------------------------------------- 5. the icons

for size in "${ICON_SIZES[@]}"; do
    source_icon="${script_dir}/icons/${APP_ID}-${size}.png"
    [[ -f ${source_icon} ]] || die "missing icon '${source_icon}'."

    install -D -m 0644 -- "${source_icon}" "${icon_root}/${size}x${size}/apps/${APP_ID}.png"
done
step "Installed ${#ICON_SIZES[@]} icons under ${icon_root}"

# ---------------------------------------------------------------- 6. the desktop entry

template="${script_dir}/${APP_ID}.desktop.in"
[[ -f ${template} ]] || die "missing desktop-entry template '${template}'."

desktop_file="${desktop_dir}/${APP_ID}.desktop"
mkdir -p -- "${desktop_dir}"

# A framework-dependent build finds its runtime through DOTNET_ROOT, /etc/dotnet/install_location
# or /usr/share/dotnet — never through PATH. A runtime installed in $HOME (the dotnet-install
# script's default) is in none of those, so the application runs from a shell that has DOTNET_ROOT
# set and does nothing at all when clicked in the launcher, which starts it from a bare
# environment. Where that is the case, the entry carries the location with it.

exec_value="\"${launcher}\""

# Read from the installed tree rather than from the flag, because --from takes a directory
# somebody else published and says nothing about how: a self-contained publish carries the
# runtime beside the application, a framework-dependent one goes looking for it.
# An `&&` list is the whole statement here, so its failure would trip `set -e`: spelled as an
# `if` rather than the shorter form.
bundles_runtime=false
if [[ -f "${install_dir}/libcoreclr.so" ]]; then
    bundles_runtime=true
fi

if [[ ${bundles_runtime} != true ]] && [[ ! -d /usr/share/dotnet ]] && ! compgen -G '/etc/dotnet/install_location*' > /dev/null; then
    if dotnet_bin=$(command -v dotnet 2>/dev/null); then
        dotnet_root=$(dirname -- "$(readlink -f -- "${dotnet_bin}")")
        exec_value="env DOTNET_ROOT=\"${dotnet_root}\" ${exec_value}"
        step "Pinning DOTNET_ROOT=${dotnet_root} in the entry: the runtime is not where a launcher would look"
    else
        printf '  Warning: this is a framework-dependent build and no .NET runtime was found in a\n'
        printf '           standard location. The launcher entry may do nothing when clicked.\n'
    fi
fi

# Substituted in bash rather than with sed: an installation path is arbitrary text, and there is
# no sed delimiter that a path cannot contain. The launcher is quoted because the Exec key is
# word-split, so a home directory with a space in it would otherwise become two arguments.
while IFS= read -r line; do
    printf '%s\n' "${line//@EXEC@/${exec_value}}"
done < "${template}" > "${desktop_file}"

chmod 0644 -- "${desktop_file}"
step "Wrote ${desktop_file}"

# ---------------------------------------------------------------- 7. tell the desktop about it

# All three are conveniences: the caches rebuild by themselves eventually, and a machine without
# them is not a failed install.
command -v update-desktop-database > /dev/null 2>&1 && update-desktop-database -q "${desktop_dir}" 2>/dev/null || true
command -v gtk-update-icon-cache > /dev/null 2>&1 && gtk-update-icon-cache -q -t -f "${icon_root}" 2>/dev/null || true
command -v kbuildsycoca6 > /dev/null 2>&1 && kbuildsycoca6 --noincremental > /dev/null 2>&1 || true

printf '\nDone. "Enigma Markdown Editor" is in your application launcher.\n'

case ":${PATH}:" in
    *":${bin_home}:"*) printf 'Run it from a terminal with: %s\n' "${APP_ID}" ;;
    *) printf 'Note: %s is not on your PATH, so the %s command will not be found there.\n' "${bin_home}" "${APP_ID}" ;;
esac

printf 'Your settings, workspaces and logs live in %s/Enigma.MarkdownEditor.\n' "${config_home}"
printf 'To remove it again, run uninstall.sh: it is beside this script, and any copy of it will do.\n'
