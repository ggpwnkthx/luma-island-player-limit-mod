# BepInEx Mod Development Container

A ready-to-use development environment for building [BepInEx](https://github.com/BepInEx/BepInEx) mods for Unity games, powered by [OpenCode](https://opencode.ai).

## What's Included

- **.devcontainer/** - Dev Container configuration with:
  - .NET SDK (2/8.0)
  - SteamCMD for downloading game files
  - OpenCode AI assistant pre-installed
  - VS Code extensions for C# development

- **.opencode/** - OpenCode configuration and dependencies

- **src/** - Plugin source code (replace with your mod)

- **build.sh** - Build script that:
  1. Downloads the target Unity game via SteamCMD
  2. Installs BepInEx into the game directory
  3. Builds the plugin against game assemblies

## Quick Start

### 1. Open in Dev Container

Open this repository in VS Code and click **"Reopen in Container"** when prompted. The dev container will:
- Install SteamCMD
- Download and set up the game
- Install OpenCode

### 2. Configure Your Mod

Edit the following files for your mod:

| File | Purpose |
|------|---------|
| `src/Plugin.cs` | Your BepInEx plugin code |
| `src/Plugin.csproj` | Project file (update assembly name, references) |

### 3. Environment Variables

Create a `.env` file in the project root (or set these environment variables):

```env
STEAM_APP_ID=1234567       # Your game's Steam App ID
STEAM_USER=your_username   # Optional: for authenticated downloads
STEAM_PASS=your_password   # Optional: for authenticated downloads
```

### 4. Build

```sh
./build.sh
```

Or manually with dotnet:

```sh
dotnet restore src/Plugin.csproj -p:GamePath="/path/to/game" -p:ManagedDir="/path/to/game/Game_Data/Managed"
dotnet build src/Plugin.csproj -c Release --no-restore -p:GamePath="/path/to/game" -p:ManagedDir="/path/to/game/Game_Data/Managed"
```

Output: `src/bin/Release/net472/YourMod.dll`

## Project Structure

```
.
├── .devcontainer/
│   ├── Dockerfile          # Dev container image definition
│   └── devcontainer.json   # Dev container settings
├── .opencode/
│   ├── package.json        # OpenCode dependencies
│   └── settings.json       # OpenCode configuration
├── src/
│   ├── Plugin.cs           # BepInEx plugin with Harmony patches
│   └── Plugin.csproj       # .NET Framework 4.7.2 project
├── build.sh                # Automated build script
└── .env                    # Environment variables (create this)
```

## Using OpenCode

Once inside the dev container, OpenCode is available as your AI coding assistant. You can:

- Ask questions about the codebase
- Generate new patches or features
- Debug issues with your mod
- Refactor and improve code

OpenCode reads the `.opencode/` configuration and uses the project context to provide relevant assistance.

## Dev Container Features

The dev container automatically:
- Installs SteamCMD for game download
- Sets up .NET SDK with support for game DLL references
- Configures OpenCode with project-aware context
- Provides VS Code extensions for C# development

### Customizing the Container

To use a Unity game, update `build.sh`:

```bash
STEAM_APP_ID=1234567
```

To use Steam login instead of anonymous:

```json
{
  "build": {
    "args": {
      "STEAM_ARGS": "+login username password"
    }
  }
}
```

## Installing Your Mod

Copy the built DLL to your game's BepInEx plugins folder:

```
<Game Installation>/BepInEx/plugins/YourMod.dll
```

Launch the game once to generate the config file at `BepInEx/config/`, then edit it to customize your mod's settings.
