# AGENTS.md

## Build

```sh
./build.sh
```

Or manually (requires game installed at `GamePath`):

```sh
dotnet restore src/Plugin.csproj -p:GamePath="..." -p:ManagedDir=".../Game_Data/Managed"
dotnet build src/Plugin.csproj -c Release --no-restore -p:GamePath="..." -p:ManagedDir=".../Game_Data/Managed"
```

Output: `src/bin/Release/net472/LumaPlayerLimit.dll`

## Required Env Vars

- `STEAM_APP_ID` - Steam app ID
- `STEAM_USER` / `STEAM_PASS` - Optional; anonymous download tried first

Game cached at: `~/.cache/steam/<STEAM_APP_ID>`

## Architecture

- **Target:** .NET Framework 4.7.2 (`net472`)
- **Framework:** BepInEx 5.x + Harmony 2.x
- **Host environment:** Linux development machine targeting a **Windows Unity game** that runs through Proton or Wine
- **Primary repo assumption:** This repo targets a **Unity Mono** game with managed assemblies available under `<Game>_Data/Managed`
- **Game types not referenced directly** - all patching uses `AccessTools.TypeByName()` + reflection; types like `JoinGameRow`, `LobbyUtility`, `SteamLobbyController` are resolved at runtime from game assemblies
- **Reflect helper** in `Plugin.cs:356` handles all reflection access (static/instance fields, properties, methods) - prefer using it over writing new reflection code
- **Patching strategy:** Prefer runtime patches and reflection over editing shipped game DLLs in place

## Reverse Engineering Workflow for Windows Unity Games on Linux

Use this section when the target game is a **Windows build** and your dev/modding workflow happens on **Linux**.

### Ground Rules

- Only reverse engineer games you own or are explicitly authorized to inspect.
- Do not use this repo or workflow to bypass DRM, defeat anti-cheat, or gain an advantage in multiplayer games.
- Do not commit redistributed game binaries, decompiled source dumps, or proprietary assets to the repo.
- Treat reverse engineering notes as implementation guidance, not redistributable source.

### 1. Start by classifying the target correctly

Before touching code, determine what you are actually modding:

- **Windows Unity Mono game:** expect `<Game>.exe` plus `<Game>_Data/Managed/Assembly-CSharp.dll`
- **Windows Unity Il2Cpp game:** expect `<Game>.exe`, usually `GameAssembly.dll`, and `<Game>_Data/il2cpp_data`
- **Native Linux Unity game:** this is a different install/runtime path and not the default assumption for this repo

For this repo, the happy path is still **Windows + Unity Mono**. If the target is Il2Cpp, document that immediately and treat it as a different toolchain and patching workflow.

### 2. On Linux, decompile Windows managed DLLs with Linux-friendly tools first

Preferred order:

1. **ILSpy / ILSpyCmd** on Linux for static inspection and source export
2. **AvaloniaILSpy** if you want a GUI on Linux
3. **dnSpyEx under Wine or a Windows VM** only when you specifically need its debugger/editor workflow

Use these tools to inspect:

- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll` if present
- Any game-specific managed plugin DLLs under `<Game>_Data/Managed`
- Unity and framework assemblies only as supporting context

Do not start by hex-editing or replacing DLLs. Start by mapping types and behavior.

### 3. Know where the files live on Linux for Windows games

For a Steam game running under Proton, you usually care about two locations:

- **Game install directory** - contains `<Game>.exe`, `<Game>_Data`, and where BepInEx files are installed
- **Proton prefix** - contains the Wine environment for that game and is where Proton/Wine configuration changes apply

When reverse engineering, read managed DLLs from the **game install directory**, not from random copies in the Proton prefix.

### 4. Build a patch map before writing code

For each feature change, record:

- Exact assembly name
- Full type name
- Full method signature
- Whether the member is static or instance-based
- Relevant fields/properties involved in the state transition
- Nearby call flow before and after the behavior you want to change

Also write down why the selected hook is the least invasive patch point.

Prefer small, targeted runtime patches over broad rewrites. If a postfix on an existing limit-calculation method works, use that instead of patching multiple downstream callers.

### 5. Prefer runtime patching over editing game binaries

Default order of operations:

1. **Harmony prefix/postfix** when you can intercept behavior cleanly
2. **Harmony transpiler** only when simpler hooks are not enough
3. **Reflection** for private/internal members when compile-time references are brittle
4. **Preloader patchers** only when runtime patching is not viable

Normal workflow for this repo is:

- inspect the game DLLs
- identify the patch point
- implement the change in the BepInEx plugin
- load it at runtime

Do **not** make direct edits to shipped game DLLs as the primary workflow.

### 6. Use reflection defensively against game updates

Because updates can rename or move members:

- Prefer `AccessTools.TypeByName()` over hard references to game assemblies
- Resolve methods, fields, and properties by name and validate that they were found
- Fail gracefully with explicit log messages when a target cannot be resolved
- Centralize reflection in the existing `Reflect` helper instead of duplicating access logic

If a hook is version-sensitive, log the resolved type/member names during startup so breakage is obvious after a game update.

### 7. BepInEx install expectations for Windows games running on Linux

If the target game is a **Windows build running under Proton/Wine**, treat BepInEx setup as a **Windows BepInEx** install, not a native Linux one.

- Install BepInEx into the **Windows game's install directory** next to `<Game>.exe`
- Under Proton/Wine, BepInEx relies on `winhttp.dll` proxy loading
- Proton/Wine usually needs an explicit `winhttp` DLL override before BepInEx will start

Acceptable ways to configure the override:

- Steam launch option: `WINEDLLOVERRIDES="winhttp.dll=n,b" %command%`
- Or configure `winhttp` in `winecfg` / `protontricks`

If BepInEx does not start, check for `BepInEx/LogOutput.txt` first before assuming your plugin is broken.

### 8. Mono vs Il2Cpp decision rules

#### If the target is Mono

- Use the repo's existing `net472` plugin workflow
- Reference the game's managed assemblies for compile-time context only when necessary
- Prefer runtime reflection and Harmony patches to keep the mod resilient

#### If the target is Il2Cpp

- Do **not** force Mono assumptions into the repo
- Document the indicators you found (`GameAssembly.dll`, `il2cpp_data`, no usable `Assembly-CSharp.dll` gameplay code)
- Switch to an Il2Cpp-capable BepInEx/tooling path before implementing gameplay patches
- Keep reverse engineering notes, but do not pretend the same patching surface exists

### 9. Keep notes, not decompiled dumps

When reverse engineering a new target, store concise notes such as:

- Game version inspected
- Proton version used, if relevant to reproduction
- Assembly name inspected
- Type and method names chosen for patching
- Why that hook was selected
- Known fragility or fallback targets

Do not check in raw decompiled source from the game. Summaries, signatures, and pseudocode notes are enough.

### 10. Validate findings in-game on Linux

After identifying a patch point:

- Add temporary `Logger.LogInfo(...)` lines around target resolution and patch execution
- Verify the patch runs only in the expected menu, scene, or gameplay state
- Check for side effects in lobby creation, joining, UI refresh, save/load, and host/client synchronization
- Confirm the plugin loads through Proton/Wine before blaming Harmony patch logic
- Remove or reduce noisy logging after validation

### 11. Debugging expectations on Linux

When logging is not enough:

- Prefer static analysis first with ILSpy tooling on Linux
- Use dnSpyEx only if you deliberately choose a Wine/Windows debugging path
- Re-check the live method body after each game update before trusting old control flow or member names
- Separate **loader failures** (BepInEx did not inject) from **patch failures** (plugin loaded but hook is wrong)

## Reverse Engineering Checklist

Before opening a PR that depends on reverse engineering work, confirm all of the following:

- [ ] Verified the game is a **Windows Unity game running under Proton/Wine** or documented why not
- [ ] Determined whether the scripting backend is **Mono** or **Il2Cpp**
- [ ] Located the relevant assembly or runtime artifacts in the game install directory
- [ ] Identified the exact target type and method signature
- [ ] Chosen the least invasive runtime patch point
- [ ] Used `AccessTools.TypeByName()` and/or the shared `Reflect` helper where appropriate
- [ ] Added clear failure logging for unresolved members
- [ ] Confirmed BepInEx itself loads under Proton/Wine before blaming the plugin
- [ ] Tested against the current target game version
- [ ] Avoided committing decompiled game source or modified game binaries

## Config

Config file generated at `BepInEx/config/LumaPlayerLimit.cfg` on first run. Key setting: `MaxPlayers` (default 8, range 4-32).