#!/usr/bin/env bash
set -euo pipefail

test -f src/Plugin.csproj

STEAMCMD_PATH="${STEAMCMD_PATH:-/home/steam/steamcmd/steamcmd.sh}"
GAME_PATH="${XDG_CACHE_HOME:-$HOME/.cache}/steam/${STEAM_APP_ID}"
BEPINEX_VERSION="${BEPINEX_VERSION:-5.4.23.5}"
BEPINEX_PACKAGE="${BEPINEX_PACKAGE:-BepInEx_win_x64_${BEPINEX_VERSION}.zip}"
BEPINEX_URL="${BEPINEX_URL:-https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/${BEPINEX_PACKAGE}}"

find_managed_dir() {
    local result
    result="$(find "$GAME_PATH" -name "UnityEngine.dll" -type f 2>/dev/null | head -1)"
    dirname "$result"
}

game_is_installed() {
    [[ -n "$(find_managed_dir)" ]]
}

bepinex_is_installed() {
    [[ -f "$GAME_PATH/BepInEx/core/BepInEx.dll" ]]
}

ensure_parent_writable() {
    local target="$1"
    local parent
    parent="$(dirname "$target")"

    mkdir -p "$parent" 2>/dev/null || {
        echo "Cannot create install parent directory: $parent" >&2
        echo "Current user: $(id -un)" >&2
        exit 1
    }

    [[ -w "$parent" ]] || {
        echo "Install parent directory is not writable: $parent" >&2
        echo "Current user: $(id -un)" >&2
        exit 1
    }
}

download_to_file() {
    local url="$1"
    local output="$2"

    if command -v curl >/dev/null 2>&1; then
        curl -fL --retry 3 --retry-delay 1 -o "$output" "$url"
    elif command -v wget >/dev/null 2>&1; then
        wget -O "$output" "$url"
    else
        echo "Need curl or wget to download BepInEx." >&2
        exit 1
    fi
}

extract_zip() {
    local archive="$1"
    local dest="$2"

    if command -v unzip >/dev/null 2>&1; then
        unzip -qo "$archive" -d "$dest"
    elif command -v bsdtar >/dev/null 2>&1; then
        bsdtar -xf "$archive" -C "$dest"
    elif command -v 7z >/dev/null 2>&1; then
        7z x -y "-o$dest" "$archive" >/dev/null
    else
        echo "Need unzip, bsdtar, or 7z to extract BepInEx." >&2
        exit 1
    fi
}

download_game() {
    if [[ ! -x "$STEAMCMD_PATH" ]]; then
        echo "steamcmd.sh not found or not executable: $STEAMCMD_PATH" >&2
        echo "Set STEAMCMD_PATH to your SteamCMD install." >&2
        exit 1
    fi

    ensure_parent_writable "$GAME_PATH"
    mkdir -p "$GAME_PATH"

    echo "Steam app ($STEAM_APP_ID) is not installed."
    echo

    attempt_download() {
        local login_label="$1"
        shift

        echo "Trying download with Steam login: $login_label"

        if "$STEAMCMD_PATH" \
            +force_install_dir "$GAME_PATH" \
            +@sSteamCmdForcePlatformType windows \
            +login "$@" \
            +app_update "$STEAM_APP_ID" validate \
            +quit; then
            if game_is_installed; then
                return 0
            fi
        fi

        return 1
    }

    echo "Downloading app $STEAM_APP_ID..."
    echo

    if attempt_download "anonymous" anonymous; then
        return 0
    fi

    echo
    echo "Anonymous download failed."

    if [[ -n "${STEAM_USER:-}" && -n "${STEAM_PASS:-}" ]]; then
        echo "Retrying with Steam user credentials..."
        attempt_download "$STEAM_USER" "$STEAM_USER" "$STEAM_PASS" && return 0
    elif [[ -n "${STEAM_USER:-}" ]]; then
        echo "Retrying with Steam user login..."
        attempt_download "$STEAM_USER" "$STEAM_USER" && return 0
    fi

    echo "Unable to download app $STEAM_APP_ID." >&2
    echo "Anonymous access failed, and no working Steam login was available." >&2
    echo "Set STEAM_USER (and STEAM_PASS if needed) to download with an authenticated account." >&2
    exit 1
}

install_bepinex() {
    ensure_parent_writable "$GAME_PATH"
    mkdir -p "$GAME_PATH"

    local tmpdir
    local archive
    tmpdir="$(mktemp -d)"
    archive="$tmpdir/$BEPINEX_PACKAGE"

    echo "BepInEx is not installed at:"
    echo "  $GAME_PATH"
    echo
    echo "Downloading BepInEx..."
    echo "  $BEPINEX_URL"
    echo

    download_to_file "$BEPINEX_URL" "$archive"
    extract_zip "$archive" "$GAME_PATH"

    rm -rf "$tmpdir"
}

if ! game_is_installed; then
    download_game
fi

MANAGED_DIR="$(find_managed_dir)"

if ! bepinex_is_installed; then
    install_bepinex
fi

if ! bepinex_is_installed; then
    echo "BepInEx still does not appear to be installed correctly at:" >&2
    echo "  $GAME_PATH" >&2
    exit 1
fi

export GAME_PATH
export MANAGED_DIR

echo $MANAGED_DIR

dotnet restore src/Plugin.csproj -p:GamePath="$GAME_PATH" -p:ManagedDir="$MANAGED_DIR"
dotnet build src/Plugin.csproj -c Release --no-restore -p:GamePath="$GAME_PATH" -p:ManagedDir="$MANAGED_DIR"

echo
echo "Build output:"
ls -lah src/bin/Release/net472/