# Luma Island Player Limit

Raises the Steam lobby player cap from 4 to a configurable value for `Luma Island.exe`.

## Install

### Windows
1. Install **BepInEx 5.x** by extracting [**`BepInEx_win_x64_5.4.23.5.zip`**](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) from the BepInEx releases page into your **Luma Island** game folder.
2. Copy **`LumaPlayerLimit.dll`** to **`Luma Island/BepInEx/plugins/`**.
3. Launch the game once to generate **`Luma Island/BepInEx/config/LumaPlayerLimit.cfg`**.
4. Optional: edit **`MaxPlayers`** in that file to any value from **4–32**. The default is **8**.

### Linux / macOS (Proton or Wine)
1. Install **BepInEx 5.x** into the game's **Windows** directory exactly as you would on Windows.
2. In Steam, set this launch option:
   `WINEDLLOVERRIDES="winhttp.dll=n,b" %command%`
3. Copy **`LumaPlayerLimit.dll`** to **`Luma Island/BepInEx/plugins/`**.
4. Launch the game once to generate **`Luma Island/BepInEx/config/LumaPlayerLimit.cfg`**.
5. Optional: edit **`MaxPlayers`** in that file to any value from **4–32**. The default is **8**.

## How it works

The mod resolves types and members at runtime via `AccessTools.TypeByName()` so it survives game updates. All game-side calls are intercepted through a centralized `Reflect` helper rather than compile-time references.

Five patches are applied via Harmony:

| Patch | Type | Effect |
|-------|------|--------|
| `SteamLobbyController.CreateLobby` | Transpiler | Replaces hardcoded `4` with `GetConfiguredMaxPlayers()` at lobby creation |
| `SteamLobbyController.HostUpdatePlayerCount` | Prefix | Sets lobby joinable/full state via `SetLobbyJoinable`; updates `numPlayers` lobby data |
| `SteamLobbyController.OnReceiveLobbyData` | Transpiler | Replaces hardcoded `4` full-lobby check so larger lobbies aren't marked full remotely |
| `LobbyUtility.CanInvite` | Prefix | Invites allowed when `activePlayerCount < MaxPlayers` and friend is not already in a game with host |
| `JoinGameRow.Initialize` | Postfix | Updates lobby row text from `x / 4` to `x / MaxPlayers`; enables join button unless version mismatch |
| `JoinGameRow.OnJoinGame` | Prefix | Blocks join on version mismatch; otherwise calls `JoinLobby` via reflection |

## Config

- `MaxPlayers`: integer, default `8`, range `4`–`32`
- The effective cap is always at least `4` (`Math.Max(4, configured)`), even if the config is set lower
- Generated at `BepInEx\config\LumaPlayerLimit.cfg` on first run

## Source layout

```
src/
├── Plugin.cs              — BepInEx entry point; binds config; registers patches
├── Core.cs                — Shared `PatchNamedMethod` helper (centralized Harmony patching)
├── Reflect.cs             — Reflection access for static/instance members, methods, and properties
├── TranspilerHelpers.cs   — IL analysis utilities (stack simulation, pattern matching, signature tracking)
└── patches/
    ├── SteamLobbyController.cs  — Lobby creation, host update, and remote lobby-data patches
    ├── LobbyUtility.cs          — Invite eligibility patch
    └── JoinGameRow.cs           — Lobby row text and join button patches
```

## Transpiler behavior

The `CreateLobby` and `OnReceiveLobbyData` transpilers perform runtime IL analysis:

1. Locate all `ldc.i4 4` instructions in the target method
2. Inspect subsequent IL to distinguish lobby-cap constants from unrelated constants (e.g., array sizes)
3. Only replace constants that are followed by lobby-related calls (`CreateLobby`, `SetLobbyData`) or comparison ops (`Clt`, `Cgt`, etc.)
4. Validate stack depth before applying each replacement to avoid breaking the method's IL logic
5. Track a signature baseline for each method; if the game updates and the IL signature similarity drops below 70%, a warning is logged but the patch is still attempted

This makes the mod resilient against many game updates without requiring immediate patches.

## Build

### Prerequisites

- SteamCMD installed (for downloading the game)
- `STEAM_APP_ID` environment variable
- On Windows: .NET SDK with `dotnet` CLI, or Visual Studio 2022
- On Linux/macOS: .NET SDK with `dotnet` CLI and `steamcmd`
- Game DLLs resolved from the Steam installation at build time

### Build script (Linux/macOS)

```sh
./build.sh
```

The script downloads the game via steamcmd if not present, installs BepInEx, then builds the plugin.

### Build with dotnet CLI directly (Windows or manual)

```sh
dotnet restore src/Plugin.csproj -p:GamePath="C:\path\to\Luma Island" -p:ManagedDir="C:\path\to\Luma Island\Luma Island_Data\Managed"
dotnet build src/Plugin.csproj -c Release --no-restore -p:GamePath="C:\path\to\Luma Island" -p:ManagedDir="C:\path\to\Luma Island\Luma Island_Data\Managed"
```

Output:

```
src/bin/Release/net472/LumaPlayerLimit.dll
```

### Dev Container

The `.devcontainer/Dockerfile` automatically installs steamcmd and Luma Island. To use authenticated login (if anonymous fails for your account):

```json
{
  "build": {
    "args": {
      "STEAM_ARGS": "+login username password"
    }
  }
}
```
