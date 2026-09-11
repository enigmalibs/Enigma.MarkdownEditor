#!/bin/sh
#
# Installs Enigma Markdown Editor for the current user, from the folder this script sits in.
#
# Everything lands under $HOME - no root, no package manager, nothing written outside the user's
# own directories - which is what makes the tarball an install any user can perform and undo.
#
# Usage:
#   ./install.sh                 install to the default locations
#   ./install.sh --prefix DIR    install the application into DIR instead
#   ./install.sh --help

set -eu

SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)

APP_ID="enigma-markdown-editor"
APP_NAME="Enigma Markdown Editor"
EXE_NAME="Enigma.MarkdownEditor.Desktop"

APP_DIR="${HOME}/.local/opt/${APP_ID}"
DESKTOP_DIR="${XDG_DATA_HOME:-${HOME}/.local/share}/applications"
ICON_DIR="${XDG_DATA_HOME:-${HOME}/.local/share}/icons/hicolor/256x256/apps"

usage() {
    cat <<USAGE
${APP_NAME} installer

  ./install.sh                 install to ${HOME}/.local/opt/${APP_ID}
  ./install.sh --prefix DIR    install the application into DIR instead
  ./install.sh --help          show this message

The desktop entry and icon always go to the XDG user directories:
  ${DESKTOP_DIR}
  ${ICON_DIR}
USAGE
}

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix)
            if [ $# -lt 2 ]; then
                echo "install.sh: --prefix needs a directory" >&2
                exit 2
            fi
            APP_DIR="$2"
            shift 2
            ;;
        --prefix=*)
            APP_DIR="${1#--prefix=}"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "install.sh: unknown argument '$1' (try --help)" >&2
            exit 2
            ;;
    esac
done

# Fail before touching anything if the tarball was unpacked partially or the script was copied out
# of it: a half-installed application is worse than one that refused to start installing.
for required in "${SCRIPT_DIR}/app/${EXE_NAME}" \
                "${SCRIPT_DIR}/share/${APP_ID}.desktop" \
                "${SCRIPT_DIR}/share/${APP_ID}.png"; do
    if [ ! -f "${required}" ]; then
        echo "install.sh: missing ${required}" >&2
        echo "            run this script from inside the unpacked release folder." >&2
        exit 1
    fi
done

echo "Installing ${APP_NAME}"
echo "  application  : ${APP_DIR}"
echo "  desktop entry: ${DESKTOP_DIR}/${APP_ID}.desktop"
echo "  icon         : ${ICON_DIR}/${APP_ID}.png"
echo

# Replace rather than merge: an upgrade that copied over the previous install would leave every
# file a newer version dropped - stale managed assemblies the host would happily still load.
if [ -d "${APP_DIR}" ]; then
    echo "  removing the previous install..."
    rm -rf -- "${APP_DIR}"
fi

mkdir -p -- "${APP_DIR}" "${DESKTOP_DIR}" "${ICON_DIR}"
cp -R -- "${SCRIPT_DIR}/app/." "${APP_DIR}/"

# The tarball already carries these modes; re-applying them costs nothing and covers an unpack
# that dropped them (a copy through a filesystem without permission bits, or an over-tight umask).
chmod 755 -- "${APP_DIR}/${EXE_NAME}"
if [ -f "${APP_DIR}/createdump" ]; then
    chmod 755 -- "${APP_DIR}/createdump"
fi
find "${APP_DIR}" -name '*.so' -exec chmod 755 -- {} +

# The desktop entry ships as a template because Exec must be an absolute path and this format
# expands no variables; the install prefix is only known here.
sed "s|@EXEC@|${APP_DIR}/${EXE_NAME}|g" \
    "${SCRIPT_DIR}/share/${APP_ID}.desktop" > "${DESKTOP_DIR}/${APP_ID}.desktop"
chmod 644 -- "${DESKTOP_DIR}/${APP_ID}.desktop"

cp -- "${SCRIPT_DIR}/share/${APP_ID}.png" "${ICON_DIR}/${APP_ID}.png"
chmod 644 -- "${ICON_DIR}/${APP_ID}.png"

# Best effort: most desktops pick both up by scanning, and neither tool is present everywhere.
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "${DESKTOP_DIR}" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "${XDG_DATA_HOME:-${HOME}/.local/share}/icons/hicolor" >/dev/null 2>&1 || true
fi

cat <<DONE

${APP_NAME} is installed.

  Launch it from your application menu, or run:
    ${APP_DIR}/${EXE_NAME}

  Settings, workspaces, session and logs live in:
    ${XDG_CONFIG_HOME:-${HOME}/.config}/Enigma.MarkdownEditor

  To uninstall, remove what this script wrote:
    rm -rf "${APP_DIR}"
    rm -f  "${DESKTOP_DIR}/${APP_ID}.desktop"
    rm -f  "${ICON_DIR}/${APP_ID}.png"

  Your settings and notes are not touched by those commands. To remove the settings too:
    rm -rf "${XDG_CONFIG_HOME:-${HOME}/.config}/Enigma.MarkdownEditor"
DONE
